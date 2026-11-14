using Fluxer.Net;
using Fluxer.Net.Commands;
using Norma_Bot.Models.GameNotifications;
using Norma_Bot.Services;
using System.Text.RegularExpressions;

public class OpenGachaCommands : ModuleBase
{
    private static readonly Regex ChannelMentionRegex = new(
        @"^<#(?<id>\d+)>$",
        RegexOptions.Compiled);

    private static readonly Regex RoleMentionRegex = new(
        @"^<@&(?<id>\d+)>$",
        RegexOptions.Compiled);

    [Command("setupgame")]
    public async Task SetupGameAsync(
    string game,
    string channelMention,
    string roleMention)
    {
        if (!await EnsureAdministratorAsync())
            return;

        game = game.ToLowerInvariant();

        if (!BotServices.GameNotifications.SupportsGame(game))
        {
            await ReplyAsync($"Unknown or unsupported game: `{game}`");
            return;
        }

        if (!TryGetChannelId(channelMention, out ulong channelId))
        {
            await ReplyAsync("Please provide a valid channel mention.");
            return;
        }

        if (!TryGetRoleId(roleMention, out ulong roleId))
        {
            await ReplyAsync("Please provide a valid role mention.");
            return;
        }

        await BotServices.GameNotificationRepository.SaveConfigAsync(
            new GameNotificationConfig(
                Context.Guild!.Id,
                game,
                channelId,
                roleId));

        await ReplyAsync(
            $"Notifications for `{game}` have been configured.");
    }

    [Command("checkgames")]
    public async Task CheckGamesAsync()
    {
        if (!await EnsureAdministratorAsync())
            return;

        await BotServices.GameNotifications.CheckAsync();

        await ReplyAsync("Game notification check completed.");
    }

    [Command("checkgame")]
    public async Task CheckGameAsync(string game)
    {
        if (!await EnsureAdministratorAsync())
            return;

        await BotServices.GameNotifications.CheckAsync(
            game.ToLowerInvariant());

        await ReplyAsync(
            $"Notification check completed for `{game}`.");
    }

    private async Task<bool> EnsureAdministratorAsync()
    {
        if (!IsGuildAdmin())
        {
            await ReplyAsync(
                "You need the Administrator permission to use this command."
            );
            return false;
        }

        return true;
    }

    private bool IsGuildAdmin()
    {
        if (Context.Guild == null || Context.Member == null)
            return false;

        if (Context.Guild.OwnerId == Context.User.Id)
            return true;

        return Context.Member.HasPermission(GuildPermission.Administrator);
    }

    private static bool TryParseMentionId(Regex regex, string value, out ulong id)
    {
        id = 0;
        var match = regex.Match(value.Trim());
        return match.Success && ulong.TryParse(match.Groups["id"].Value, out id);
    }

    private bool TryGetChannelId(string mention, out ulong channelId)
    {
        channelId = 0;

        return Context.Guild is not null &&
               TryParseMentionId(
                   ChannelMentionRegex,
                   mention,
                   out channelId) &&
               Context.Guild.Channels.ContainsKey(channelId);
    }

    private bool TryGetRoleId(string mention, out ulong roleId)
    {
        roleId = 0;

        return Context.Guild is not null &&
               TryParseMentionId(
                   RoleMentionRegex,
                   mention,
                   out roleId) &&
               Context.Guild.Roles.ContainsKey(roleId);
    }
}
