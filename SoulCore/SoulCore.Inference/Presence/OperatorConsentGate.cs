using System.Text.RegularExpressions;

namespace SoulCore.Inference.Presence;

/// <summary>
/// PROP-17.1: host-side one-shot consent for email/MT4 writes.
/// Model-minted <c>confirmed=true</c> is never consent — only LinearThrone/Kayleigh
/// (chat affirmative or companion-token POST) can <see cref="Grant"/>.
/// </summary>
public interface IOperatorConsentGate
{
    void Grant(string scope);
    bool TryConsume(string scope);
    void NotePending(string scope);
    string? PendingScope { get; }
    bool TryGrantFromUserText(string? userText);
}

public sealed class OperatorConsentGate : IOperatorConsentGate
{
    public const string EmailSend = "email_send";
    public const string EmailDelete = "email_delete";
    public const string Mt4Write = "mt4_write";

    private static readonly Regex Affirmative = new(
        @"^(?:yes|y|ok|okay|confirm|confirmed|do it|go ahead|approved?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly object _gate = new();
    private readonly HashSet<string> _granted = new(StringComparer.OrdinalIgnoreCase);
    private string? _pending;

    public string? PendingScope
    {
        get
        {
            lock (_gate)
                return _pending;
        }
    }

    public void Grant(string scope)
    {
        var s = Normalize(scope);
        if (s.Length == 0)
            return;
        lock (_gate)
        {
            _granted.Add(s);
            if (string.Equals(_pending, s, StringComparison.OrdinalIgnoreCase))
                _pending = null;
        }
    }

    public bool TryConsume(string scope)
    {
        var s = Normalize(scope);
        if (s.Length == 0)
            return false;
        lock (_gate)
            return _granted.Remove(s);
    }

    public void NotePending(string scope)
    {
        var s = Normalize(scope);
        if (s.Length == 0)
            return;
        lock (_gate)
            _pending = s;
    }

    public bool TryGrantFromUserText(string? userText)
    {
        if (string.IsNullOrWhiteSpace(userText) || !Affirmative.IsMatch(userText.Trim()))
            return false;
        string? pending;
        lock (_gate)
            pending = _pending;
        if (pending is null)
            return false;
        Grant(pending);
        return true;
    }

    private static string Normalize(string? scope) => (scope ?? "").Trim().ToLowerInvariant();
}
