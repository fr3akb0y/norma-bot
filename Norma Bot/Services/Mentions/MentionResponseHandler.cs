using Fluxer.Net;
using Fluxer.Net.Gateway;

namespace Norma_Bot.Services.Mentions;

public static class MentionResponseHandler
{
    public static async Task HandleAsync(
        FluxerClient client,
        SocketMessage message)
    {
        if (message.Author == null || message.Author.IsBot)
            return;

        if (string.IsNullOrWhiteSpace(message.Content))
            return;

        if (message.Content.StartsWith("!"))
            return;

        bool mentionsNorma =
            message.Mentions?.Any(x => x.Id == client.User.Id) == true;

        if (!mentionsNorma)
            return;

        string content = RemoveBotMention(
            message.Content,
            client.User.Id);

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

    private static string RemoveBotMention(
        string content,
        ulong botId)
    {
        return content
            .Replace($"<@{botId}>", "")
            .Replace($"<@!{botId}>", "")
            .Trim();
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