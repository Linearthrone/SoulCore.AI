using House.ChatDesktop.Services;

namespace House.ChatDesktop.Models;

/// <summary>
/// Friendly mailbox picker row for Presence email setup dialog (PROP-17.4: unlimited ids).
/// </summary>
public sealed class EmailMailboxChoice
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public string DefaultDisplayName { get; init; } = "";

    public override string ToString() => Title;

    public static string TitleFor(string id)
    {
        var key = id.Trim();
        if (string.IsNullOrWhiteSpace(key))
            return "Mailbox";
        return key.ToLowerInvariant() switch
        {
            "victoria" => "Victoria's mailbox",
            "personal" => "LinearThrone's personal mail",
            "business" => "LinearThrone's business mail",
            _ => key
        };
    }

    public static string DefaultDisplayNameFor(string id) => id.Trim().ToLowerInvariant() switch
    {
        "victoria" => "Victoria",
        "personal" => "LinearThrone",
        "business" => "LinearThrone",
        _ => ""
    };

    public static string SubtitleFor(string id, EmailAccountSnapshot? account)
    {
        var whose = string.IsNullOrWhiteSpace(id)
            ? "Mailbox"
            : $"Id · {id.Trim()}";

        if (account is null)
            return whose + " · not set up";

        var owner = string.IsNullOrWhiteSpace(account.DisplayName)
            ? (string.IsNullOrWhiteSpace(account.Address) ? "owner unset" : account.Address)
            : account.DisplayName;

        if (!account.Enabled)
            return $"{whose} · {owner} · turned off";

        if (account.IsConfigured)
            return $"{whose} · {owner} · ready";

        if (account.HasPassword && !string.IsNullOrWhiteSpace(account.Address))
            return $"{whose} · {owner} · almost ready";

        return $"{whose} · needs setup";
    }

    /// <summary>
    /// Lists every account Host returned. No hardcoded victoria|personal|business slots.
    /// </summary>
    public static IReadOnlyList<EmailMailboxChoice> BuildList(IReadOnlyList<EmailAccountSnapshot> accounts)
    {
        var list = new List<EmailMailboxChoice>();
        foreach (var account in accounts.OrderBy(a => a.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(account.Id))
                continue;
            list.Add(new EmailMailboxChoice
            {
                Id = account.Id,
                Title = TitleFor(account.Id),
                Subtitle = SubtitleFor(account.Id, account),
                DefaultDisplayName = DefaultDisplayNameFor(account.Id)
            });
        }

        return list;
    }

    /// <summary>System tab summary lines: mailbox name + owner (never password).</summary>
    public static string FormatSystemListLine(EmailAccountSnapshot account)
    {
        var name = TitleFor(account.Id);
        var owner = string.IsNullOrWhiteSpace(account.DisplayName)
            ? (string.IsNullOrWhiteSpace(account.Address) ? "(no owner)" : account.Address)
            : account.DisplayName;
        var state = !account.Enabled ? "off"
            : account.IsConfigured ? "ready"
            : account.HasPassword ? "almost"
            : "needs setup";
        return $"{name} · {owner} · {state}";
    }
}
