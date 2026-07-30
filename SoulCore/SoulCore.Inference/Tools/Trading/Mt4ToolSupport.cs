using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Trading;

/// <summary>
/// Shared gate + argument helpers for the 11 MT4 tools (BED-138).
/// Gates run in the <see cref="ITool"/> layer — never in the bridge — so
/// unit tests can prove no <see cref="IMt4Bridge.InvokeAsync"/> call reaches
/// the backend when a gate is closed.
/// </summary>
public static class Mt4ToolSupport
{
    public const string ReadDeniedMessage =
        "mt4 read requires user authorization — enable AllowMt4Read in Settings → Tools & Access";

    public const string TradeDeniedMessage =
        "mt4 trade requires user authorization — enable AllowMt4Trade in Settings → Tools & Access";

    public const string SlRequiredMessage =
        "sl required: execute_trade rejects trades without a valid stop-loss (sl)";

    /// <summary>
    /// Map a SoulCore tool name to the Hermes MCP <c>mt4_*</c> name.
    /// <c>mt4_status</c> stays; <c>list_symbols</c> → <c>mt4_list_symbols</c>.
    /// </summary>
    public static string ToMcpName(string soulCoreToolName)
    {
        if (string.IsNullOrWhiteSpace(soulCoreToolName))
            return "mt4_unknown";
        var name = soulCoreToolName.Trim();
        return name.StartsWith("mt4_", StringComparison.Ordinal)
            ? name
            : "mt4_" + name;
    }

    public static bool IsReadAllowed(Desktop.IToolsAccessSettings access) =>
        access is not null && access.AllowMt4Read;

    public static bool IsTradeAllowed(Desktop.IToolsAccessSettings access) =>
        access is not null && access.AllowMt4Trade;

    public static bool IsReadAllowed(ToolsOptions options) =>
        options is not null && options.AllowMt4Read;

    public static bool IsTradeAllowed(ToolsOptions options) =>
        options is not null && options.AllowMt4Trade;

