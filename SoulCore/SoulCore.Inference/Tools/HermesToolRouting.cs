using System.Text.Json;
using SoulCore.Config;

namespace SoulCore.Inference.Tools;

/// <summary>
/// Shared helpers for BED-144 Hermes-routed tools: backend selection,
/// unavailable handling, and native-fallback refusal.
/// </summary>
public static class HermesToolRouting
{
    public const string NativeNotImplementedMessage =
        "native backend not implemented for this tool — set Backend=llmod/bridge or implement the native path";

    public const string ComputerControlRequiredMessage =
        "desktop control requires user authorization — ask the user to enable AllowComputerControl";

    public const string DesktopCaptureDisabledMessage =
        "desktop capture disabled — set Tools.AllowDesktopCapture=true";

    public const string BrowserCaptureDisabledMessage =
        "browser capture disabled — set Tools.AllowBrowserCapture=true";

    public const string Mt4ReadDisabledMessage =
        "mt4 read requires user authorization — set Tools.AllowMt4Read=true";

    public const string Mt4TradeDisabledMessage =
        "mt4 trade requires user authorization — set Tools.AllowMt4Trade=true";

    public static bool IsHermesBackend(string? backend) =>
        string.Equals(
            (backend ?? string.Empty).Trim(),
            ToolsOptions.BackendHermes,
            StringComparison.OrdinalIgnoreCase);

    public static bool IsNativeBackend(string? backend) =>
        string.Equals(
            (backend ?? string.Empty).Trim(),
            ToolsOptions.BackendNative,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>BED-169: direct LLMOD MCP HTTP on shadow (<c>llmod</c> or <c>native</c> alias).</summary>
    public static bool IsLlmodBackend(string? backend)
    {
        var b = (backend ?? string.Empty).Trim();
        return string.Equals(b, ToolsOptions.BackendLlmod, StringComparison.OrdinalIgnoreCase)
            || string.Equals(b, ToolsOptions.BackendNative, StringComparison.OrdinalIgnoreCase);
    }

    public static JsonElement EmptyArgs() =>
        JsonDocument.Parse("{}").RootElement.Clone();

    public static JsonElement MergeObject(JsonElement args, IReadOnlyDictionary<string, object?> extras)
    {
        var dict = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (args.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in args.EnumerateObject())
                dict[p.Name] = p.Value.Clone();
        }

        foreach (var (key, value) in extras)
        {
            if (value is null) continue;
            dict[key] = JsonSerializer.SerializeToElement(value);
        }

        return JsonSerializer.SerializeToElement(dict);
    }

    public static bool TryGetString(JsonElement args, string name, out string value)
    {
        value = string.Empty;
        if (args.ValueKind != JsonValueKind.Object) return false;
        if (!args.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String) return false;
        value = el.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    public static bool TryGetInt(JsonElement args, string name, out int value)
    {
        value = 0;
        if (args.ValueKind != JsonValueKind.Object) return false;
        if (!args.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Number) return false;
        return el.TryGetInt32(out value);
    }

    public static bool IsConfirmed(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object) return false;
        if (!args.TryGetProperty("confirmed", out var el)) return false;
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => string.Equals(el.GetString(), "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(el.GetString(), "yes", StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Number => el.TryGetInt32(out var n) && n != 0,
            _ => false
        };
    }

    public static async Task<ToolResult> RouteAsync(
        IHermesMcpInvoker hermes,
        string backend,
        string mcpToolName,
        JsonElement mcpArgs,
        Func<CancellationToken, Task<ToolResult>>? nativeFallback,
        CancellationToken ct)
    {
        if (IsHermesBackend(backend) || string.IsNullOrWhiteSpace(backend))
        {
            return await hermes.CallMcpToolAsync(mcpToolName, mcpArgs, ct).ConfigureAwait(false);
        }

        if (IsNativeBackend(backend))
        {
            if (nativeFallback is not null)
                return await nativeFallback(ct).ConfigureAwait(false);

            return new ToolResult(Success: false, Content: NativeNotImplementedMessage, Data: null);
        }

        return new ToolResult(
            Success: false,
            Content: $"unknown backend '{backend}' — use '{ToolsOptions.BackendLlmod}' or '{ToolsOptions.BackendNative}'",
            Data: null);
    }

    /// <summary>
    /// PreferHermes / BED-161: map Hermes MCP (or Hermes-native) tool names to
    /// SoulCore <see cref="ITool"/> names so the Host tool-loop always dispatches
    /// via the registry (→ <see cref="IHermesMcpInvoker.CallMcpToolAsync"/> for
    /// hermes backends) and never treats Hermes server-side agent tools as the
    /// primary execution path.
    /// </summary>
    /// <returns>
    /// A registered SoulCore tool name, or <c>null</c> when no mapping exists.
    /// </returns>
    public static string? ResolveSoulCoreToolName(
        string? wireName,
        JsonElement args,
        IReadOnlySet<string> soulCoreToolNames)
    {
        if (string.IsNullOrWhiteSpace(wireName) || soulCoreToolNames is null || soulCoreToolNames.Count == 0)
            return null;

        var name = wireName.Trim();
        if (soulCoreToolNames.Contains(name))
            return name;

        // computer_use (+ action) → desktop_* SoulCore tools.
        if (string.Equals(name, "computer_use", StringComparison.Ordinal))
        {
            var action = "screenshot";
            if (TryGetString(args, "action", out var a) && !string.IsNullOrWhiteSpace(a))
                action = a.Trim().ToLowerInvariant();

            var mapped = action switch
            {
                "screenshot" or "capture" or "screen" => "desktop_screenshot",
                "click" or "left_click" or "right_click" => "desktop_click",
                "drag" or "mouse_drag" or "left_click_drag" => "desktop_drag",
                "type" or "type_text" => "desktop_type",
                "key" or "hotkey" or "press" => "desktop_key",
                _ => "desktop_screenshot"
            };
            return soulCoreToolNames.Contains(mapped) ? mapped : null;
        }

        // browser_bridge_* → browser_*
        if (name.StartsWith("browser_bridge_", StringComparison.Ordinal))
        {
            var suffix = name["browser_bridge_".Length..];
            var candidate = string.Equals(suffix, "health", StringComparison.Ordinal)
                ? "browser_health"
                : "browser_" + suffix;
            return soulCoreToolNames.Contains(candidate) ? candidate : null;
        }

        return null;
    }
}
