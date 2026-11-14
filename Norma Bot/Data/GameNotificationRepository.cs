using Norma_Bot.Models.GameNotifications;
using Npgsql;
using System.Collections.Concurrent;

namespace Norma_Bot.Data;

public sealed class GameNotificationRepository
{
    private readonly string? _connectionString;

    private readonly ConcurrentDictionary<
        (ulong GuildId, string Game),
        GameNotificationConfig> _memoryConfigs = new();

    private readonly ConcurrentDictionary<
        (string Game, string Code),
        byte> _memoryCodes = new();

    private readonly ConcurrentDictionary<
        string,
        byte> _memoryInitializedGames = new();

    private readonly ConcurrentDictionary<
        (string Game, string EventId, long EndUnix),
        byte> _memoryEventWarnings = new();

    public GameNotificationRepository(
        string? connectionString)
    {
        _connectionString =
            string.IsNullOrWhiteSpace(connectionString)
                ? null
                : NormalizeConnectionString(connectionString);
    }

    public bool IsPersistent =>
        _connectionString is not null;

    public async Task InitializeAsync()
    {
        if (_connectionString is null)
            return;

        const string sql = """
            CREATE TABLE IF NOT EXISTS game_notification_config
            (
                guild_id TEXT NOT NULL,
                game TEXT NOT NULL,
                channel_id TEXT NOT NULL,
                role_id TEXT NOT NULL,
                PRIMARY KEY (guild_id, game)
            );

            CREATE TABLE IF NOT EXISTS game_codes
            (
                game TEXT NOT NULL,
                code TEXT NOT NULL,
                PRIMARY KEY (game, code)
            );

            CREATE TABLE IF NOT EXISTS game_code_state
            (
                game TEXT PRIMARY KEY,
                initialized BOOLEAN NOT NULL
            );

            CREATE TABLE IF NOT EXISTS game_event_warnings
            (
                game TEXT NOT NULL,
                event_id TEXT NOT NULL,
                end_unix BIGINT NOT NULL,
                PRIMARY KEY (game, event_id, end_unix)
            );
            """;

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync();

        await using var command =
            new NpgsqlCommand(sql, connection);

        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveConfigAsync(
        GameNotificationConfig config)
    {
        if (_connectionString is null)
        {
            _memoryConfigs[
                (config.GuildId, config.Game)] = config;

            return;
        }

        const string sql = """
            INSERT INTO game_notification_config
                (
                    guild_id,
                    game,
                    channel_id,
                    role_id
                )
            VALUES
                (
                    @guildId,
                    @game,
                    @channelId,
                    @roleId
                )
            ON CONFLICT (guild_id, game)
            DO UPDATE SET
                channel_id = EXCLUDED.channel_id,
                role_id = EXCLUDED.role_id;
            """;

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync();

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "guildId",
            config.GuildId.ToString());

        command.Parameters.AddWithValue(
            "game",
            config.Game);

        command.Parameters.AddWithValue(
            "channelId",
            config.ChannelId.ToString());

        command.Parameters.AddWithValue(
            "roleId",
            config.RoleId.ToString());

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<GameNotificationConfig>>
        GetConfigsAsync(string game)
    {
        if (_connectionString is null)
        {
            return _memoryConfigs
                .Where(x =>
                    x.Key.Game.Equals(
                        game,
                        StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Value)
                .ToList();
        }

        const string sql = """
            SELECT
                guild_id,
                game,
                channel_id,
                role_id
            FROM game_notification_config
            WHERE game = @game;
            """;

        var result =
            new List<GameNotificationConfig>();

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync();

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "game",
            game);

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(
                new GameNotificationConfig(
                    ulong.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    ulong.Parse(reader.GetString(2)),
                    ulong.Parse(reader.GetString(3))));
        }

        return result;
    }

    public async Task<bool>
        IsCodeStateInitializedAsync(string game)
    {
        if (_connectionString is null)
        {
            return _memoryInitializedGames
                .ContainsKey(game);
        }

        const string sql = """
            SELECT initialized
            FROM game_code_state
            WHERE game = @game;
            """;

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync();

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "game",
            game);

        var result =
            await command.ExecuteScalarAsync();

