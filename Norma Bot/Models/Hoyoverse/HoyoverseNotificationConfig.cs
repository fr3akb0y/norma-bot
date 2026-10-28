namespace Norma_Bot.Models.Hoyoverse;

public sealed record HoyoverseNotificationConfig(
    ulong GuildId,

    ulong GenshinChannelId,
    ulong StarRailChannelId,
    ulong ZenlessChannelId,

    ulong GenshinRoleId,
    ulong StarRailRoleId,
    ulong ZenlessRoleId);
