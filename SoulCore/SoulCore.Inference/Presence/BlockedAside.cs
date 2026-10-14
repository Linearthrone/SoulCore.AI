using System.Text.RegularExpressions;
using SoulCore.Memory;

namespace SoulCore.Inference.Presence;

/// <summary>
/// One chat note when unfinished work cannot run. Later passes stay on the activity band.
/// </summary>
public interface IBlockedAsideNotice
{
    /// <summary>True only the first time she should tell Kayleigh she is doing her own thing.</summary>
    bool TryClaim();

    /// <summary>Work is moving again, so a later block may say so once more.</summary>
    void WorkCanRun();
}

public sealed class BlockedAsideNotice : IBlockedAsideNotice
{
    private int _claimed;

    public bool TryClaim() => Interlocked.CompareExchange(ref _claimed, 1, 0) == 0;

    public void WorkCanRun() => Interlocked.Exchange(ref _claimed, 0);
}

public static class BlockedAside
{
    public const string Line = "I'll do my own thing until that block is fixed.";

    private static readonly Regex WhyNotWorking = new(
        @"\b(?:isn'?t|aren'?t|not) working\b|\b(?:can(?:not|'t)|unable to) (?:work|proceed)\b|\bnothing (?:i can|to) (?:work on|do)\b|\bwhy i (?:am |'m )?not\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool CountsAsUnfinishedBlock(VictoriaTask task)
    {
        var status = task.Status ?? "";
        return status.Equals("parked", StringComparison.OrdinalIgnoreCase)
               || status.Equals("blocked", StringComparison.OrdinalIgnoreCase)
               || status.Equals("todo", StringComparison.OrdinalIgnoreCase)
               || status.Equals("in_progress", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsWhySheIsntWorking(string? text) =>
        !string.IsNullOrWhiteSpace(text) && WhyNotWorking.IsMatch(text);
}