        return result is bool value && value;
    }

    public async Task<HashSet<string>>
        GetCodesAsync(string game)
    {
        if (_connectionString is null)
        {
            return _memoryCodes.Keys
                .Where(x =>
                    x.Game.Equals(
                        game,
                        StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Code)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);
        }

        const string sql = """
            SELECT code
            FROM game_codes
            WHERE game = @game;
            """;

        var result =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync();

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "game",
            game);

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(
                reader.GetString(0));
        }

        return result;
    }

    public async Task ReplaceCodesAsync(
        string game,
        IEnumerable<string> codes)
    {
        var normalized =
            codes
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (_connectionString is null)
        {
            foreach (var key in
                     _memoryCodes.Keys
                         .Where(x =>
                             x.Game.Equals(
                                 game,
                                 StringComparison.OrdinalIgnoreCase))
                         .ToArray())
            {
                _memoryCodes.TryRemove(
                    key,
                    out _);
            }

            foreach (var code in normalized)
            {
                _memoryCodes[(game, code)] = 0;
            }

            _memoryInitializedGames[game] = 0;

            return;
        }

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync();

        await using var transaction =
            await connection.BeginTransactionAsync();

        await using (
            var delete = new NpgsqlCommand(
                """
                DELETE FROM game_codes
                WHERE game = @game;
                """,
                connection,
                transaction))
        {
            delete.Parameters.AddWithValue(
                "game",
                game);

            await delete.ExecuteNonQueryAsync();
        }

        foreach (var code in normalized)
        {
            await using var insert =
                new NpgsqlCommand(
                    """
                    INSERT INTO game_codes
                        (game, code)
                    VALUES
                        (@game, @code)
                    ON CONFLICT DO NOTHING;
                    """,
                    connection,
                    transaction);

            insert.Parameters.AddWithValue(
                "game",
                game);

            insert.Parameters.AddWithValue(
                "code",
                code);

            await insert.ExecuteNonQueryAsync();
        }

        await using (
            var state = new NpgsqlCommand(
                """
                INSERT INTO game_code_state
                    (game, initialized)
                VALUES
                    (@game, TRUE)
                ON CONFLICT (game)
                DO UPDATE SET
                    initialized = TRUE;
                """,
                connection,
                transaction))
        {
            state.Parameters.AddWithValue(
                "game",
                game);

            await state.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task<bool>
        WasEventWarningSentAsync(
            string game,
            string eventId,
            DateTimeOffset endTime)
    {
        long endUnix =
            endTime.ToUnixTimeSeconds();

        if (_connectionString is null)
        {
            return _memoryEventWarnings
                .ContainsKey(
                    (
                        game,
                        eventId,
                        endUnix
                    ));
        }

        const string sql = """
            SELECT 1
            FROM game_event_warnings
            WHERE game = @game
              AND event_id = @eventId
              AND end_unix = @endUnix;
            """;

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync();

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "game",
            game);

        command.Parameters.AddWithValue(
            "eventId",
            eventId);

        command.Parameters.AddWithValue(
            "endUnix",
            endUnix);

        return
            await command.ExecuteScalarAsync()
            is not null;
    }

    public async Task
        MarkEventWarningSentAsync(
            string game,
            string eventId,
            DateTimeOffset endTime)
    {
        long endUnix =
            endTime.ToUnixTimeSeconds();

        if (_connectionString is null)
        {
            _memoryEventWarnings[
                (
                    game,
                    eventId,
                    endUnix
                )] = 0;

            return;
        }

        const string sql = """
            INSERT INTO game_event_warnings
                (
                    game,
                    event_id,
                    end_unix
                )
            VALUES
                (
                    @game,
                    @eventId,
                    @endUnix
                )
            ON CONFLICT DO NOTHING;
            """;

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync();

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "game",
            game);

        command.Parameters.AddWithValue(
            "eventId",
            eventId);

        command.Parameters.AddWithValue(
            "endUnix",
            endUnix);

        await command.ExecuteNonQueryAsync();
    }

    public async Task
        CleanupEventWarningsAsync(
            DateTimeOffset olderThan)
    {
        long unix =
            olderThan.ToUnixTimeSeconds();

        if (_connectionString is null)
        {
            foreach (var key in
                     _memoryEventWarnings.Keys
                         .Where(x =>
                             x.EndUnix < unix)
                         .ToArray())
            {
                _memoryEventWarnings.TryRemove(
                    key,
                    out _);
            }

            return;
        }

        const string sql = """
            DELETE FROM game_event_warnings
            WHERE end_unix < @endUnix;
            """;

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync();

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "endUnix",
            unix);

        await command.ExecuteNonQueryAsync();
    }

    private static string
        NormalizeConnectionString(string value)
    {
        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out var uri)
            ||
            (uri.Scheme != "postgres"
             && uri.Scheme != "postgresql"))
        {
            return value;
        }

        var userInfo =
            uri.UserInfo.Split(':', 2);

        var builder =
            new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.IsDefaultPort
                    ? 5432
                    : uri.Port,

                Database =
                    Uri.UnescapeDataString(
                        uri.AbsolutePath
                            .TrimStart('/')),

                Username =
                    Uri.UnescapeDataString(
                        userInfo[0]),

                Password =
                    userInfo.Length > 1
                        ? Uri.UnescapeDataString(
                            userInfo[1])
                        : string.Empty
            };

        return builder.ConnectionString;
    }
}