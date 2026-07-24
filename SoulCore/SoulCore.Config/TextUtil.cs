namespace SoulCore.Config;

/// <summary>Small shared string helpers for logging / error bodies.</summary>
public static class TextUtil
{
    public static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";
}
