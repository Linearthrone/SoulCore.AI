using System.Text.RegularExpressions;
using SoulCore.Inference.Clients;
using SoulCore.Memory;

namespace SoulCore.Inference.Presence;

/// <summary>
/// Decides when a chat turn should keep going without waiting for Kayleigh,
/// and the short status line Presence shows while that happens.
/// </summary>
public static class WorkContinuation
{
    /// <summary>Extra model passes after the first, so a full tool round does not end the task.</summary>
    public const int MaxExtraPasses = 3;

    public const string Nudge =
        "Continue the unfinished task from where you left off. Take the next concrete step. " +
        "Do not wait to be told to go. If you need Kayleigh to decide, or she already told you to hold, " +
        "say so in one sentence and call no tools.";

    public const string ContinuingLine =
        "Still working. That tool round filled up, so I'm continuing.";

    public const string PausedLine =
        "I paused because the tool round filled up. Say something and I'll pick it back up.";

    /// <summary>
    /// Fallback when the model ends a tool round with blank text (gemma4 habit).
    /// Keeps the outer WorkContinuation pass alive instead of throwing empty.
    /// </summary>
    public const string EmptyAfterToolsLine =
        "That step finished. Continuing from where I left off.";

    public const string WaitingLine = "Waiting on you";

    public const string PausedActivity = "Paused — say something to send her again";

    public const string NoAssignmentPhrase = "No open assignment";

    /// <summary>Desktop chat history key. Background work uses the same session as Presence.</summary>
    public const string PresenceSessionId = "presence-local";

    private static readonly Regex UserStop = new(
        @"^(?:stop|wait|pause|hold on)\b|\bhands off\b|\bdon'?t (?:click|type|continue|touch)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Handoff = new(
        @"\b(?:standing by|hands off|waiting on you|your call|say the word|what do you want|i(?:'m| am) stopped)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsWorkTool(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        return name.StartsWith("desktop_", StringComparison.Ordinal)
               || name.StartsWith("browser_", StringComparison.Ordinal)
               || name.StartsWith("guest_", StringComparison.Ordinal)
               || name is "list_desktop_windows" or "focus_desktop_window";
    }

    public static bool IsUserStop(string? userText)
        => !string.IsNullOrWhiteSpace(userText) && UserStop.IsMatch(userText.Trim());

    public static bool IsHandoff(string? reply)
        => !string.IsNullOrWhiteSpace(reply) && Handoff.IsMatch(reply);

