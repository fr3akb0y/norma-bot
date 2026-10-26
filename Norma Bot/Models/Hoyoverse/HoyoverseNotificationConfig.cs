namespace Norma_Bot.Models.Hoyoverse;

public sealed record HoyoverseNotificationConfig(
    ulong GuildId,
    ulong ChannelId,
    ulong GenshinRoleId,
    ulong StarRailRoleId,
    ulong ZenlessRoleId);
