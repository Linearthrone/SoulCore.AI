using System.Collections.Concurrent;

namespace SoulCore.Inference;

/// <summary>
/// Thread-safe in-process session history. Keyed by the client
/// <c>chat.send</c> <c>sessionId</c> (or a WS-connection fallback). Bounded by
/// <see cref="MaxMessages"/> so context windows stay finite.
/// </summary>
public sealed class ChatSessionHistoryStore : IChatSessionHistoryStore
{
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _sessions =
        new(StringComparer.Ordinal);

    /// <summary>Hard cap on messages retained per session (oldest trimmed first).</summary>
    public int MaxMessages { get; }

    public ChatSessionHistoryStore(int maxMessages = 40)
    {
        if (maxMessages < 2)
            throw new ArgumentOutOfRangeException(nameof(maxMessages), "Must retain at least 2 messages.");
        MaxMessages = maxMessages;
    }

    /// <inheritdoc />
    public IReadOnlyList<ChatMessage> GetMessages(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return Array.Empty<ChatMessage>();

        if (!_sessions.TryGetValue(sessionId.Trim(), out var list))
            return Array.Empty<ChatMessage>();

        lock (list)
            return list.ToArray();
    }

    /// <inheritdoc />
    public void AppendTurn(string sessionId, IReadOnlyList<ChatMessage> turnMessages)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return;
        if (turnMessages is null || turnMessages.Count == 0)
            return;

        var key = sessionId.Trim();
        var list = _sessions.GetOrAdd(key, _ => new List<ChatMessage>());

        lock (list)
        {
            foreach (var m in turnMessages)
            {
                if (m is null) continue;
                list.Add(m);
            }

            while (list.Count > MaxMessages)
                list.RemoveAt(0);
        }
    }

    /// <inheritdoc />
    public void Clear(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return;
        _sessions.TryRemove(sessionId.Trim(), out _);
    }
}
