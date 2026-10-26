namespace Norma_Bot.Models;

public sealed record ReactionRoleMapping(
    ulong GuildId,
    ulong ChannelId,
    ulong MessageId,
    string EmojiKey,
    string EmojiValue,
    ulong RoleId);
