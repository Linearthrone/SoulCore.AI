namespace SoulCore.Inference.Presence;

/// <summary>
/// PROP-17.3: R&amp;R may research freely, but buy / email / install / trade /
/// “drop the lesson and build it” must land as <c>suspended</c> for LinearThrone —
/// never start-now on the work slot.
/// </summary>
public static class ActionProposalClassifier
{
    public readonly record struct Proposal(string Title, string Description, string Kind);

    public static bool LooksLikeActionProposal(string? text) =>
        TryClassify(text, out _);

    public static bool TryClassify(string? text, out Proposal proposal)
    {
        proposal = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var t = text.Trim();
        if (IsBuy(t))
        {
            proposal = new Proposal(TitleFrom(t, "Buy"), t, "buy");
            return true;
        }

        if (IsEmail(t))
        {
            proposal = new Proposal(TitleFrom(t, "Email"), t, "email");
            return true;
        }

        if (IsInstall(t))
        {
            proposal = new Proposal(TitleFrom(t, "Install"), t, "install");
            return true;
        }

        if (IsTrade(t))
        {
            proposal = new Proposal(TitleFrom(t, "Trade"), t, "trade");
            return true;
        }

        if (IsReplaceLesson(t))
        {
            proposal = new Proposal(TitleFrom(t, "Build instead"), t, "replace_work");
            return true;
        }

        return false;
    }

    private static bool IsBuy(string t) =>
        ContainsAny(t, "buy ", "purchase ", "order this", "add to cart", "check out");

    private static bool IsEmail(string t) =>
        ContainsAny(t, "send email", "email kayleigh", "email lineart", "compose email", "mail this");

    private static bool IsInstall(string t) =>
        ContainsAny(t, "apt install", "snap install", "flatpak install", "install firefox",
            "install chrome", "install chromium", "pip install", "npm install -g");

    private static bool IsTrade(string t) =>
        ContainsAny(t, "open trade", "place order", "buy lot", "sell lot", "mt4 ", "market order");

    private static bool IsReplaceLesson(string t) =>
        ContainsAny(t, "drop the lesson", "skip the lesson", "instead of the lesson",
            "stop the lesson and build", "forget the assignment", "replace the lesson");

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (haystack.Contains(n, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string TitleFrom(string text, string prefix)
    {
        var body = text.Length > 60 ? text[..57] + "..." : text;
        var title = prefix + ": " + body;
        return title.Length > 80 ? title[..77] + "..." : title;
    }
}