    public static bool IsConfirmed(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object)
            return false;
        if (!args.TryGetProperty("confirmed", out var prop))
            return false;

        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => IsTruthyString(prop.GetString()),
            JsonValueKind.Number => prop.TryGetInt32(out var n) && n != 0,
            _ => false
        };
    }

    /// <summary>
    /// Returns a clone of <paramref name="args"/> with SoulCore-only keys
    /// (e.g. <c>confirmed</c>) removed before dispatch to the MCP bridge.
    /// </summary>
    public static JsonElement StripConfirmed(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object)
            return args;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var prop in args.EnumerateObject())
            {
                if (string.Equals(prop.Name, "confirmed", StringComparison.OrdinalIgnoreCase))
                    continue;
                prop.WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        return JsonDocument.Parse(stream.ToArray()).RootElement.Clone();
    }

    public static bool TryGetRequiredString(
        JsonElement args,
        string name,
        out string value,
        out ToolResult? error)
    {
        value = string.Empty;
        error = null;
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty(name, out var prop)
            || prop.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(prop.GetString()))
        {
            error = new ToolResult(
                false,
                $"error: '{name}' (string) is required",
                null);
            return false;
        }

        value = prop.GetString()!.Trim();
        return true;
    }

    public static bool TryGetRequiredDouble(
        JsonElement args,
        string name,
        out double value,
        out ToolResult? error)
    {
        value = 0;
        error = null;
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty(name, out var prop))
        {
            error = new ToolResult(false, $"error: '{name}' (number) is required", null);
            return false;
        }

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out value))
            return true;

        if (prop.ValueKind == JsonValueKind.String
            && double.TryParse(prop.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;

        error = new ToolResult(false, $"error: '{name}' must be a valid number", null);
        return false;
    }

    public static bool TryGetOptionalDouble(JsonElement args, string name, out double? value)
    {
        value = null;
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty(name, out var prop)
            || prop.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var n))
        {
            value = n;
            return true;
        }

        if (prop.ValueKind == JsonValueKind.String
            && double.TryParse(prop.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out n))
        {
            value = n;
            return true;
        }

        return false;
    }

    public static bool TryGetRequiredInt64(
        JsonElement args,
        string name,
        out long value,
        out ToolResult? error)
    {
        value = 0;
        error = null;
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty(name, out var prop))
        {
            error = new ToolResult(false, $"error: '{name}' (integer) is required", null);
            return false;
        }

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out value))
            return true;

        if (prop.ValueKind == JsonValueKind.String
            && long.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return true;

        error = new ToolResult(false, $"error: '{name}' must be a valid integer", null);
        return false;
    }

    /// <summary>
    /// Validate <c>sl</c> for <c>execute_trade</c>: must be present and a finite
    /// positive number (price or points — MT4 EA interprets).
    /// </summary>
    public static bool TryValidateStopLoss(JsonElement args, out double sl, out ToolResult? error)
    {
        sl = 0;
        error = null;
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("sl", out var prop)
            || prop.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            error = new ToolResult(false, SlRequiredMessage, null);
            return false;
        }

        double parsed;
        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out parsed))
        {
            // ok
        }
        else if (prop.ValueKind == JsonValueKind.String
            && double.TryParse(prop.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
        {
            // ok
        }
        else
        {
            error = new ToolResult(false, SlRequiredMessage, null);
            return false;
        }

        if (double.IsNaN(parsed) || double.IsInfinity(parsed) || parsed <= 0)
        {
            error = new ToolResult(false, SlRequiredMessage, null);
            return false;
        }

        sl = parsed;
        return true;
    }

    public static string BuildExecuteTradeConfirmPrompt(
        string direction,
        double volume,
        string symbol)
    {
        var dir = string.IsNullOrWhiteSpace(direction) ? "?" : direction.Trim().ToUpperInvariant();
        var vol = volume.ToString("0.####", CultureInfo.InvariantCulture);
        var sym = string.IsNullOrWhiteSpace(symbol) ? "?" : symbol.Trim().ToUpperInvariant();
        return $"confirm trade: {dir} {vol} {sym} at market? reply yes to confirm";
    }

    public static string BuildClosePositionConfirmPrompt(long ticket) =>
        $"confirm close position: ticket {ticket}? reply yes to confirm";

    public static string BuildBacktestConfirmPrompt(
        string ea,
        string symbol,
        string from,
        string to) =>
        $"confirm backtest: {ea} on {symbol} from {from} to {to}? reply yes to confirm";

    public static JsonElement EmptyObjectSchema() =>
        JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement.Clone();

    public static JsonElement ParseSchema(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static bool IsTruthyString(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return false;
        return s.Equals("true", StringComparison.OrdinalIgnoreCase)
            || s.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || s.Equals("1", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Base for MT4 <see cref="ITool"/> implementations — holds options + bridge
/// and provides gated read/write dispatch helpers.
/// </summary>
public abstract class Mt4ToolBase : ITool
{
    private readonly IOptions<ToolsOptions> _options;
    private readonly Desktop.IToolsAccessSettings? _access;

    protected Mt4ToolBase(IMt4Bridge bridge, IOptions<ToolsOptions> options)
        : this(bridge, options, access: null)
    {
    }

    protected Mt4ToolBase(
        IMt4Bridge bridge,
        IOptions<ToolsOptions> options,
        Desktop.IToolsAccessSettings? access)
    {
        Bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _access = access;
    }

    protected IMt4Bridge Bridge { get; }

    protected ToolsOptions Options => _options.Value;

    public abstract ToolDefinition Definition { get; }

    public abstract Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default);

    protected Task<ToolResult> ExecuteReadAsync(JsonElement args, CancellationToken ct)
    {
        var allowed = _access is not null
            ? Mt4ToolSupport.IsReadAllowed(_access)
            : Mt4ToolSupport.IsReadAllowed(Options);
        if (!allowed)
            return Task.FromResult(new ToolResult(false, Mt4ToolSupport.ReadDeniedMessage, null));

        var mcp = Mt4ToolSupport.ToMcpName(Definition.Name);
        var payload = args.ValueKind == JsonValueKind.Object ? args : Mt4ToolSupport.ParseSchema("{}");
        return Bridge.InvokeAsync(mcp, payload, ct);
    }

    /// <summary>
    /// Two-phase write: master AllowMt4Trade gate,
    /// then per-call <c>confirmed=true</c> gate, then bridge dispatch.
    /// </summary>
    protected Task<ToolResult> ExecuteWriteAsync(
        JsonElement args,
        string confirmPrompt,
        CancellationToken ct)
    {
        var allowed = _access is not null
            ? Mt4ToolSupport.IsTradeAllowed(_access)
            : Mt4ToolSupport.IsTradeAllowed(Options);
        if (!allowed)
            return Task.FromResult(new ToolResult(false, Mt4ToolSupport.TradeDeniedMessage, null));

        if (!Mt4ToolSupport.IsConfirmed(args))
            return Task.FromResult(new ToolResult(false, confirmPrompt, null));

        var mcp = Mt4ToolSupport.ToMcpName(Definition.Name);
        var payload = Mt4ToolSupport.StripConfirmed(
            args.ValueKind == JsonValueKind.Object ? args : Mt4ToolSupport.ParseSchema("{}"));
        return Bridge.InvokeAsync(mcp, payload, ct);
    }
}
