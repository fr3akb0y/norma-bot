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
            await _httpClient.GetFromJsonAsync<ApiResponse>(
                $"{_baseUrl}/stella/events?lang=EN",
                cancellationToken);

        if (response is null)
            return [];

        // For reminders we only care about events that are
        // currently active or scheduled to start soon.
        return response.Current
            .Concat(response.Upcoming)
            .Where(x => x.EndTime > DateTimeOffset.UtcNow)
            .Select(x => new GameEvent(
                x.Id.ToString(),
                x.Title,
                x.StartTime,
                x.EndTime))
            .ToList();
    }

    private sealed class ApiResponse
    {
        [JsonPropertyName("current")]
        public List<ApiEvent> Current { get; set; } = [];

        [JsonPropertyName("upcoming")]
        public List<ApiEvent> Upcoming { get; set; } = [];

        [JsonPropertyName("ended")]
        public List<ApiEvent> Ended { get; set; } = [];
    }

    private sealed class ApiEvent
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("startTime")]
        public DateTimeOffset StartTime { get; set; }

        [JsonPropertyName("endTime")]
        public DateTimeOffset EndTime { get; set; }
    }
}