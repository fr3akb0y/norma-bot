namespace Norma_Bot.Services.GameNotifications;

public sealed class GameProviderRegistry
{
    private readonly Dictionary<string, List<IGameProvider>> _providers;

    public GameProviderRegistry(
        IEnumerable<IGameProvider> providers)
    {
        _providers = providers
            .GroupBy(x => x.Slug)
            .ToDictionary(
                x => x.Key,
                x => x.ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IGameProvider> GetProviders(string game)
    {
        return _providers.TryGetValue(game, out var providers)
            ? providers
            : [];
    }

    public IReadOnlyCollection<string> Games =>
        _providers.Keys;
}