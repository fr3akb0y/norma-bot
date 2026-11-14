using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Norma_Bot.Models.GameNotifications;

namespace Norma_Bot.Services.GameNotifications.Providers;

public sealed class OpenGachaCodesProvider : IGameProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _slug;

    public string Slug => _slug;
    public string DisplayName { get; }

    public bool SupportsCodes => true;
    public bool SupportsEvents => false;

    public OpenGachaCodesProvider(
        HttpClient httpClient,
        string slug,
        string displayName)
    {
        _httpClient = httpClient;
        _slug = slug;
        DisplayName = displayName;
    }

    public async Task<IReadOnlyList<GameCode>> GetCodesAsync(
        CancellationToken cancellationToken = default)
    {
        var response =
            await _httpClient.GetFromJsonAsync<List<ApiCode>>(
                $"https://api.ennead.cc/codes/{_slug}",
                cancellationToken);

        return response?
            .Select(x => new GameCode(
                x.Code,
                x.Rewards))
            .ToList()
            ?? [];
    }

    public Task<IReadOnlyList<GameEvent>> GetEventsAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<GameEvent>>([]);
    }

    private sealed class ApiCode
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("rewards")]
        public List<string> Rewards { get; set; } = [];
    }
}