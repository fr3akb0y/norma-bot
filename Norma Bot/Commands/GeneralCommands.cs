using Fluxer.Net.Commands;
using Fluxer.Net.Rest;

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

    private static readonly string SexImagesPath = "Images/SexCommand/";
    private static readonly string[] SexImages =
    [
        "NormaSex1.jpg",
        "NormaSex2.jpg",
        "NormaSex3.jpg",
        "NormaSex4.jpg",
        "NormaSex5.jpg",
        "NormaSex6.jpg",
        "NormaSex7.jpg",
        "NormaSex8.jpg",
        "NormaSex9.jpg",
        "NormaSex10.jpg"
    ];

    [Command("sex", ignoreExtraArgs: true)]
    public async Task SexAsync()
    {
        string fileName =
            SexImages[Random.Shared.Next(SexImages.Length)];

        string filePath = Path.Combine(
            AppContext.BaseDirectory,
            "Images/SexCommand",
            fileName);

        if (!File.Exists(filePath))
        {
            Console.WriteLine(
                $"Random picture could not be found: {filePath}");

            await ReplyAsync(
                "Couldn't find the selected image.");

            return;
        }

        try
        {
            var attachment = new AttachmentRequest(filePath)
            {
                Flags = Fluxer.Net.AttachmentFlag.IsSpoiler
            };

            await ReplyAsync(
                attachments:
                [
                    attachment
                ]);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Failed to send random picture: " +
                $"{ex.GetType().Name}: {ex.Message}");

            await ReplyAsync(
                "Something went wrong while sending the image.");
        }
    }
}