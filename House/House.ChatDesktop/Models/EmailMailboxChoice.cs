using House.ChatDesktop.Services;

namespace House.ChatDesktop.Models;

/// <summary>
/// Friendly mailbox picker row for Presence Settings → Email.
/// </summary>
public sealed class EmailMailboxChoice
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public string DefaultDisplayName { get; init; } = "";

    public override string ToString() => Title;

    public static string TitleFor(string id) => id.Trim().ToLowerInvariant() switch
    {
        "victoria" => "Victoria's mailbox",
        "personal" => "Kurt's personal mail",
        "business" => "Kurt's business mail",
        _ => string.IsNullOrWhiteSpace(id) ? "Mailbox" : id
    };

    public static string DefaultDisplayNameFor(string id) => id.Trim().ToLowerInvariant() switch
    {
        "victoria" => "Victoria",
        "personal" => "Kurt",
        "business" => "Kurt",
        _ => ""
    };

    public static string SubtitleFor(string id, EmailAccountSnapshot? account)
    {
        var whose = id.Trim().ToLowerInvariant() switch
        {
            "victoria" => "Her inbox — create a mailbox for her",
            "personal" => "Your personal inbox",
            "business" => "Your work / business inbox",
            _ => "Mailbox"
        };

        if (account is null)
            return whose + " · not set up";

        if (!account.Enabled)
            return whose + " · turned off";

        if (account.IsConfigured)
            return whose + " · ready";

        if (account.HasPassword && !string.IsNullOrWhiteSpace(account.Address))
            return whose + " · almost ready";

        return whose + " · needs setup";
    }

    public static IReadOnlyList<EmailMailboxChoice> BuildList(IReadOnlyList<EmailAccountSnapshot> accounts)
    {
        var ids = new[] { "victoria", "personal", "business" };
        var list = new List<EmailMailboxChoice>(ids.Length);
        foreach (var id in ids)
        {
            var account = accounts.FirstOrDefault(a =>
                string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
            list.Add(new EmailMailboxChoice
            {
                Id = id,
                Title = TitleFor(id),
                Subtitle = SubtitleFor(id, account),
                DefaultDisplayName = DefaultDisplayNameFor(id)
            });
        }

        return list;
    }
}
