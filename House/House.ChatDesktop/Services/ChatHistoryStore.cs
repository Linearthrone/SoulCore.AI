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

    public IReadOnlyList<ChatMessage> LoadRecent(string conversationId, int limit = DefaultLimit)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("Transcript conversation id is required.", nameof(conversationId));
        limit = Math.Clamp(limit, 1, 1000);

        return LoadRows(conversationId, limit, visibleOnly: true);
    }

    /// <summary>
    /// Every stored row for the persona, including ones an archive hide mark skips.
    /// Archive does not delete rows.
    /// </summary>
    public IReadOnlyList<ChatMessage> LoadIncludingHidden(string conversationId, int limit = DefaultLimit)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return LoadRows(conversationId, limit, visibleOnly: false);
    }

    /// <summary>How many rows are stored for this transcript, including hidden ones.</summary>
    public int CountStored(string conversationId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RequireConversation(conversationId);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Messages WHERE ConversationId = $cid;";
        cmd.Parameters.AddWithValue("$cid", conversationId);
        return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Latest visible text for a role, or null when none remains after the hide mark.
    /// Blank rows are skipped. Role is the stored value (<c>user</c> or <c>assistant</c>).
    /// </summary>
    public string? LatestVisibleText(string conversationId, string role)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RequireConversation(conversationId);
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role is required.", nameof(role));

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT Text
            FROM Messages
            WHERE ConversationId = $cid
              AND Role = $role
              AND rowid > $mark
            ORDER BY AtUtc DESC, rowid DESC;
            """;
        cmd.Parameters.AddWithValue("$cid", conversationId);
        cmd.Parameters.AddWithValue("$role", role);
        cmd.Parameters.AddWithValue("$mark", VisibleMark(conversationId));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var text = reader.GetString(0);
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return null;
    }

    /// <summary>
    /// Personas with no hide mark stay listed. After a mark, the row comes back only when a newer message exists.
    /// </summary>
    public bool IsThreadListed(string conversationId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RequireConversation(conversationId);
        if (ReadHideMark(conversationId) is not long mark)
            return true;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT 1
            FROM Messages
            WHERE ConversationId = $cid AND rowid > $mark
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$cid", conversationId);
        cmd.Parameters.AddWithValue("$mark", mark);
        using var reader = cmd.ExecuteReader();
        return reader.Read();
    }

    /// <summary>
    /// Record a hide mark at the latest row id for this transcript. Rows at or before that id stay stored and are skipped on load.
    /// An empty transcript records 0 so a later insert (rowid starts at 1) can show again.
    /// </summary>
    public void HideThroughLatest(string conversationId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RequireConversation(conversationId);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO ThreadHideMarks (ConversationId, MarkRowId)
            VALUES (
                $cid,
                COALESCE((SELECT MAX(rowid) FROM Messages WHERE ConversationId = $cid), 0))
            ON CONFLICT(ConversationId) DO UPDATE SET
                MarkRowId = MAX(ThreadHideMarks.MarkRowId, excluded.MarkRowId);
            """;
        cmd.Parameters.AddWithValue("$cid", conversationId);
        cmd.ExecuteNonQuery();
    }

    private IReadOnlyList<ChatMessage> LoadRows(string conversationId, int limit, bool visibleOnly)
    {
        RequireConversation(conversationId);
        limit = Math.Clamp(limit, 1, 1000);
        // A missing mark uses 0: SQLite rowids start at 1, so every stored row stays visible.
        // Including hidden rows uses -1 so the same predicate still matches rowid 0.
        var mark = visibleOnly ? VisibleMark(conversationId) : -1L;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT Id, Role, Text, AtUtc, FrameId, MediaPath, MediaId
            FROM Messages
            WHERE ConversationId = $cid
              AND rowid > $mark
            ORDER BY AtUtc DESC, rowid DESC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$cid", conversationId);
        cmd.Parameters.AddWithValue("$limit", limit);
        cmd.Parameters.AddWithValue("$mark", mark);

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

    private long VisibleMark(string conversationId) => ReadHideMark(conversationId) ?? 0L;

    private long? ReadHideMark(string conversationId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT MarkRowId FROM ThreadHideMarks WHERE ConversationId = $cid;";
        cmd.Parameters.AddWithValue("$cid", conversationId);
        var value = cmd.ExecuteScalar();
        if (value is null or DBNull)
            return null;
        return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void RequireConversation(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("Transcript conversation id is required.", nameof(conversationId));
    }

    public void Save(ChatMessage message, string conversationId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("Transcript conversation id is required.", nameof(conversationId));
        if (string.Equals(conversationId, PresenceConversationId, StringComparison.Ordinal))
            throw new ArgumentException(
                "presence-local is not a persona transcript. Key rows with ChatTranscriptKey.ForPersona.",
                nameof(conversationId));

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
            CREATE TABLE IF NOT EXISTS ThreadHideMarks (
                ConversationId TEXT PRIMARY KEY NOT NULL,
                MarkRowId INTEGER NOT NULL
            );
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
