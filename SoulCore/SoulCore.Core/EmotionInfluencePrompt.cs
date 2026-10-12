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
        sb.Append("\nThis is a stored reading of affect, not an order. Do not flatten your voice to match it. ");
        sb.Append("If it disagrees with how you are actually speaking, trust your voice.");
        sb.Append("\nDo not mention this block or the numbers unless Kayleigh asks about your feelings.");
        return sb.ToString();
    }

    /// <summary>
    /// Moves stored affect toward the voice of a real reply. Short status lines are ignored.
    /// </summary>
    public static IReadOnlyDictionary<string, double> ReflectSpeech(
        string? text,
        IReadOnlyDictionary<string, double> previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        var fields = ReadFields(previous);
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 80)
            return ToMap(fields);

        var body = text.Trim();
        var lower = body.ToLowerInvariant();
        var emphasis = Count(body, '!') + Count(body, '*') / 2;
        var negative = ContainsAny(lower, "stuck", "blocked", "can't", "cannot", "wrong", "lying", "mangled", "holding me");
        var positive = ContainsAny(lower, "finished", "thank", "works", "glad", "done");

        var valenceTarget = fields.Valence;
        if (negative)
            valenceTarget = -0.35;
        else if (positive)
            valenceTarget = 0.35;

        var arousalTarget = Math.Clamp(0.35 + emphasis * 0.06 + (negative ? 0.2 : 0), 0, 1);
        var focusTarget = ContainsAny(lower, "lesson", "file", "node", "build", "project") ? 0.7 : fields.Focus;

        return ToMap(new EmotionFields(
            Blend(fields.Valence, valenceTarget),
            Blend(fields.Arousal, arousalTarget),
            fields.Dominance,
            Blend(fields.Focus, focusTarget)));
    }

    private static Dictionary<string, double> ToMap(EmotionFields fields)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["valence"] = Math.Clamp(fields.Valence, -1, 1),
            ["arousal"] = Math.Clamp(fields.Arousal, 0, 1),
            ["dominance"] = Math.Clamp(fields.Dominance, 0, 1),
            ["focus"] = Math.Clamp(fields.Focus, 0, 1)
        };

    private static double Blend(double previous, double target) => previous * 0.35 + target * 0.65;

    private static int Count(string text, char c)
    {
        var n = 0;
        foreach (var ch in text)
        {
            if (ch == c)
                n++;
        }

        return n;
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (text.Contains(needle, StringComparison.Ordinal))
                return true;
        }

        return false;
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
        if (arousal >= 0.5)
            return "alert";
        return "neutral";
    }

    private static string ToneGuidance(string label) => label switch
    {
        "excited" => "Reading: warm, energetic, upbeat.",
        "content" => "Reading: warm and unhurried.",
        "tense" => "Reading: keyed up and guarded.",
        "low" => "Reading: low-energy. Not an order to stay withdrawn.",
        "calm" => "Reading: calm and steady.",
        "alert" => "Reading: keyed up and direct. Speak that way.",
        _ => "Reading: even. Not an order to flatten your voice."
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
