using Fluxer.Net;
using Norma_Bot.Data;
using Norma_Bot.Models;
using System.Text.RegularExpressions;

namespace Norma_Bot.Services;

public sealed class ReactionRoleService
{
    private static readonly Regex CustomEmojiRegex = new(
        @"^<a?:(?<name>[^:>]+):(?<id>\d+)>$",
        RegexOptions.Compiled);

    private readonly FluxerClient _client;
    private readonly ReactionRoleRepository _repository;

    public ReactionRoleService(FluxerClient client, ReactionRoleRepository repository)
    {
        _client = client;
        _repository = repository;
    }

    public void RegisterGatewayHandlers()
    {
        _client.Gateway.MessageReactionAdded += async reaction =>
            await HandleReactionAsync(reaction, addRole: true);

        _client.Gateway.MessageReactionRemoved += async reaction =>
            await HandleReactionAsync(reaction, addRole: false);
    }

    public async Task<ReactionRoleMessage> CreateMessageAsync(
        ulong guildId,
        ulong channelId,
        string content)
    {
        var message = await _client.Rest.SendMessageAsync(channelId, content);

        var reactionRoleMessage = new ReactionRoleMessage(
            guildId,
            channelId,
            message.Id);

        await _repository.AddMessageAsync(reactionRoleMessage);
        return reactionRoleMessage;
    }

    public async Task<bool> AddMappingAsync(
        ulong guildId,
        ulong messageId,
        string emojiInput,
        ulong roleId)
    {
        var message = await _repository.GetMessageAsync(guildId, messageId);
        if (message is null)
            return false;

        var emoji = ParseEmoji(emojiInput);

        // Add the bot's own reaction first so users can simply click it.
        await _client.Rest.AddReactionAsync(
            message.ChannelId,
            message.MessageId,
            emoji.ApiValue);

        await _repository.AddOrUpdateMappingAsync(new ReactionRoleMapping(
            message.GuildId,
            message.ChannelId,
            message.MessageId,
            emoji.Key,
            emoji.ApiValue,
            roleId));

        return true;
    }

    private async Task HandleReactionAsync(dynamic reaction, bool addRole)
    {
        try
        {
            if (!TryReadReaction(reaction, out ulong guildId, out ulong messageId,
                    out ulong userId, out string emojiKey))
            {
                return;
            }

            // Never try to assign a role because of the bot's own setup reaction.
            if (_client.Gateway.CurrentUser?.Id == userId)
                return;

            var mapping = await _repository.GetMappingAsync(guildId, messageId, emojiKey);
            if (mapping is null)
                return;

            if (addRole)
            {
                await _client.Rest.AddMemberRoleAsync(
                    mapping.GuildId,
                    userId,
                    mapping.RoleId);
            }
            else
            {
                await _client.Rest.RemoveMemberRoleAsync(
                    mapping.GuildId,
                    userId,
                    mapping.RoleId);
            }
        }
        catch (Exception ex)
        {
            // Deliberately do not log member IDs, message contents or other user data.
            Console.WriteLine($"Reaction role update failed: {ex.GetType().Name}");
        }
    }

    private static (string Key, string ApiValue) ParseEmoji(string input)
    {
        input = input.Trim();
        var match = CustomEmojiRegex.Match(input);

        if (!match.Success)
            return ($"unicode:{input}", input);

        string id = match.Groups["id"].Value;
        string name = match.Groups["name"].Value;

        // The REST reaction endpoints use the name:id form for custom emojis.
        return ($"custom:{id}", $"{name}:{id}");
    }

    private static bool TryReadReaction(
        dynamic reaction,
        out ulong guildId,
        out ulong messageId,
        out ulong userId,
        out string emojiKey)
    {
        guildId = 0;
        messageId = 0;
        userId = 0;
        emojiKey = string.Empty;

        try
        {
            object? rawGuildId = reaction.GuildId;
            if (rawGuildId is null)
                return false;

            guildId = Convert.ToUInt64(rawGuildId);
            messageId = Convert.ToUInt64(reaction.MessageId);
            userId = Convert.ToUInt64(reaction.UserId);
        }
        catch
        {
            return false;
        }

        // Fluxer.Net reaction gateway models have used both a nested Emoji object and
        // flattened EmojiId/EmojiName fields across versions, so support both shapes.
        try
        {
            object? rawEmojiId = reaction.Emoji.Id;
            string? emojiName = Convert.ToString(reaction.Emoji.Name);

            if (rawEmojiId is not null && Convert.ToUInt64(rawEmojiId) != 0)
            {
                emojiKey = $"custom:{Convert.ToUInt64(rawEmojiId)}";
                return true;
            }

            if (!string.IsNullOrWhiteSpace(emojiName))
            {
                emojiKey = $"unicode:{emojiName}";
                return true;
            }
        }
        catch
        {
            // Fall through to the flattened gateway representation below.
        }

        try
        {
            object? rawEmojiId = reaction.EmojiId;
            string? emojiName = Convert.ToString(reaction.EmojiName);

            if (rawEmojiId is not null && Convert.ToUInt64(rawEmojiId) != 0)
            {
                emojiKey = $"custom:{Convert.ToUInt64(rawEmojiId)}";
                return true;
            }

            if (!string.IsNullOrWhiteSpace(emojiName))
            {
                emojiKey = $"unicode:{emojiName}";
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
