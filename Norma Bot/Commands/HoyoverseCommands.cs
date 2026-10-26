using Fluxer.Net.Commands;
using Norma_Bot.Models.Hoyoverse;
using Norma_Bot.Services;
using System.Text.RegularExpressions;

public class HoyoverseCommands : ModuleBase
{
    private static readonly Regex ChannelMentionRegex = new(
        @"^<#(?<id>\d+)>$",
        RegexOptions.Compiled);

    private static readonly Regex RoleMentionRegex = new(
        @"^<@&(?<id>\d+)>$",
        RegexOptions.Compiled);

    [Command("setuphoyo")]
    public async Task SetupHoyoverseAsync(
        string channel,
        string genshinRole,
        string starRailRole,
        string zenlessRole)
    {
        if (!await EnsureAdministratorAsync())
            return;

        if (Context.Guild is null)
        {
            await ReplyAsync("This command can only be used inside a server.");
            return;
        }

        if (!TryParseMentionId(ChannelMentionRegex, channel, out var channelId) ||
            !Context.Guild.Channels.ContainsKey(channelId))
        {
            await ReplyAsync("Please mention a channel from this server.");
            return;
        }

        if (!TryParseMentionId(RoleMentionRegex, genshinRole, out var genshinRoleId) ||
            !Context.Guild.Roles.ContainsKey(genshinRoleId) ||
            !TryParseMentionId(RoleMentionRegex, starRailRole, out var starRailRoleId) ||
            !Context.Guild.Roles.ContainsKey(starRailRoleId) ||
            !TryParseMentionId(RoleMentionRegex, zenlessRole, out var zenlessRoleId) ||
            !Context.Guild.Roles.ContainsKey(zenlessRoleId))
        {
            await ReplyAsync("Please mention all three roles. Example: `!setuphoyo #notifications @Genshin @StarRail @Zenless`");
            return;
        }

        await BotServices.HoyoverseRepository.SaveConfigAsync(
            new HoyoverseNotificationConfig(
                Context.Guild.Id,
                channelId,
                genshinRoleId,
                starRailRoleId,
                zenlessRoleId));

        await ReplyAsync(
            "HoYoverse notifications configured. The bot will check events and codes automatically. " +
            "The first code fetch only seeds the current active-code snapshot, so existing codes are not spammed immediately.");
    }

    [Command("checkhoyo")]
    public async Task CheckHoyoverseAsync()
    {
        if (!await EnsureAdministratorAsync())
            return;

        await ReplyAsync("Running a HoYoverse check now.");
        await BotServices.HoyoverseNotifications.CheckAsync();
        await ReplyAsync("HoYoverse check finished.");
    }

    private async Task<bool> EnsureAdministratorAsync()
    {
        if (Context.Member?.GuildPermissions.Administrator == true)
            return true;

        await ReplyAsync("You need the Administrator permission to use this command.");
        return false;
    }

    private static bool TryParseMentionId(Regex regex, string value, out ulong id)
    {
        id = 0;
        var match = regex.Match(value.Trim());
        return match.Success && ulong.TryParse(match.Groups["id"].Value, out id);
    }
}
