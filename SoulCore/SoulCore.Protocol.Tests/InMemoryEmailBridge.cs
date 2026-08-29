using System.Collections.Concurrent;
using SoulCore.Inference.Tools.Email;

namespace SoulCore.Protocol.Tests;

/// <summary>Recording in-memory mailbox for email tool tests. No IMAP.</summary>
public sealed class InMemoryEmailBridge : IEmailBridge
{
    private readonly ConcurrentDictionary<string, AccountBox> _boxes = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Calls { get; } = new();

    public AccountBox SeedAccount(string id, string role, string address, string displayName = "")
    {
        var box = new AccountBox(id, role, address, displayName);
        _boxes[id] = box;
        return box;
    }

    public IReadOnlyList<EmailAccountInfo> ListAccounts()
    {
        Calls.Add("list_accounts");
        return _boxes.Values
            .Select(b => new EmailAccountInfo(b.Id, b.Role, b.DisplayName, b.Address, Enabled: true, Configured: true))
            .ToList();
    }

    public Task<IReadOnlyList<string>> ListFoldersAsync(string accountId, CancellationToken ct = default)
    {
        Calls.Add($"list_folders:{accountId}");
        var box = Require(accountId);
        return Task.FromResult<IReadOnlyList<string>>(box.Folders.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList());
    }

    public Task<IReadOnlyList<EmailMessageSummary>> ListAsync(
        string accountId,
        string folder,
        int limit,
        bool unreadOnly,
        CancellationToken ct = default)
    {
        Calls.Add($"list:{accountId}:{folder}:{unreadOnly}");
        var box = Require(accountId);
        var dest = Normalize(folder);
        var rows = box.GetFolder(dest)
            .OrderByDescending(m => m.Date)
            .Where(m => !unreadOnly || m.Unread)
            .Take(Math.Max(1, limit))
            .Select(m => m.ToSummary(accountId, dest))
            .ToList();
        return Task.FromResult<IReadOnlyList<EmailMessageSummary>>(rows);
    }

    public Task<EmailMessageDetail?> GetAsync(
        string accountId,
        string uid,
        string? folder,
        CancellationToken ct = default)
    {
        Calls.Add($"get:{accountId}:{uid}");
        var box = Require(accountId);
        var msg = box.Find(uid, folder);
        return Task.FromResult(msg?.ToDetail(accountId));
    }

    public Task<IReadOnlyList<EmailMessageSummary>> SearchAsync(
        string accountId,
        string query,
        string? folder,
        int limit,
        CancellationToken ct = default)
    {
        Calls.Add($"search:{accountId}:{query}");
        var box = Require(accountId);
        var dest = Normalize(folder);
        var q = query.Trim();
        var rows = box.GetFolder(dest)
            .Where(m =>
                m.Subject.Contains(q, StringComparison.OrdinalIgnoreCase)
                || m.From.Contains(q, StringComparison.OrdinalIgnoreCase)
                || m.Body.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.Date)
            .Take(Math.Max(1, limit))
            .Select(m => m.ToSummary(accountId, dest))
            .ToList();
        return Task.FromResult<IReadOnlyList<EmailMessageSummary>>(rows);
    }

    public Task MoveAsync(
        string accountId,
        string uid,
        string destFolder,
        string? sourceFolder,
        CancellationToken ct = default)
    {
        Calls.Add($"move:{accountId}:{uid}:{destFolder}");
        var box = Require(accountId);
        var msg = box.Find(uid, sourceFolder)
            ?? throw new InvalidOperationException($"uid {uid} not found");
        box.Remove(msg);
        msg.Folder = Normalize(destFolder);
        box.Add(msg);
        return Task.CompletedTask;
    }

