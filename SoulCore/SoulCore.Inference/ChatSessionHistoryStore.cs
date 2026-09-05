using System.Collections.Concurrent;

namespace SoulCore.Inference;

/// <summary>
/// Thread-safe in-process session history. Keyed by the client
/// <c>chat.send</c> <c>sessionId</c> (or a WS-connection fallback). Bounded by
/// <see cref="MaxMessages"/> so context windows stay finite.
/// Uses a fixed-capacity ring buffer per session — O(1) append/trim with
/// copy-on-read snapshots (no <c>RemoveAt(0)</c> loop).
/// </summary>
public sealed class ChatSessionHistoryStore : IChatSessionHistoryStore
{
    private readonly ConcurrentDictionary<string, SessionRing> _sessions =
        new(StringComparer.Ordinal);

    /// <summary>Hard cap on messages retained per session (oldest overwritten first).</summary>
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

        if (!_sessions.TryGetValue(sessionId.Trim(), out var ring))
            return Array.Empty<ChatMessage>();

        return ring.Snapshot();
    }

    /// <inheritdoc />
    public void AppendTurn(string sessionId, IReadOnlyList<ChatMessage> turnMessages)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return;
        if (turnMessages is null || turnMessages.Count == 0)
            return;

        var key = sessionId.Trim();
        var ring = _sessions.GetOrAdd(key, _ => new SessionRing(MaxMessages));
        ring.Append(turnMessages);
    }

    /// <inheritdoc />
    public void Clear(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return;
        _sessions.TryRemove(sessionId.Trim(), out _);
    }

    /// <summary>Fixed-capacity ring buffer — oldest slot overwritten when full.</summary>
    private sealed class SessionRing
    {
        private readonly ChatMessage[] _buffer;
        private int _head;
        private int _count;
        private readonly object _lock = new();

        public SessionRing(int capacity) => _buffer = new ChatMessage[capacity];

        public void Append(IReadOnlyList<ChatMessage> turnMessages)
        {
            lock (_lock)
            {
                foreach (var m in turnMessages)
                {
                    if (m is null)
                        continue;

                    if (_count < _buffer.Length)
                    {
                        _buffer[(_head + _count) % _buffer.Length] = m;
                        _count++;
                    }
                    else
                    {
                        _buffer[_head] = m;
                        _head = (_head + 1) % _buffer.Length;
                    }
                }
            }
        }

        public ChatMessage[] Snapshot()
        {
            lock (_lock)
            {
                if (_count == 0)
                    return Array.Empty<ChatMessage>();

                var result = new ChatMessage[_count];
                for (var i = 0; i < _count; i++)
                    result[i] = _buffer[(_head + i) % _buffer.Length];
                return result;
            }
        }
    }
}
