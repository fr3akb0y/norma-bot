using Fluxer.Net;
using Fluxer.Net.Commands;
using Microsoft.Extensions.Configuration;
using Norma_Bot.Data;
using Norma_Bot.Services;
using Norma_Bot.Services.Hoyoverse;
using System.Reflection;

public class Program
{
    private static FluxerClient _client = null!;
    private static CommandService _commands = null!;

    public static async Task Main()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        string token = configuration["Fluxer:Token"]
            ?? throw new Exception("Fluxer token is missing.");

        string? databaseConnectionString = configuration["Database:ConnectionString"]
            ?? configuration["DATABASE_URL"];

        if (string.IsNullOrWhiteSpace(databaseConnectionString))
        {
            Console.WriteLine("No database configured. Using in-memory test storage; data will reset when the bot stops.");
        }

        var fluxerConfig = new FluxerConfig
        {
            ApiBaseUrl = "https://cunnyverse.moe/api/v{v}/",
            GatewayUrl = "wss://cunnyverse.moe/gateway",
            MediaUrl = "https://cunnyverse.moe/media",
            WebUrl = "https://cunnyverse.moe",
            StaticUrl = "https://cunnyverse.moe",
            InviteUrl = "https://cunnyverse.moe/invite",
            GiftUrl = "https://cunnyverse.moe/gift",
            AdminUrl = "https://cunnyverse.moe/admin",
            Version = 1
        };

        _client = new FluxerClient(
            "Bot " + token,
            fluxerConfig
        );

        var reactionRoleRepository = new ReactionRoleRepository(databaseConnectionString);
        await reactionRoleRepository.InitializeAsync();

        BotServices.ReactionRoles = new ReactionRoleService(
            _client,
            reactionRoleRepository);
        BotServices.ReactionRoles.RegisterGatewayHandlers();

        var hoyoverseRepository = new HoyoverseRepository(databaseConnectionString);
        await hoyoverseRepository.InitializeAsync();
        BotServices.HoyoverseRepository = hoyoverseRepository;

        string hoyoverseApiBaseUrl = configuration["Hoyoverse:ApiBaseUrl"]
            ?? "https://api.ennead.cc/mihoyo";

        var hoyoverseApiClient = new HoyoverseApiClient(
            new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
            hoyoverseApiBaseUrl);

        BotServices.HoyoverseNotifications = new HoyoverseNotificationService(
            _client,
            hoyoverseApiClient,
            hoyoverseRepository);

        _commands = new CommandService();
        await _commands.AddModulesAsync(Assembly.GetExecutingAssembly());

        _client.Gateway.MessageCreated += async message =>
        {
            if (message.Author == null || message.Author.IsBot)
                return;

            const string prefix = "!";
            if (message.Content?.StartsWith(prefix) != true)
                return;

            var context = new CommandContext(_client, message);
            var result = await _commands.ExecuteAsync(context, prefix.Length);

            if (!result.IsSuccess)
                Console.WriteLine($"Command failed: {result.ErrorReason}");
        };

        await _client.StartAsync();

        // The current app is a plain console process rather than a Generic Host, so a
        // PeriodicTimer-style background task is enough. If the app is later migrated to
        // Microsoft.Extensions.Hosting, this service can become a BackgroundService.
        _ = BotServices.HoyoverseNotifications.RunAsync();

        await Task.Delay(-1);
    }
}
