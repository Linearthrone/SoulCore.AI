using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Email;

/// <summary>
/// Mutable email account credentials for Presence Settings (desktop + companion).
/// Seeded from <see cref="EmailOptions"/>; runtime overrides persist under LocalAppData.
/// Never logs passwords.
/// </summary>
public interface IEmailAccountStore
{
    IReadOnlyList<EmailAccountOptions> ListAccounts();

    EmailAccountOptions? Get(string accountId);

    /// <summary>Upsert one account. Empty password in patch keeps the existing password.</summary>
    EmailAccountOptions Upsert(EmailAccountWriteRequest request);

    object ToPublicDto(EmailAccountOptions account);
}

public sealed class EmailAccountWriteRequest
{
    public string Id { get; set; } = "";
    public string? Role { get; set; }
    public string? DisplayName { get; set; }
    public string? Address { get; set; }
    public string? ImapHost { get; set; }
    public int? ImapPort { get; set; }
    public bool? ImapUseSsl { get; set; }
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public bool? SmtpUseSsl { get; set; }
    public string? Username { get; set; }
    /// <summary>When null or whitespace, existing password is kept.</summary>
    public string? Password { get; set; }
    public bool? Enabled { get; set; }
}

public sealed class EmailAccountStore : IEmailAccountStore
{
    private readonly object _gate = new();
    private readonly ILogger<EmailAccountStore> _logger;
    private readonly string _runtimePath;
    private List<EmailAccountOptions> _accounts;

    public EmailAccountStore(IOptions<EmailOptions> options, ILogger<EmailAccountStore> logger)
        : this(options, logger, runtimePath: null)
    {
    }

    internal EmailAccountStore(IOptions<EmailOptions> options, ILogger<EmailAccountStore> logger, string? runtimePath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _runtimePath = string.IsNullOrWhiteSpace(runtimePath)
            ? DefaultRuntimePath()
            : runtimePath.Trim();

        var seed = options.Value?.Accounts?
            .Where(a => a is not null && !string.IsNullOrWhiteSpace(a.ResolveId()))
            .Select(Clone)
            .ToList()
            ?? new List<EmailAccountOptions>();

        _accounts = seed;
        TryLoadRuntimeOverlay();
        EnsureCanonicalSlots();
    }

