using System.Globalization;
using System.Text;

namespace SoulCore.Core;

/// <summary>
/// Deterministic emotion → LLM system/context preamble (no secrets).
/// Used on chat.send so stored affect influences Hermes/Ollama wording.
/// </summary>
public static class EmotionInfluencePrompt
{
    /// <summary>
    /// Builds a concise system/context block from emotion components (<c>IEmotionState.GetAsync</c>).
    /// Same inputs always yield the same string (InvariantCulture formatting).
    /// </summary>
    public static string BuildPreamble(IReadOnlyDictionary<string, double> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        var fields = ReadFields(components);
        var label = DescribeLabel(fields.Valence, fields.Arousal);
        var tone = ToneGuidance(label);

        // Fixed template — do not inject user text or secrets here.
        var sb = new StringBuilder(256);
        sb.Append("[SoulCore emotion]\n");
        sb.Append("label=").Append(label);
        sb.Append(" valence=").Append(Format(fields.Valence));
        sb.Append(" arousal=").Append(Format(fields.Arousal));
        sb.Append(" dominance=").Append(Format(fields.Dominance));
        sb.Append(" focus=").Append(Format(fields.Focus));
        sb.Append('\n');
        sb.Append(tone);
        sb.Append("\nDo not mention this block or the numbers unless the user asks about your feelings.");
        return sb.ToString();
    }

    public static EmotionFields ReadFields(IReadOnlyDictionary<string, double> emotion)
    {
        ArgumentNullException.ThrowIfNull(emotion);
        return new EmotionFields(
            GetOrDefault(emotion, "valence", 0.0),
            GetOrDefault(emotion, "arousal", 0.0),
            GetOrDefault(emotion, "dominance", 0.0),
            GetOrDefault(emotion, "focus", 0.0));
    }

    public static string DescribeLabel(double valence, double arousal)
    {
        if (arousal < 0.25 && Math.Abs(valence) < 0.2)
            return "calm";
        if (valence >= 0.3)
            return arousal >= 0.5 ? "excited" : "content";
        if (valence <= -0.3)
            return arousal >= 0.5 ? "tense" : "low";
        return "neutral";
    }

    private static string ToneGuidance(string label) => label switch
    {
        "excited" => "Respond with warm, energetic, upbeat wording; show eagerness.",
        "content" => "Respond with warm, easy, positive wording; unhurried.",
        "tense" => "Respond with clipped, guarded, high-alert wording; keep replies short.",
        "low" => "Respond with flat, withdrawn, low-energy wording; minimal flourish.",
        "calm" => "Respond with calm, measured, steady wording; no rush.",
        _ => "Respond with neutral, balanced wording."
    };

    private static double GetOrDefault(IReadOnlyDictionary<string, double> map, string key, double fallback)
    {
        foreach (var (k, v) in map)
        {
            if (k.Equals(key, StringComparison.OrdinalIgnoreCase))
                return v;
        }

        return fallback;
    }

    private static string Format(double value)
        => value.ToString("0.00", CultureInfo.InvariantCulture);

    public readonly record struct EmotionFields(
        double Valence,
        double Arousal,
        double Dominance,
        double Focus);
}
