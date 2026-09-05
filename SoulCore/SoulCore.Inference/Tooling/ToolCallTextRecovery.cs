using System.Text.Json;
using System.Text.RegularExpressions;

namespace SoulCore.Inference.Tooling;

/// <summary>
/// Recover tool calls leaked into assistant <c>content</c> (gemma4
/// <c>&lt;execute_tool&gt;</c>, JSON blobs, Gemma tool_call tokens).
/// </summary>
public static class ToolCallTextRecovery
{
    private static readonly Regex ExecuteToolTag = new(
        @"<execute_tool>\s*(?<name>[A-Za-z0-9_]+)\s*(?<args>\{[\s\S]*?\})?\s*</execute_tool>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GemmaToolCall = new(
        @"<\|tool_call>\s*call:(?<name>[A-Za-z0-9_]+)\s*(?<args>\{[\s\S]*?\})?\s*<tool_call\|>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryRecover(
        string content,
        IReadOnlySet<string> toolNames,
        out List<RecoveredToolCall> calls)
    {
        calls = new List<RecoveredToolCall>();
        if (string.IsNullOrWhiteSpace(content) || toolNames.Count == 0)
            return false;

        foreach (Match m in ExecuteToolTag.Matches(content))
            TryAddMatch(m.Groups["name"].Value, m.Groups["args"].Value, toolNames, calls);

        foreach (Match m in GemmaToolCall.Matches(content))
            TryAddMatch(m.Groups["name"].Value, m.Groups["args"].Value, toolNames, calls);

        if (calls.Count > 0)
            return true;

        if (TryRecoverJson(content, toolNames, out var jsonCall) && jsonCall.HasValue)
        {
            calls.Add(jsonCall.Value);
            return true;
        }

        return false;
    }

    public static bool LooksLikeToolLeak(string? content, IReadOnlySet<string> toolNames)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;
        return ExecuteToolTag.IsMatch(content)
               || GemmaToolCall.IsMatch(content)
               || TryRecoverJson(content, toolNames, out _);
    }

    private static void TryAddMatch(
        string name,
        string argsText,
        IReadOnlySet<string> toolNames,
        List<RecoveredToolCall> calls)
    {
        if (string.IsNullOrWhiteSpace(name) || !toolNames.Contains(name))
            return;
        var args = ParseArgs(argsText);
        if (calls.Any(c => string.Equals(c.Name, name, StringComparison.Ordinal)
                           && JsonElementEquals(c.Arguments, args)))
        {
            return;
        }

        calls.Add(new RecoveredToolCall(name, args));
    }

    private static JsonElement? ParseArgs(string argsText)
    {
        if (string.IsNullOrWhiteSpace(argsText))
            return JsonDocument.Parse("{}").RootElement.Clone();
        try
        {
            using var doc = JsonDocument.Parse(argsText.Trim());
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}").RootElement.Clone();
        }
    }

    private static bool TryRecoverJson(
        string content,
        IReadOnlySet<string> toolNames,
        out RecoveredToolCall? call)
    {
        call = null;
        if (TryParseJsonObject(content, toolNames, out call))
            return true;

        var json = ExtractFirstJsonObject(content);
        if (json is not null && TryParseJsonObject(json, toolNames, out call))
            return true;

        return false;
    }

    private static bool TryParseJsonObject(
        string json,
        IReadOnlySet<string> toolNames,
        out RecoveredToolCall? call)
    {
        call = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("name", out var nameEl)
                || nameEl.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var name = nameEl.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(name) || !toolNames.Contains(name))
                return false;

            JsonElement? argsEl = null;
            if (root.TryGetProperty("arguments", out var rawArgs))
            {
                argsEl = rawArgs.ValueKind switch
                {
                    JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Number
                        or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null
                        => rawArgs.Clone(),
                    JsonValueKind.String => ParseStringArguments(rawArgs.GetString()),
                    _ => null
                };
            }

            call = new RecoveredToolCall(name, argsEl);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static JsonElement? ParseStringArguments(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractFirstJsonObject(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0)
            return null;
        var depth = 0;
        var inString = false;
        var escape = false;
        for (var i = start; i < text.Length; i++)
        {
            var ch = text[i];
            if (inString)
            {
                if (escape) { escape = false; continue; }
                if (ch == '\\') { escape = true; continue; }
                if (ch == '"') { inString = false; }
                continue;
            }

            if (ch == '"') { inString = true; continue; }
            if (ch == '{') depth++;
            else if (ch == '}')
            {
                depth--;
                if (depth == 0)
                    return text.Substring(start, i - start + 1);
            }
        }

        return null;
    }

    private static bool JsonElementEquals(JsonElement? a, JsonElement? b)
    {
        if (!a.HasValue && !b.HasValue) return true;
        if (!a.HasValue || !b.HasValue) return false;
        return a.Value.GetRawText() == b.Value.GetRawText();
    }

    public readonly record struct RecoveredToolCall(string Name, JsonElement? Arguments);
}
