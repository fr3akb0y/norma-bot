using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Norma_Bot.Models.GameNotifications;

namespace Norma_Bot.Services.GameNotifications.Providers;

public sealed class StellaSoraProvider : IGameProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public string Slug => "stella";
    public string DisplayName => "Stella Sora";

    public bool SupportsCodes => false;
    public bool SupportsEvents => true;

    public StellaSoraProvider(
        HttpClient httpClient,
        string baseUrl)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public Task<IReadOnlyList<GameCode>> GetCodesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<GameCode>>([]);
    }

    public async Task<IReadOnlyList<GameEvent>> GetEventsAsync(
        CancellationToken cancellationToken = default)
    {
        var response =
            await _httpClient.GetFromJsonAsync<List<ApiEvent>>(
                $"{_baseUrl}/stella/events?lang=EN",
                cancellationToken);

        if (response is null)
            return [];

        return response
            .Where(x => x.EndTime > 0)
            .Select(x => new GameEvent(
                x.Id.ToString(),
                x.Name,
                DateTimeOffset.FromUnixTimeSeconds(x.StartTime),
                DateTimeOffset.FromUnixTimeSeconds(x.EndTime)))
            .ToList();
    }

    private sealed class ApiEvent
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("start_time")]
        public long StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public long EndTime { get; set; }
    }
}