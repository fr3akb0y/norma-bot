using Norma_Bot.Models;
using Npgsql;
using System.Collections.Concurrent;

namespace Norma_Bot.Data;

public sealed class ReactionRoleRepository
{
    private readonly string? _connectionString;
    private readonly ConcurrentDictionary<ulong, ReactionRoleMessage> _memoryMessages = new();
    private readonly ConcurrentDictionary<(ulong MessageId, string EmojiKey), ReactionRoleMapping> _memoryMappings = new();

    public ReactionRoleRepository(string? connectionString)
    {
        _connectionString = string.IsNullOrWhiteSpace(connectionString)
            ? null
            : NormalizeConnectionString(connectionString);
    }

    public bool IsPersistent => _connectionString is not null;

    public async Task InitializeAsync()
    {
        if (_connectionString is null)
            return;

        const string sql = """
            CREATE TABLE IF NOT EXISTS reaction_role_messages
            (
                guild_id   TEXT NOT NULL,
                channel_id TEXT NOT NULL,
                message_id TEXT NOT NULL PRIMARY KEY
            );

            CREATE TABLE IF NOT EXISTS reaction_roles
            (
                message_id  TEXT NOT NULL,
                emoji_key   TEXT NOT NULL,
                emoji_value TEXT NOT NULL,
                role_id     TEXT NOT NULL,
                PRIMARY KEY (message_id, emoji_key),
                CONSTRAINT fk_reaction_role_message
                    FOREIGN KEY (message_id)
                    REFERENCES reaction_role_messages(message_id)
                    ON DELETE CASCADE
            );
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task AddMessageAsync(ReactionRoleMessage message)
    {
        if (_connectionString is null)
        {
            _memoryMessages[message.MessageId] = message;
            return;
        }

        const string sql = """
            INSERT INTO reaction_role_messages (guild_id, channel_id, message_id)
            VALUES (@guildId, @channelId, @messageId)
            ON CONFLICT (message_id) DO UPDATE SET
                guild_id = EXCLUDED.guild_id,
                channel_id = EXCLUDED.channel_id;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("guildId", message.GuildId.ToString());
        command.Parameters.AddWithValue("channelId", message.ChannelId.ToString());
        command.Parameters.AddWithValue("messageId", message.MessageId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    public async Task<ReactionRoleMessage?> GetMessageAsync(ulong guildId, ulong messageId)
    {
        if (_connectionString is null)
        {
            return _memoryMessages.TryGetValue(messageId, out var message) && message.GuildId == guildId
                ? message
                : null;
        }

        const string sql = """
            SELECT guild_id, channel_id, message_id
            FROM reaction_role_messages
            WHERE guild_id = @guildId AND message_id = @messageId;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("guildId", guildId.ToString());
        command.Parameters.AddWithValue("messageId", messageId.ToString());

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new ReactionRoleMessage(
            ulong.Parse(reader.GetString(0)),
            ulong.Parse(reader.GetString(1)),
            ulong.Parse(reader.GetString(2)));
    }

    public async Task AddOrUpdateMappingAsync(ReactionRoleMapping mapping)
    {
        if (_connectionString is null)
        {
            _memoryMappings[(mapping.MessageId, mapping.EmojiKey)] = mapping;
            return;
        }

        const string sql = """
            INSERT INTO reaction_roles (message_id, emoji_key, emoji_value, role_id)
            VALUES (@messageId, @emojiKey, @emojiValue, @roleId)
            ON CONFLICT (message_id, emoji_key) DO UPDATE SET
                emoji_value = EXCLUDED.emoji_value,
                role_id = EXCLUDED.role_id;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("messageId", mapping.MessageId.ToString());
        command.Parameters.AddWithValue("emojiKey", mapping.EmojiKey);
        command.Parameters.AddWithValue("emojiValue", mapping.EmojiValue);
        command.Parameters.AddWithValue("roleId", mapping.RoleId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    public async Task<ReactionRoleMapping?> GetMappingAsync(
        ulong guildId,
        ulong messageId,
        string emojiKey)
    {
        if (_connectionString is null)
        {
            return _memoryMappings.TryGetValue((messageId, emojiKey), out var mapping) && mapping.GuildId == guildId
                ? mapping
                : null;
        }

        const string sql = """
            SELECT m.guild_id, m.channel_id, m.message_id,
                   r.emoji_key, r.emoji_value, r.role_id
            FROM reaction_roles r
            INNER JOIN reaction_role_messages m ON m.message_id = r.message_id
            WHERE m.guild_id = @guildId
              AND m.message_id = @messageId
              AND r.emoji_key = @emojiKey;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("guildId", guildId.ToString());
        command.Parameters.AddWithValue("messageId", messageId.ToString());
        command.Parameters.AddWithValue("emojiKey", emojiKey);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new ReactionRoleMapping(
            ulong.Parse(reader.GetString(0)),
            ulong.Parse(reader.GetString(1)),
            ulong.Parse(reader.GetString(2)),
            reader.GetString(3),
            reader.GetString(4),
            ulong.Parse(reader.GetString(5)));
    }

    private static string NormalizeConnectionString(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
        {
            return value;
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty
        };

        return builder.ConnectionString;
    }
}