    public Task MarkAsync(
        string accountId,
        string uid,
        bool? seen,
        bool? flagged,
        string? folder,
        CancellationToken ct = default)
    {
        Calls.Add($"mark:{accountId}:{uid}");
        var box = Require(accountId);
        var msg = box.Find(uid, folder)
            ?? throw new InvalidOperationException($"uid {uid} not found");
        if (seen is not null)
            msg.Unread = !seen.Value;
        if (flagged is not null)
            msg.Flagged = flagged.Value;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(
        string accountId,
        string uid,
        string? folder,
        CancellationToken ct = default)
    {
        Calls.Add($"delete:{accountId}:{uid}");
        var box = Require(accountId);
        var msg = box.Find(uid, folder)
            ?? throw new InvalidOperationException($"uid {uid} not found");
        box.Remove(msg);
        return Task.CompletedTask;
    }

    public Task<string> SendAsync(
        string accountId,
        EmailComposeRequest request,
        CancellationToken ct = default)
    {
        Calls.Add($"send:{accountId}:{string.Join(",", request.To)}:{request.Subject}");
        var box = Require(accountId);
        var id = box.NextUid();
        var stored = new StoredMessage
        {
            Uid = id,
            Folder = "INBOX",
            From = box.Address,
            To = string.Join(", ", request.To),
            Cc = request.Cc is { Count: > 0 } ? string.Join(", ", request.Cc) : "",
            Subject = request.Subject,
            Body = request.Body,
            Date = DateTimeOffset.UtcNow,
            Unread = false,
            Flagged = false,
            MessageId = $"<mem-{id}@test>"
        };
        box.Add(stored);
        return Task.FromResult(stored.MessageId);
    }

    private AccountBox Require(string accountId)
    {
        if (_boxes.TryGetValue(accountId, out var box))
            return box;
        throw new InvalidOperationException($"unknown email account '{accountId}'");
    }

    private static string Normalize(string? folder)
    {
        return EmailToolSupport.NormalizeFolderName(folder);
    }

    public sealed class AccountBox
    {
        private int _next = 100;
        public AccountBox(string id, string role, string address, string displayName)
        {
            Id = id;
            Role = role;
            Address = address;
            DisplayName = displayName;
        }

        public string Id { get; }
        public string Role { get; }
        public string Address { get; }
        public string DisplayName { get; }
        public ConcurrentDictionary<string, List<StoredMessage>> Folders { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string NextUid() => Interlocked.Increment(ref _next).ToString();

        public List<StoredMessage> GetFolder(string folder)
        {
            return Folders.GetOrAdd(folder, _ => new List<StoredMessage>());
        }

        public void Add(StoredMessage msg)
        {
            var list = GetFolder(msg.Folder);
            lock (list)
                list.Add(msg);
        }

        public void Remove(StoredMessage msg)
        {
            foreach (var list in Folders.Values)
            {
                lock (list)
                    list.Remove(msg);
            }
        }

        public StoredMessage? Find(string uid, string? folder)
        {
            if (!string.IsNullOrWhiteSpace(folder))
            {
                var dest = EmailToolSupport.NormalizeFolderName(folder);
                return GetFolder(dest).FirstOrDefault(m => m.Uid == uid);
            }

            foreach (var list in Folders.Values)
            {
                lock (list)
                {
                    var hit = list.FirstOrDefault(m => m.Uid == uid);
                    if (hit is not null)
                        return hit;
                }
            }

            return null;
        }

        public StoredMessage Seed(
            string uid,
            string from,
            string subject,
            string body,
            string folder = "INBOX",
            bool unread = true,
            string? to = null)
        {
            var msg = new StoredMessage
            {
                Uid = uid,
                Folder = EmailToolSupport.NormalizeFolderName(folder),
                From = from,
                To = to ?? Address,
                Subject = subject,
                Body = body,
                Date = DateTimeOffset.Parse("2026-08-29T12:00:00Z"),
                Unread = unread,
                MessageId = $"<seed-{uid}@test>"
            };
            Add(msg);
            return msg;
        }
    }

    public sealed class StoredMessage
    {
        public string Uid { get; set; } = "";
        public string Folder { get; set; } = "INBOX";
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public string Cc { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public DateTimeOffset Date { get; set; }
        public bool Unread { get; set; }
        public bool Flagged { get; set; }
        public string MessageId { get; set; } = "";

        public EmailMessageSummary ToSummary(string accountId, string folder) =>
            new(accountId, Uid, folder, From, To, Subject, Date, Unread, Flagged, Subject);

        public EmailMessageDetail ToDetail(string accountId) =>
            new(accountId, Uid, Folder, From, To, Cc, Subject, Date, Unread, Flagged, Body, MessageId, Array.Empty<string>());
    }
}
