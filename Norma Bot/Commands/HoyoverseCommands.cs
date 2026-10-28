using Fluxer.Net;
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
        string genshinChannel,
        string starRailChannel,
        string zenlessChannel,
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

        // Channels
        if (!TryGetChannelId(genshinChannel, out var genshinChannelId) ||
            !TryGetChannelId(starRailChannel, out var starRailChannelId) ||
            !TryGetChannelId(zenlessChannel, out var zenlessChannelId))
        {
            await ReplyAsync("Please mention all three announcement channels.");
            return;
        }

        if (!TryGetRoleId(genshinRole, out var genshinRoleId) ||
            !TryGetRoleId(starRailRole, out var starRailRoleId) ||
            !TryGetRoleId(zenlessRole, out var zenlessRoleId))
        {
            await ReplyAsync("Please mention all three game roles.");
            return;
        }

        await BotServices.HoyoverseRepository.SaveConfigAsync(
            new HoyoverseNotificationConfig(
                Context.Guild.Id,

                genshinChannelId,
                starRailChannelId,
                zenlessChannelId,

                genshinRoleId,
                starRailRoleId,
                zenlessRoleId
            ));

        await ReplyAsync(
            "HoYoverse notifications configured.\n" +
            $"Genshin Impact → <#{genshinChannelId}>\n" +
            $"Honkai: Star Rail → <#{starRailChannelId}>\n" +
            $"Zenless Zone Zero → <#{zenlessChannelId}>"
        );
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
