using Fluxer.Net;
using Norma_Bot.Data;
using Norma_Bot.Models.Hoyoverse;

namespace Norma_Bot.Services.Hoyoverse;

public sealed class HoyoverseNotificationService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan EventWarningLeadTime = TimeSpan.FromDays(3);

    private static readonly GameDefinition[] Games =
    [
        new("genshin", "Genshin Impact", c => c.GenshinRoleId),
        new("starrail", "Honkai: Star Rail", c => c.StarRailRoleId),
        new("zenless", "Zenless Zone Zero", c => c.ZenlessRoleId)
    ];

    private readonly FluxerClient _client;
    private readonly HoyoverseApiClient _apiClient;
    private readonly HoyoverseRepository _repository;

    public HoyoverseNotificationService(
        FluxerClient client,
        HoyoverseApiClient apiClient,
        HoyoverseRepository repository)
    {
        _client = client;
        _apiClient = apiClient;
        _repository = repository;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Give the gateway a short moment to finish connecting before the first notification pass.
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await CheckAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HoYoverse notification check failed: {ex.GetType().Name}");
            }

            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        var config = await _repository.GetConfigAsync();
        if (config is null)
            return;

        foreach (var game in Games)
        {
            await CheckCodesAsync(config, game, cancellationToken);
            await CheckEventsAsync(config, game, cancellationToken);
        }

        await _repository.CleanupEventWarningsAsync(DateTimeOffset.UtcNow.AddDays(-7));
    }

    private async Task CheckCodesAsync(
        HoyoverseNotificationConfig config,
        GameDefinition game,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<HoyoverseCode> currentCodes;
        try
        {
            currentCodes = await _apiClient.GetActiveCodesAsync(game.Slug, cancellationToken);
        }
        catch (Exception ex)
        {
            // A failed/invalid fetch must never wipe the stored snapshot.
            Console.WriteLine($"HoYoverse code fetch failed for {game.Slug}: {ex.GetType().Name}");
            return;
        }

        bool initialized = await _repository.IsCodeStateInitializedAsync(game.Slug);
        var storedCodes = await _repository.GetCodesAsync(game.Slug);
        var currentSet = currentCodes.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (initialized)
        {
            foreach (var code in currentCodes.Where(x => !storedCodes.Contains(x.Code)))
            {
                string rewards = code.Rewards.Count > 0
                    ? $"\nRewards: {string.Join(", ", code.Rewards)}"
                    : string.Empty;

                await _client.Rest.SendMessageAsync(
                    config.ChannelId,
                    $"<@&{game.RoleSelector(config)}> **New {game.DisplayName} redemption code:** `{code.Code}`{rewards}");
            }
        }

        // The table is a snapshot of currently active codes, not a permanent history.
        await _repository.ReplaceCodesAsync(game.Slug, currentSet);
    }

    private async Task CheckEventsAsync(
        HoyoverseNotificationConfig config,
        GameDefinition game,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<HoyoverseEvent> events;
        try
        {
            events = await _apiClient.GetEventsAsync(game.Slug, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"HoYoverse calendar fetch failed for {game.Slug}: {ex.GetType().Name}");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var gameEvent in events)
        {
            if (gameEvent.EndTime <= now)
                continue;

            var warningStart = gameEvent.EndTime - EventWarningLeadTime;
            if (now < warningStart)
                continue;

            if (await _repository.WasEventWarningSentAsync(game.Slug, gameEvent.EventId, gameEvent.EndTime))
                continue;

            var remaining = gameEvent.EndTime - now;
            string remainingText = remaining.TotalDays >= 1
                ? $"{Math.Ceiling(remaining.TotalDays):0} day(s)"
                : $"{Math.Max(1, Math.Ceiling(remaining.TotalHours)):0} hour(s)";

            await _client.Rest.SendMessageAsync(
                config.ChannelId,
                $"<@&{game.RoleSelector(config)}> **{gameEvent.Name}** in {game.DisplayName} ends in about {remainingText}. " +
                $"Make sure you finish it and claim your rewards before it ends.");

            await _repository.MarkEventWarningSentAsync(game.Slug, gameEvent.EventId, gameEvent.EndTime);
        }
    }

    private sealed record GameDefinition(
        string Slug,
        string DisplayName,
        Func<HoyoverseNotificationConfig, ulong> RoleSelector);
}
