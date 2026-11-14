namespace Norma_Bot.Models.GameNotifications;

public sealed record GameNotificationConfig(
    ulong GuildId,
    string Game,
    ulong ChannelId,
    ulong RoleId);

public sealed record GameCode(
    string Code,
    IReadOnlyList<string> Rewards);

public sealed record GameEvent(
    string EventId,
    string Name,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime);