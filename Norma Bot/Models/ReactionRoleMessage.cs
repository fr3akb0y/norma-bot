namespace Norma_Bot.Models;

public sealed record ReactionRoleMessage(
    ulong GuildId,
    ulong ChannelId,
    ulong MessageId);
