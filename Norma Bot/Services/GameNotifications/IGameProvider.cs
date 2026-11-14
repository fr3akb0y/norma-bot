namespace Norma_Bot.Services.GameNotifications;

using Norma_Bot.Models.GameNotifications;

public interface IGameProvider
{
    string Slug { get; }
    string DisplayName { get; }

    bool SupportsCodes { get; }
    bool SupportsEvents { get; }

    Task<IReadOnlyList<GameCode>> GetCodesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameEvent>> GetEventsAsync(
        CancellationToken cancellationToken = default);
}