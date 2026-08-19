namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// BED-194/195: consistent tool Data so the model cannot treat launch Success as "done".
/// </summary>
public static class BrowserResultHonesty
{
    public const string GoalCompleteKey = "goal_complete";
    public const string ActionOkKey = "action_ok";
    public const string DegradedKey = "degraded";
    public const string LocatorKey = "locator";

    public static object LaunchOnly(string url, string backend) => new
    {
        url,
        backend,
        action_ok = true,
        goal_complete = false,
        load_verified = false,
        note = "Browser process started; page load / login NOT verified. Do not claim navigated or logged in."
    };

    public static object Navigated(string url, string title, string backend, bool goalComplete = false) => new
    {
        url,
        title,
        backend,
        action_ok = true,
        goal_complete = goalComplete,
        load_verified = true
    };

    public static object DegradedPixelSnapshot(string reason) => new
    {
        action_ok = true,
        goal_complete = false,
        degraded = true,
        locator = "pixel",
        note = "AT-SPI/a11y snapshot unavailable — PNG only. Cannot prove Login by this alone. " + reason
    };

    public static object FillRedacted(string field, int valueChars, string backend) => new
    {
        field,
        value_chars = valueChars,
        value = "[redacted]",
        backend,
        action_ok = true,
        goal_complete = false
    };

    public static string RedactSecrets(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;
        // Avoid echoing obvious password assignments in tool Content.
        return System.Text.RegularExpressions.Regex.Replace(
            content,
            @"(?i)(password|passwd|secret|token|otp)\s*[:=]\s*\S+",
            "$1=[redacted]");
    }
}
