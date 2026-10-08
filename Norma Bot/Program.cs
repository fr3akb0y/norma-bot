using Fluxer.Net;
using Fluxer.Net.Commands;
using Microsoft.Extensions.Configuration;
using System.Reflection;

public class Program
{
    private static FluxerClient _client = null!;
    private static CommandService _commands = null!;

    public static async Task Main()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        string token = configuration["Fluxer:Token"]
            ?? throw new Exception("Fluxer token is missing.");

        string apiUrl = configuration["Fluxer:ApiUrl"]
            ?? throw new Exception("Fluxer API URL is missing.");

        _client = new FluxerClient("Bot " + token);

        await _client.LoginAsync(apiUrl);

        _commands = new CommandService();

        await _commands.AddModulesAsync(
            Assembly.GetExecutingAssembly()
        );

        _client.Gateway.MessageCreated += async message =>
        {
            // Ignore bots/system messages
            if (message.Author == null || message.Author.IsBot)
                return;

            const string prefix = "!";

            if (message.Content?.StartsWith(prefix) != true)
                return;

            var context = new CommandContext(_client, message);

            var result = await _commands.ExecuteAsync(
                context,
                prefix.Length
            );

            if (!result.IsSuccess)
            {
                Console.WriteLine(
                    $"Command failed: {result.Error}"
                );
            }
        };

        await _client.StartAsync();

        await Task.Delay(-1);
    }
}