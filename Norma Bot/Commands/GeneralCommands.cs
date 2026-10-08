using Fluxer.Net.Commands;

public class GeneralCommands : ModuleBase
{
    [Command("help")]
    public async Task HelpAsync()
    {
        await ReplyAsync("Here are all available commands:\n`!help`\n`!bonk @Someone`");
    }

    [Command("bonk", ignoreExtraArgs: true)]
    public async Task BonkAsync()
    {
        var target = Context.Message.Mentions?.FirstOrDefault();

        if (target == null)
        {
            await ReplyAsync("Please mention someone to bonk. Example: `!bonk @Someone`");
            return;
        }

        if (target.Id == Context.User.Id)
        {
            await ReplyAsync("You can't bonk yourself!");
            return;
        }

        string senderName = Context.Member?.GetCurrentName()
            ?? Context.User.GetCurrentName();

        string targetName = target.GetCurrentName();

        await ReplyAsync($"{senderName} bonked {targetName}!");
    }
}