namespace Norma_Bot.Models.Hoyoverse;

public sealed record HoyoverseEvent(
    string Game,
    string EventId,
    string Name,
    DateTimeOffset EndTime);
