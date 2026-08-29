namespace SoulCore.Config;

/// <summary>
/// Multi-account IMAP/SMTP mailboxes Victoria can manage. Credentials come from
/// env (<c>SOULCORE_Email__Accounts__N__Password</c>) — never commit secrets.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public const string RoleVictoria = "victoria";
    public const string RolePersonal = "personal";
    public const string RoleBusiness = "business";

    public const string DefaultImapHost = "imap.gmail.com";
    public const int DefaultImapPort = 993;
    public const string DefaultSmtpHost = "smtp.gmail.com";
    public const int DefaultSmtpPort = 587;

    /// <summary>Named mailboxes (victoria / personal / business). Empty = tools list none.</summary>
    public List<EmailAccountOptions> Accounts { get; set; } = new();
}

/// <summary>One IMAP+SMTP identity. Password is env-only.</summary>
public sealed class EmailAccountOptions
{
    /// <summary>Stable id the tools take as <c>account</c> (e.g. victoria, personal, business).</summary>
    public string Id { get; set; } = "";

    /// <summary>victoria | personal | business — who this mailbox belongs to.</summary>
    public string Role { get; set; } = "";

    public string DisplayName { get; set; } = "";
    public string Address { get; set; } = "";

    public string ImapHost { get; set; } = EmailOptions.DefaultImapHost;
    public int ImapPort { get; set; } = EmailOptions.DefaultImapPort;
    public bool ImapUseSsl { get; set; } = true;

    public string SmtpHost { get; set; } = EmailOptions.DefaultSmtpHost;
    public int SmtpPort { get; set; } = EmailOptions.DefaultSmtpPort;

    /// <summary>
    /// When true, SMTP uses implicit TLS (465). When false (default), STARTTLS on 587.
    /// </summary>
    public bool SmtpUseSsl { get; set; }

    public string Username { get; set; } = "";

    /// <summary>App password or mailbox password. Bind from env only.</summary>
    public string Password { get; set; } = "";

    public bool Enabled { get; set; } = true;

    public bool HasPassword => !string.IsNullOrWhiteSpace(Password);

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(ResolveId())
        && !string.IsNullOrWhiteSpace(Address)
        && !string.IsNullOrWhiteSpace(ImapHost)
        && !string.IsNullOrWhiteSpace(SmtpHost)
        && HasPassword;

    public string ResolveId()
    {
        if (!string.IsNullOrWhiteSpace(Id))
            return Id.Trim();
        if (!string.IsNullOrWhiteSpace(Role))
            return Role.Trim();
        return (Address ?? string.Empty).Trim();
    }

    public string ResolveRole()
    {
        if (!string.IsNullOrWhiteSpace(Role))
            return Role.Trim().ToLowerInvariant();
        var id = ResolveId().ToLowerInvariant();
        if (id is EmailOptions.RoleVictoria or EmailOptions.RolePersonal or EmailOptions.RoleBusiness)
            return id;
        return id;
    }

    public string ResolveUsername() =>
        string.IsNullOrWhiteSpace(Username) ? (Address ?? string.Empty).Trim() : Username.Trim();
}
