using Norma_Bot.Models.GameNotifications;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Norma_Bot.Services.GameNotifications.Providers;

public sealed class GameScheduleProvider : IGameProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _route;

    public string Slug { get; }
    public string DisplayName { get; }

    public bool SupportsCodes => false;
    public bool SupportsEvents => true;

    public GameScheduleProvider(
        HttpClient httpClient,
        string baseUrl,
        string route,
        string slug,
        string displayName)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _route = route.TrimStart('/');
        Slug = slug;
        DisplayName = displayName;
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
                $"{_baseUrl}/{_route}",
                cancellationToken);

        if (response?.Data is null)
            return [];

        var result = new List<GameEvent>();

        foreach (var item in response.Data)
        {
            if (!TryParseTime(item.StartTime, out var start))
                continue;

            if (!TryParseTime(item.EndTime, out var end))
                continue;

            result.Add(new GameEvent(
                item.Id,
                item.Title,
                start,
                end));
        }

        return result;
    }

    private static bool TryParseTime(
        string value,
        out DateTimeOffset result)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out result);
    }

    private sealed class ApiResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("data")]
        public List<ApiEvent> Data { get; set; } = [];
    }

    private sealed class ApiEvent
    {
        [JsonPropertyName("id")]
        public JsonElement RawId { get; set; }

        [JsonIgnore]
        public string Id => RawId.ToString();

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("start_time")]
        public string StartTime { get; set; } = string.Empty;

        [JsonPropertyName("end_time")]
        public string EndTime { get; set; } = string.Empty;
    }
}