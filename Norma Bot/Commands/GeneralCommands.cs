using Fluxer.Net;
using Fluxer.Net.Commands;
using System.Net.Http.Headers;
using Newtonsoft.Json;

public class GeneralCommands : ModuleBase
{
    #region Attachment Upload Classes
    public sealed class AttachmentUploadRequest
    {
        [JsonProperty("attachments")]
        public List<AttachmentUploadRequestItem> Attachments { get; set; } = [];
    }

    public sealed class AttachmentUploadRequestItem
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("filename")]
        public string Filename { get; set; } = string.Empty;

        [JsonProperty("file_size")]
        public long FileSize { get; set; }

        [JsonProperty("content_type")]
        public string ContentType { get; set; } = string.Empty;
    }

    public sealed class AttachmentUploadResponse
    {
        [JsonProperty("attachments")]
        public List<AttachmentUploadResponseItem> Attachments { get; set; } = [];
    }

    public sealed class AttachmentUploadResponseItem
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("filename")]
        public string Filename { get; set; } = string.Empty;

        [JsonProperty("upload_filename")]
        public string UploadFilename { get; set; } = string.Empty;

        [JsonProperty("file_size")]
        public long FileSize { get; set; }

        [JsonProperty("content_type")]
        public string ContentType { get; set; } = string.Empty;

        [JsonProperty("upload_mode")]
        public string UploadMode { get; set; } = string.Empty;

        [JsonProperty("upload_url")]
        public string? UploadUrl { get; set; }
    }

    public sealed class PreUploadedAttachment
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("filename")]
        public string Filename { get; set; } = string.Empty;

        [JsonProperty("upload_filename")]
        public string UploadFilename { get; set; } = string.Empty;

        [JsonProperty("file_size")]
        public long FileSize { get; set; }

        [JsonProperty("content_type")]
        public string ContentType { get; set; } = string.Empty;

        [JsonProperty("flags")]
        public int Flags { get; set; }
    }

    public sealed class CreateMessageRequest
    {
        [JsonProperty("attachments")]
        public List<PreUploadedAttachment> Attachments { get; set; } = [];
    }
    #endregion

    [Command("help")]
    public async Task HelpAsync()
    {
        string helpMessage = """
            Here are all available commands:
            `!help`
            `!bonk @someone`
            `!sex`
            """;
    
        if (IsGuildAdmin())
        {
            helpMessage += """
            
            Admin commands:
            `!setuproles #roles TEXT`
            `!addreactionrole MESSAGE_ID EMOJI @Role`
            `!setuphoyo #genshin #starrail #zenless @genshin @starrail @zenless`
            `!checkhoyo` <- Does the hourly check manually to look for ending events and new codes
            """;
        }
    
        await ReplyAsync(helpMessage);
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
            "Images",
            "SexCommand",
            fileName);

        if (!File.Exists(filePath))
        {
            Console.WriteLine(
                $"Random picture could not be found: {filePath}");

            await ReplyAsync(
                "No! I won't have sex with you right now.");

            return;
        }

        try
        {
            string contentType = Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };

            var fileInfo = new FileInfo(filePath);

            var uploadRequest = new AttachmentUploadRequest
            {
                Attachments =
                [
                    new AttachmentUploadRequestItem
                {
                    Id = 0,
                    Filename = fileName,
                    FileSize = fileInfo.Length,
                    ContentType = contentType
                }
                ]
            };

            var uploadResponse =
                await Context.Client.Rest
                    .UploadAttachmentsAsync<
                        AttachmentUploadRequest,
                        AttachmentUploadResponse>(
                            Context.Channel.Id,
                            uploadRequest);

            var upload = uploadResponse.Attachments.Single();

            if (upload.UploadMode != "singlepart" ||
                string.IsNullOrWhiteSpace(upload.UploadUrl))
            {
                throw new InvalidOperationException(
                    "Fluxer did not return a singlepart upload URL.");
            }

            using var uploadClient = new HttpClient();

            await using var fileStream = File.OpenRead(filePath);

            using var uploadContent =
                new StreamContent(fileStream);

            uploadContent.Headers.ContentType =
                new MediaTypeHeaderValue(contentType);

            using var uploadResult =
                await uploadClient.PutAsync(
                    upload.UploadUrl,
                    uploadContent);

            uploadResult.EnsureSuccessStatusCode();

            var messageRequest = new CreateMessageRequest
            {
                Attachments =
                [
                    new PreUploadedAttachment
                {
                    Id = upload.Id,
                    Filename = upload.Filename,
                    UploadFilename = upload.UploadFilename,
                    FileSize = upload.FileSize,
                    ContentType = upload.ContentType,

                    // IS_SPOILER = 1 << 3
                    Flags = 8
                }
                ]
            };

            await Context.Client.Rest.SendRequestAsync<object, CreateMessageRequest>(
                HttpMethod.Post,
                $"/channels/{Context.Channel.Id}/messages",
                messageRequest);
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

    private bool IsGuildAdmin()
    {
        if (Context.Guild == null || Context.Member == null)
            return false;

        if (Context.Guild.OwnerId == Context.User.Id)
            return true;

        return Context.Member.HasPermission(GuildPermission.Administrator);
    }
}
