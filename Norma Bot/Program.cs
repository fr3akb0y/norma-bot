using Fluxer.Net;
using Fluxer.Net.Commands;
using Microsoft.Extensions.Configuration;
using Norma_Bot.Data;
using Norma_Bot.Services;
using Norma_Bot.Services.GameNotifications;
using Norma_Bot.Services.GameNotifications.Providers;
using Norma_Bot.Services.Mentions;
using Norma_Bot.Services.Hoyoverse;
using Serilog;
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

        var gatewayLogger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console()
            .CreateLogger();

        var restLogger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console()
            .CreateLogger();

        var fluxerConfig = new FluxerConfig
        {
            ApiBaseUrl = "https://cunnyverse.moe/api/v{v}/",
            GatewayUrl = "wss://cunnyverse.moe/gateway/?v=1&encoding=json",
            MediaUrl = "https://cunnyverse.moe/media",
            WebUrl = "https://cunnyverse.moe",
            StaticUrl = "https://cunnyverse.moe",
            InviteUrl = "https://cunnyverse.moe/invite",
            GiftUrl = "https://cunnyverse.moe/gift",
            AdminUrl = "https://cunnyverse.moe/admin",
            Version = 1,

            GatewaySerilog = gatewayLogger,
            RestSerilog = restLogger
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

        #region Register Reminder/Code Services

        BotServices.HoyoverseNotifications = new HoyoverseNotificationService(
            _client,
            hoyoverseApiClient,
            hoyoverseRepository);

        _commands = new CommandService();
        await _commands.AddModulesAsync(Assembly.GetExecutingAssembly());

        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        string gameScheduleBaseUrl =
            configuration["Games:GameScheduleBaseUrl"]
            ?? string.Empty;

        string stellaSoraBaseUrl =
            configuration["Games:StellaSoraBaseUrl"]
            ?? string.Empty;

        var gameProviders = new List<IGameProvider>
        {
            new OpenGachaCodesProvider(
                httpClient,
                "wuwa",
                "Wuthering Waves"),

            new OpenGachaCodesProvider(
                httpClient,
                "endfield",
                "Arknights: Endfield")
        };

        if (!string.IsNullOrWhiteSpace(gameScheduleBaseUrl))
        {
            gameProviders.Add(
                new GameScheduleProvider(
                    httpClient,
                    gameScheduleBaseUrl,
                    "mc",
                    "wuwa",
                    "Wuthering Waves"));

            gameProviders.Add(
                new GameScheduleProvider(
                    httpClient,
                    gameScheduleBaseUrl,
                    "endfield",
                    "endfield",
                    "Arknights: Endfield"));

            gameProviders.Add(
                new GameScheduleProvider(
                    httpClient,
                    gameScheduleBaseUrl,
                    "ba",
                    "bluearchive",
                    "Blue Archive"));

            gameProviders.Add(
                new GameScheduleProvider(
                    httpClient,
                    gameScheduleBaseUrl,
                    "nikke",
                    "nikke",
                    "Goddess of Victory: NIKKE"));
        }

        if (!string.IsNullOrWhiteSpace(stellaSoraBaseUrl))
        {
            gameProviders.Add(
                new StellaSoraProvider(
                    httpClient,
                    stellaSoraBaseUrl));
        }

        var gameProviderRegistry =
            new GameProviderRegistry(gameProviders);

        var gameNotificationRepository =
            new GameNotificationRepository(databaseConnectionString);

        await gameNotificationRepository.InitializeAsync();

        BotServices.GameNotificationRepository =
            gameNotificationRepository;

        BotServices.GameNotifications =
            new GameNotificationService(
                _client,
                gameProviderRegistry,
                gameNotificationRepository);

        #endregion

        _client.Gateway.MessageCreated += async message =>
        {
            await MentionResponseHandler.HandleAsync(
                _client,
                message);

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

        _client.Gateway.Connected += () =>
        {
            Console.WriteLine("Gateway connected.");
        };

        _client.Gateway.Ready += data =>
        {
            Console.WriteLine("Gateway READY.");
            Console.WriteLine($"Guilds loaded: {_client.Gateway.Guilds.Count}");
        };

        _client.Gateway.Disconnected += () =>
        {
            Console.WriteLine("Gateway disconnected.");
        };

        Console.WriteLine($"Gateway URL: {fluxerConfig.GatewayUrl}");
        Console.WriteLine($"API URL: {fluxerConfig.ApiBaseUrl}");

        await _client.StartAsync();

        Console.WriteLine("StartAsync completed.");

        _ = BotServices.HoyoverseNotifications.RunAsync();
        _ = BotServices.GameNotifications.RunAsync();

        await Task.Delay(-1);
    }
}