    private static readonly Regex Assignment = new(
        @"\b(?:continue|complete|finish|lesson|lessons|keep working|go ahead|work on|research)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CheckIn = new(
        @"\b(?:both|the two|two) lessons (?:are|were) (?:now )?(?:done|finished|complete)\b|\bfinished both lessons\b|\bready for (?:the )?review\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FutureCheckIn = new(
        @"\b(?:when|until|once) both lessons\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NeedsHer = new(
        @"\b(?:need you to decide|need kayleigh|cannot proceed|can't proceed|blocked until you)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool LooksLikeAssignment(string? userText)
        => !IsUserStop(userText)
           && !string.IsNullOrWhiteSpace(userText)
           && Assignment.IsMatch(userText);

    private static readonly Regex GreenLight = new(
        @"\b(?:go ahead|keep working|keep going|you can start|start working|get started)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ExplicitNewTodo = new(
        @"\b(?:add|create|make|remember|put) (?:this |a |an |the )?(?:task|to-?do)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ChatLead = new(
        @"^(?:great\b|why don'?t\b|that is probably\b|those dates\b|baby[, ])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Permission to start work already on the list. Not a new to-do.
    /// </summary>
    public static bool IsGreenLight(string? userText)
        => !string.IsNullOrWhiteSpace(userText)
           && !IsUserStop(userText)
           && !ExplicitNewTodo.IsMatch(userText)
           && GreenLight.IsMatch(userText);

    /// <summary>
    /// True when a proposed title is Kayleigh's chat, not a to-do.
    /// </summary>
    public static bool IsChatLoggedAsTask(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return true;
        var t = title.Trim();
        if (IsGreenLight(t) || ChatLead.IsMatch(t))
            return true;
        var words = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length >= 12
               && t.Contains(" you ", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Next real to-do: an in-progress row, else the oldest todo, else the oldest suspended row.
    /// Parked and done rows stay put.
    /// </summary>
    public static VictoriaTask? NextOpenTask(IReadOnlyList<VictoriaTask> rows)
    {
        static VictoriaTask? Oldest(IEnumerable<VictoriaTask> src) =>
            src.OrderBy(t => t.Id).FirstOrDefault();

        return Oldest(rows.Where(t => string.Equals(t.Status, "in_progress", StringComparison.OrdinalIgnoreCase)))
               ?? Oldest(rows.Where(t => string.Equals(t.Status, "todo", StringComparison.OrdinalIgnoreCase)))
               ?? Oldest(rows.Where(t => string.Equals(t.Status, "suspended", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// She is done with the assignment and handing it back. A promise to check in later does not count.
    /// </summary>
    public static bool IsCheckIn(string? reply)
        => !string.IsNullOrWhiteSpace(reply)
           && !FutureCheckIn.IsMatch(reply)
           && CheckIn.IsMatch(reply);

    public static bool NeedsKayleigh(string? reply)
        => !string.IsNullOrWhiteSpace(reply) && NeedsHer.IsMatch(reply);

    public static string ActivityFor(string assignment)
    {
        var t = (assignment ?? "").Trim().Replace('\n', ' ');
        if (t.Length > 70)
            t = t[..67].TrimEnd() + "...";
        return t.Length == 0 ? "Working" : "Working — " + t;
    }

    public static string AssignmentPrompt(string assignment, int noToolStreak)
    {
        var extra = noToolStreak > 0
            ? " Your last pass did not use a tool. Use one now and take the next concrete step."
            : "";
        return "Open assignment from Kayleigh, still in force while she is away: "
               + (assignment ?? "").Trim()
               + ". Keep working until that assignment is actually finished. "
               + "Check in with her only when it says to, or you truly cannot proceed without her. "
               + "Do not write her a status note about why you are not working. "
               + "Do not stop just because she has not typed."
               + extra;
    }

    public static bool HitToolCap(string? reply)
        => !string.IsNullOrWhiteSpace(reply)
           && reply.Contains(OllamaInferenceClient.IterationCapMarker, StringComparison.Ordinal);

    /// <summary>
    /// True when she was in the middle of desktop or browser work and did not
    /// hand the turn back. Social replies with no work tools do not continue.
    /// </summary>
    public static bool ShouldKeepGoing(string? userText, string? reply, bool usedWorkTools)
    {
        if (IsUserStop(userText))
            return false;
        if (HitToolCap(reply))
            return true;
        if (!usedWorkTools)
            return false;
        if (IsHandoff(reply))
            return false;
        return true;
    }

    public static string PhraseForTool(string? name)
    {
        var n = (name ?? "").Trim();
        return n switch
        {
            "desktop_screenshot" or "browser_capture_tab" or "browser_snapshot" => "Looking at the screen",
            "desktop_click" or "browser_click" or "browser_click_text" => "Clicking",
            "desktop_type" or "browser_type" or "browser_fill" => "Typing",
            "desktop_key" or "browser_key" => "Pressing keys",
            "desktop_scroll" or "browser_scroll" => "Scrolling",
            "guest_read_file" => "Reading a file",
            "guest_write_file" => "Writing a file",
            "guest_list_dir" => "Looking through files",
            "guest_shell" => "Running a command",
            "desktop_open_app" => "Opening an app",
            "browser_navigate" => "Opening a page",
            _ when n.StartsWith("guest_", StringComparison.Ordinal) => "Working in the project",
            _ when n.StartsWith("browser_", StringComparison.Ordinal) => "In the browser",
            _ when n.StartsWith("desktop_", StringComparison.Ordinal) => "Using the desktop",
            _ when n.Length == 0 => "Working",
            _ => "Working — " + n
        };
    }

    public static string VisibleText(string reply, bool continuing)
    {
        if (!HitToolCap(reply))
            return reply;
        return continuing ? ContinuingLine : PausedLine;
    }
}