    public static string DefaultRuntimePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoulCore",
            "email-accounts.runtime.json");

    public IReadOnlyList<EmailAccountOptions> ListAccounts()
    {
        lock (_gate)
            return _accounts.Select(Clone).ToList();
    }

    public EmailAccountOptions? Get(string accountId)
    {
        lock (_gate)
        {
            var hit = FindLocked(accountId);
            return hit is null ? null : Clone(hit);
        }
    }

    public EmailAccountOptions Upsert(EmailAccountWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = (request.Id ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("account id required", nameof(request));

        lock (_gate)
        {
            var existing = FindLocked(id);
            if (existing is null)
            {
                existing = new EmailAccountOptions
                {
                    Id = id,
                    Role = string.IsNullOrWhiteSpace(request.Role) ? id : request.Role!.Trim(),
                    Enabled = true
                };
                _accounts.Add(existing);
            }

            if (request.Role is not null)
                existing.Role = request.Role.Trim();
            if (request.DisplayName is not null)
                existing.DisplayName = request.DisplayName.Trim();
            if (request.Address is not null)
                existing.Address = request.Address.Trim();
            if (request.ImapHost is not null)
                existing.ImapHost = string.IsNullOrWhiteSpace(request.ImapHost)
                    ? EmailOptions.DefaultImapHost
                    : request.ImapHost.Trim();
            if (request.ImapPort is { } imapPort && imapPort > 0)
                existing.ImapPort = imapPort;
            if (request.ImapUseSsl is { } imapSsl)
                existing.ImapUseSsl = imapSsl;
            if (request.SmtpHost is not null)
                existing.SmtpHost = string.IsNullOrWhiteSpace(request.SmtpHost)
                    ? EmailOptions.DefaultSmtpHost
                    : request.SmtpHost.Trim();
            if (request.SmtpPort is { } smtpPort && smtpPort > 0)
                existing.SmtpPort = smtpPort;
            if (request.SmtpUseSsl is { } smtpSsl)
                existing.SmtpUseSsl = smtpSsl;
            if (request.Username is not null)
                existing.Username = request.Username.Trim();
            if (!string.IsNullOrWhiteSpace(request.Password))
                existing.Password = request.Password.Trim();
            if (request.Enabled is { } enabled)
                existing.Enabled = enabled;

            PersistLocked();
            _logger.LogInformation("Email account '{Id}' upserted (hasPassword={HasPassword})", existing.ResolveId(), existing.HasPassword);
            return Clone(existing);
        }
    }

    public object ToPublicDto(EmailAccountOptions account) => new
    {
        id = account.ResolveId(),
        role = account.ResolveRole(),
        displayName = account.DisplayName,
        address = account.Address,
        imapHost = account.ImapHost,
        imapPort = account.ImapPort,
        imapUseSsl = account.ImapUseSsl,
        smtpHost = account.SmtpHost,
        smtpPort = account.SmtpPort,
        smtpUseSsl = account.SmtpUseSsl,
        username = account.ResolveUsername(),
        enabled = account.Enabled,
        hasPassword = account.HasPassword,
        isConfigured = account.IsConfigured
    };

    private EmailAccountOptions? FindLocked(string accountId)
    {
        var key = (accountId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
            return null;
        return _accounts.FirstOrDefault(a =>
            string.Equals(a.ResolveId(), key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(a.ResolveRole(), key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(a.Address, key, StringComparison.OrdinalIgnoreCase));
    }

    private void EnsureCanonicalSlots()
    {
        lock (_gate)
        {
            foreach (var id in new[] { EmailOptions.RoleVictoria, EmailOptions.RolePersonal, EmailOptions.RoleBusiness })
            {
                if (FindLocked(id) is not null)
                    continue;
                _accounts.Add(new EmailAccountOptions
                {
                    Id = id,
                    Role = id,
                    DisplayName = id switch
                    {
                        EmailOptions.RoleVictoria => "Victoria",
                        EmailOptions.RolePersonal => "Kurt personal",
                        EmailOptions.RoleBusiness => "Kurt business",
                        _ => id
                    },
                    ImapHost = EmailOptions.DefaultImapHost,
                    ImapPort = EmailOptions.DefaultImapPort,
                    ImapUseSsl = true,
                    SmtpHost = EmailOptions.DefaultSmtpHost,
                    SmtpPort = EmailOptions.DefaultSmtpPort,
                    Enabled = true,
                    Password = ""
                });
            }
        }
    }

    private void TryLoadRuntimeOverlay()
    {
        try
        {
            if (!File.Exists(_runtimePath))
                return;
            var json = File.ReadAllText(_runtimePath);
            var rows = JsonSerializer.Deserialize<List<EmailAccountRuntimeRow>>(json);
            if (rows is null || rows.Count == 0)
                return;

            lock (_gate)
            {
                foreach (var row in rows)
                {
                    if (row is null || string.IsNullOrWhiteSpace(row.Id))
                        continue;
                    var existing = FindLocked(row.Id);
                    if (existing is null)
                    {
                        existing = new EmailAccountOptions { Id = row.Id.Trim() };
                        _accounts.Add(existing);
                    }

                    if (row.Role is not null) existing.Role = row.Role;
                    if (row.DisplayName is not null) existing.DisplayName = row.DisplayName;
                    if (row.Address is not null) existing.Address = row.Address;
                    if (row.ImapHost is not null) existing.ImapHost = row.ImapHost;
                    if (row.ImapPort is { } ip && ip > 0) existing.ImapPort = ip;
                    if (row.ImapUseSsl is { } iSsl) existing.ImapUseSsl = iSsl;
                    if (row.SmtpHost is not null) existing.SmtpHost = row.SmtpHost;
                    if (row.SmtpPort is { } sp && sp > 0) existing.SmtpPort = sp;
                    if (row.SmtpUseSsl is { } sSsl) existing.SmtpUseSsl = sSsl;
                    if (row.Username is not null) existing.Username = row.Username;
                    if (!string.IsNullOrWhiteSpace(row.Password)) existing.Password = row.Password;
                    if (row.Enabled is { } en) existing.Enabled = en;
                }
            }

            _logger.LogInformation("Loaded email account overrides from runtime store ({Count} rows)", rows.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load email runtime store; using config seed only");
        }
    }

    private void PersistLocked()
    {
        try
        {
            var dir = Path.GetDirectoryName(_runtimePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var rows = _accounts.Select(a => new EmailAccountRuntimeRow
            {
                Id = a.ResolveId(),
                Role = a.Role,
                DisplayName = a.DisplayName,
                Address = a.Address,
                ImapHost = a.ImapHost,
                ImapPort = a.ImapPort,
                ImapUseSsl = a.ImapUseSsl,
                SmtpHost = a.SmtpHost,
                SmtpPort = a.SmtpPort,
                SmtpUseSsl = a.SmtpUseSsl,
                Username = a.Username,
                Password = a.Password,
                Enabled = a.Enabled
            }).ToList();

            var json = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_runtimePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist email runtime store");
        }
    }

    private static EmailAccountOptions Clone(EmailAccountOptions src) => new()
    {
        Id = src.Id,
        Role = src.Role,
        DisplayName = src.DisplayName,
        Address = src.Address,
        ImapHost = src.ImapHost,
        ImapPort = src.ImapPort,
        ImapUseSsl = src.ImapUseSsl,
        SmtpHost = src.SmtpHost,
        SmtpPort = src.SmtpPort,
        SmtpUseSsl = src.SmtpUseSsl,
        Username = src.Username,
        Password = src.Password,
        Enabled = src.Enabled
    };

    private sealed class EmailAccountRuntimeRow
    {
        public string Id { get; set; } = "";
        public string? Role { get; set; }
        public string? DisplayName { get; set; }
        public string? Address { get; set; }
        public string? ImapHost { get; set; }
        public int? ImapPort { get; set; }
        public bool? ImapUseSsl { get; set; }
        public string? SmtpHost { get; set; }
        public int? SmtpPort { get; set; }
        public bool? SmtpUseSsl { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool? Enabled { get; set; }
    }
}
