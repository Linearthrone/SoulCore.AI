using System.Text.RegularExpressions;

namespace SoulCore.Inference.Tools.ChiefArchitect;

/// <summary>
/// System guidance for Chief Architect X17 builds via computer-use + ca_* planning tools.
/// </summary>
public static class ChiefArchitectGuidance
{
    public const string Marker = "[ChiefArchitect]";

    public const string Block =
        Marker + "\n" +
        "For Chief Architect X17, plan with ca_compile_brief / ca_plan_project, then drive that window. " +
        "A slab is Build > Floor > Build Foundation, not a freehand draw. Clicks need AllowComputerControl.";

    public static string AppendToPreamble(string? contextPreamble)
    {
        var baseText = string.IsNullOrWhiteSpace(contextPreamble)
            ? string.Empty
            : contextPreamble.TrimEnd();

        if (baseText.Contains(Marker, StringComparison.Ordinal))
            return baseText;

        if (baseText.Length == 0)
            return Block;

        return baseText + "\n\n" + Block;
    }
}

/// <summary>
/// High-confidence NL -> force <c>ca_plan_project</c> for house / slab / bedroom briefs.
/// </summary>
public static class ChiefArchitectToolIntent
{
    public enum Kind
    {
        Plan,
        Focus
    }

    public readonly record struct Match(Kind Intent, string ToolName);

    private static readonly Regex ExplicitTool = new(
        @"\b(?:ca_plan_project|ca_compile_brief|ca_get_recipe|ca_next_step)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BuildHouse = new(
        @"\b(?:draw|build|design|create|draft)\b[\s\S]{0,40}\b(?:house|home|floor\s*plan|3\s*br|bedroom|slab|ranch)\b|" +
        @"\b(?:chief\s*architect|ca\s*x17)\b|" +
        @"\b\d+\s*(?:br|bedroom)s?\b[\s\S]{0,40}\b(?:slab|single[\s-]?stor|story)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryMatch(string? userText, out Match match)
    {
        match = default;
        if (string.IsNullOrWhiteSpace(userText))
            return false;

        var text = userText.Trim();
        if (ExplicitTool.IsMatch(text) || BuildHouse.IsMatch(text))
        {
            match = new Match(Kind.Plan, "ca_plan_project");
            return true;
        }

        return false;
    }
}
