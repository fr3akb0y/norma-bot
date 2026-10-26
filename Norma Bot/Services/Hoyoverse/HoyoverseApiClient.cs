using Norma_Bot.Models.Hoyoverse;
using System.Net.Http.Json;
using System.Text.Json;

namespace Norma_Bot.Services.Hoyoverse;

public sealed class HoyoverseApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public HoyoverseApiClient(HttpClient httpClient, string baseUrl)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<IReadOnlyList<HoyoverseEvent>> GetEventsAsync(string game, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"{_baseUrl}/{game}/calendar?lang=en-us", cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("events", out var eventsElement) ||
            eventsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("HoYoverse calendar response did not contain an events array.");
        }

        var events = new List<HoyoverseEvent>();
        foreach (var item in eventsElement.EnumerateArray())
        {
            if (!TryReadStringOrNumber(item, "id", out var id) ||
                !item.TryGetProperty("name", out var nameElement) ||
                nameElement.ValueKind != JsonValueKind.String ||
                !item.TryGetProperty("end_time", out var endElement) ||
                !endElement.TryGetInt64(out var endUnix))
            {
                continue;
            }

            var name = nameElement.GetString();
            if (string.IsNullOrWhiteSpace(name) || endUnix <= 0)
                continue;

            events.Add(new HoyoverseEvent(
                game,
                id,
                name,
                DateTimeOffset.FromUnixTimeSeconds(endUnix)));
        }

        return events;
    }

    public async Task<IReadOnlyList<HoyoverseCode>> GetActiveCodesAsync(string game, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"{_baseUrl}/{game}/codes", cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("active", out var activeElement) ||
            activeElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("HoYoverse code response did not contain an active array.");
        }

        var codes = new List<HoyoverseCode>();
        foreach (var item in activeElement.EnumerateArray())
        {
            if (!item.TryGetProperty("code", out var codeElement) || codeElement.ValueKind != JsonValueKind.String)
                continue;

            var code = codeElement.GetString();
            if (string.IsNullOrWhiteSpace(code))
                continue;

            var rewards = new List<string>();
            if (item.TryGetProperty("rewards", out var rewardsElement) && rewardsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var reward in rewardsElement.EnumerateArray())
                {
                    if (reward.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(reward.GetString()))
                        rewards.Add(reward.GetString()!);
                }
            }

            codes.Add(new HoyoverseCode(game, code, rewards));
        }

        return codes;
    }

    private static bool TryReadStringOrNumber(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property))
            return false;

        if (property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return value.Length > 0;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            value = property.GetRawText();
            return value.Length > 0;
        }

        return false;
    }
}
