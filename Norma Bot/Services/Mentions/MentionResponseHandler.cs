using Fluxer.Net;
using Fluxer.Net.Gateway;

namespace Norma_Bot.Services.Mentions;

public static class MentionResponseHandler
{
    public static async Task HandleAsync(
        FluxerClient client,
        SocketMessage message)
    {
        var currentUser = client.Gateway.CurrentUser;

        if (currentUser is null)
            return;

        if (message.Author == null || message.Author.IsBot)
            return;

        if (string.IsNullOrWhiteSpace(message.Content))
            return;

        if (message.Content.StartsWith("!"))
            return;

        bool mentionsNorma =
            message.Mentions?.Any(x => x.Id == currentUser.Id) == true;

        if (!mentionsNorma)
            return;

        string content = message.Content
            .Replace($"<@{currentUser.Id}>", "")
            .Replace($"<@!{currentUser.Id}>", "")
            .Trim();

        if (string.IsNullOrWhiteSpace(content))
        {
            await SendRandomAsync(
                client,
                message.ChannelId,
                MentionResponses.EmptyPingResponses);

            return;
        }

        foreach (var group in MentionResponses.Groups)
        {
            bool matched = group.Keywords.Any(
                keyword => content.Contains(
                    keyword,
                    StringComparison.OrdinalIgnoreCase));

            if (!matched)
                continue;

            await SendRandomAsync(
                client,
                message.ChannelId,
                group.Responses);

            return;
        }
    }

    private static async Task SendRandomAsync(
        FluxerClient client,
        ulong channelId,
        string[] responses)
    {
        if (responses.Length == 0)
            return;

        string response =
            responses[Random.Shared.Next(responses.Length)];

        await client.Rest.SendMessageAsync(
            channelId,
            response);
    }
}