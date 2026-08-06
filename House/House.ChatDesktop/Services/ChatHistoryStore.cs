using Microsoft.Data.Sqlite;
using House.ChatDesktop.Models;

namespace House.ChatDesktop.Services;

/// <summary>
/// Local SMS-style chat history for Presence (LLMOD pattern: SQLite + hydrate last N).
/// Survives ChatDesktop restarts; not the Host episodic store.
/// </summary>
public sealed class ChatHistoryStore : IDisposable
{
    public const string PresenceConversationId = "presence-local";
    private const int DefaultLimit = 200;

    private readonly SqliteConnection _connection;
    private bool _disposed;

    public ChatHistoryStore(string? dbPath = null)
    {
        var path = dbPath ?? DefaultDbPath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connection = new SqliteConnection($"Data Source={path}");
        _connection.Open();
        EnsureSchema();
    }

    public static string DefaultDbPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HouseVictoria",
            "presence-chat.db");

    public IReadOnlyList<ChatMessage> LoadRecent(
        string conversationId = PresenceConversationId,
        int limit = DefaultLimit)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        limit = Math.Clamp(limit, 1, 1000);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT Id, Role, Text, AtUtc, FrameId, MediaPath, MediaId
            FROM Messages
            WHERE ConversationId = $cid
            ORDER BY AtUtc DESC, RowId DESC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$cid", conversationId);
        cmd.Parameters.AddWithValue("$limit", limit);

        var list = new List<ChatMessage>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new ChatMessage
            {
                Id = reader.GetString(0),
                Role = reader.GetString(1),
                Text = reader.GetString(2),
                At = DateTimeOffset.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
                FrameId = reader.IsDBNull(4) ? null : reader.GetString(4),
                MediaPath = reader.IsDBNull(5) ? null : reader.GetString(5),
                MediaId = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }

        list.Reverse();
        return list;
    }

    public void Save(ChatMessage message, string conversationId = PresenceConversationId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(message.Id))
            message.Id = Guid.NewGuid().ToString("N");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO Messages (Id, ConversationId, Role, Text, AtUtc, FrameId, MediaPath, MediaId)
            VALUES ($id, $cid, $role, $text, $at, $frame, $mediaPath, $mediaId)
            ON CONFLICT(Id) DO UPDATE SET
                Text = excluded.Text,
                AtUtc = excluded.AtUtc,
                FrameId = excluded.FrameId,
                MediaPath = excluded.MediaPath,
                MediaId = excluded.MediaId;
            """;
        cmd.Parameters.AddWithValue("$id", message.Id);
        cmd.Parameters.AddWithValue("$cid", conversationId);
        cmd.Parameters.AddWithValue("$role", message.Role);
        cmd.Parameters.AddWithValue("$text", message.Text ?? string.Empty);
        cmd.Parameters.AddWithValue("$at", message.At.ToUniversalTime().ToString("O"));
        cmd.Parameters.AddWithValue("$frame", (object?)message.FrameId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$mediaPath", (object?)message.MediaPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$mediaId", (object?)message.MediaId ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private void EnsureSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Messages (
                Id TEXT PRIMARY KEY NOT NULL,
                ConversationId TEXT NOT NULL,
                Role TEXT NOT NULL,
                Text TEXT NOT NULL,
                AtUtc TEXT NOT NULL,
                FrameId TEXT NULL,
                MediaPath TEXT NULL,
                MediaId TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_messages_conv_at
                ON Messages (ConversationId, AtUtc DESC);
            """;
        cmd.ExecuteNonQuery();

        EnsureColumn("MediaPath", "TEXT NULL");
        EnsureColumn("MediaId", "TEXT NULL");
    }

    private void EnsureColumn(string name, string decl)
    {
        using var check = _connection.CreateCommand();
        check.CommandText = "PRAGMA table_info(Messages);";
        using var reader = check.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), name, StringComparison.OrdinalIgnoreCase))
                return;
        }

        using var alter = _connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE Messages ADD COLUMN {name} {decl};";
        alter.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _connection.Dispose();
    }
}
