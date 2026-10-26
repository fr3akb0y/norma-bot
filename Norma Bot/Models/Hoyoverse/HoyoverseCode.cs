namespace Norma_Bot.Models.Hoyoverse;

public sealed record HoyoverseCode(
    string Game,
    string Code,
    IReadOnlyList<string> Rewards);
