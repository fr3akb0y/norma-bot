using Fluxer.Net;
using Norma_Bot.Data;
using Norma_Bot.Models.GameNotifications;

namespace Norma_Bot.Services.GameNotifications
{
    public sealed class GameNotificationService
    {
        private static readonly TimeSpan EventWarningLeadTime =
            TimeSpan.FromDays(3);

        private readonly FluxerClient _client;
        private readonly GameProviderRegistry _providers;
        private readonly GameNotificationRepository _repository;

        public GameNotificationService(
            FluxerClient client,
            GameProviderRegistry providers,
            GameNotificationRepository repository)
        {
            _client = client;
            _providers = providers;
            _repository = repository;
        }

        public bool SupportsGame(string game) =>
            _providers.GetProviders(game).Count > 0;

        public async Task CheckAsync(
            CancellationToken cancellationToken = default)
        {
            foreach (var game in _providers.Games)
            {
                await CheckAsync(game, cancellationToken);
            }

            await _repository.CleanupEventWarningsAsync(
                DateTimeOffset.UtcNow.AddDays(-7));
        }

        public async Task CheckAsync(
            string game,
            CancellationToken cancellationToken = default)
        {
            var configs =
                await _repository.GetConfigsAsync(game);

            if (configs.Count == 0)
                return;

            var providers =
                _providers.GetProviders(game);

            foreach (var provider in providers)
            {
                if (provider.SupportsCodes)
                {
                    await CheckCodesAsync(
                        provider,
                        configs,
                        cancellationToken);
                }

                if (provider.SupportsEvents)
                {
                    await CheckEventsAsync(
                        provider,
                        configs,
                        cancellationToken);
                }
            }
        }

        private async Task CheckCodesAsync(
            IGameProvider provider,
            IReadOnlyCollection<GameNotificationConfig> configs,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<GameCode> currentCodes;

            try
            {
                currentCodes =
                    await provider.GetCodesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"{provider.DisplayName} code check failed: " +
                    $"{ex.GetType().Name}: {ex.Message}");

                return;
            }

            bool initialized =
                await _repository.IsCodeStateInitializedAsync(
                    provider.Slug);

            var storedCodes =
                await _repository.GetCodesAsync(provider.Slug);

            if (initialized)
            {
                foreach (var code in currentCodes
                             .Where(x => !storedCodes.Contains(x.Code)))
                {
                    string rewards = code.Rewards.Count > 0
                        ? $"\nRewards: {string.Join(", ", code.Rewards)}"
                        : string.Empty;

                    foreach (var config in configs)
                    {
                        await _client.Rest.SendMessageAsync(
                            config.ChannelId,
                            $"<@&{config.RoleId}> " +
                            $"**New {provider.DisplayName} redemption code:** " +
                            $"`{code.Code}`{rewards}");
                    }
                }
            }

            await _repository.ReplaceCodesAsync(
                provider.Slug,
                currentCodes.Select(x => x.Code));
        }

        private async Task CheckEventsAsync(
            IGameProvider provider,
            IReadOnlyCollection<GameNotificationConfig> configs,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<GameEvent> events;

            try
            {
                events =
                    await provider.GetEventsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"{provider.DisplayName} event check failed: " +
                    $"{ex.GetType().Name}: {ex.Message}");

                return;
            }

            var now = DateTimeOffset.UtcNow;

            foreach (var gameEvent in events)
            {
                if (gameEvent.EndTime <= now)
                    continue;

                if (now < gameEvent.EndTime - EventWarningLeadTime)
                    continue;

                if (await _repository.WasEventWarningSentAsync(
                        provider.Slug,
                        gameEvent.EventId,
                        gameEvent.EndTime))
                {
                    continue;
                }

                TimeSpan remaining =
                    gameEvent.EndTime - now;

                string remainingText =
                    remaining.TotalDays >= 1
                        ? $"{Math.Ceiling(remaining.TotalDays):0} day(s)"
                        : $"{Math.Max(1, Math.Ceiling(remaining.TotalHours)):0} hour(s)";

                foreach (var config in configs)
                {
                    await _client.Rest.SendMessageAsync(
                        config.ChannelId,
                        $"<@&{config.RoleId}> " +
                        $"**{gameEvent.Name}** in {provider.DisplayName} " +
                        $"ends in about {remainingText}. " +
                        $"Make sure you finish it and claim your rewards.");
                }

                await _repository.MarkEventWarningSentAsync(
                    provider.Slug,
                    gameEvent.EventId,
                    gameEvent.EndTime);
            }
        }

        private static readonly TimeSpan PollInterval =
    TimeSpan.FromHours(1);

        public async Task RunAsync(
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(
                TimeSpan.FromSeconds(10),
                cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Game notification check failed: " +
                        $"{ex.GetType().Name}: {ex.Message}");
                }

                await Task.Delay(
                    PollInterval,
                    cancellationToken);
            }
        }
    }
}