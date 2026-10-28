using Fluxer.Net;
using Fluxer.Net.Commands;
using Norma_Bot.Services;
using System.Text.RegularExpressions;

public class RoleCommands : ModuleBase
{
    private static readonly Regex ChannelMentionRegex = new(
        @"^<#(?<id>\d+)>$",
        RegexOptions.Compiled);

    private static readonly Regex RoleMentionRegex = new(
        @"^<@&(?<id>\d+)>$",
        RegexOptions.Compiled);

    [Command("setuproles")]
    public async Task SetupRolesAsync(
        string channel,
        [Remainder] string message)
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
            await ReplyAsync("Please mention a channel from this server. Example: `!setuproles #roles Choose your roles!`");
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            await ReplyAsync("Please provide the message the bot should post.");
            return;
        }

        message = message.Replace("\\n", "\n");

        var createdMessage = await BotServices.ReactionRoles.CreateMessageAsync(
            Context.Guild.Id,
            channelId,
            message);

        await ReplyAsync(
            $"Reaction-role message created. Message ID: `{createdMessage.MessageId}`\n" +
            $"Add a role reaction with: `!addreactionrole {createdMessage.MessageId} [EMOJI] @Role`");
    }

    [Command("addreactionrole")]
    public async Task AddReactionRoleAsync(
        ulong messageId,
        string emoji,
        string role)
    {
        if (!await EnsureAdministratorAsync())
            return;

        if (Context.Guild is null)
        {
            await ReplyAsync("This command can only be used inside a server.");
            return;
        }

        if (!TryParseMentionId(RoleMentionRegex, role, out var roleId) ||
            !Context.Guild.Roles.ContainsKey(roleId))
        {
            await ReplyAsync("Please mention a role from this server. Example: `!addreactionrole 123456 😀 @Gamer`");
            return;
        }

        bool added = await BotServices.ReactionRoles.AddMappingAsync(
            Context.Guild.Id,
            messageId,
            emoji,
            roleId);

        if (!added)
        {
            await ReplyAsync("I don't know that reaction-role message. Create it first with `!setuproles`." );
            return;
        }

        await ReplyAsync("Reaction role added.");
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
}
