using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Trading;

/// <summary>
/// Shared wiring for mt4_* tools (BED-138/144). Per-trade confirmation and
/// SL checks run <b>before</b> Hermes MCP dispatch so the gate holds on both backends.
/// </summary>
public abstract class Mt4ToolBase : ITool
{
    protected readonly IHermesMcpInvoker Hermes;
    protected readonly ToolsOptions Options;

    protected Mt4ToolBase(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
    {
        Hermes = hermes ?? throw new ArgumentNullException(nameof(hermes));
        Options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public abstract ToolDefinition Definition { get; }

    public abstract Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default);

    protected ToolResult? RefuseIfReadDisabled()
    {
        if (Options.AllowMt4Read) return null;
        return new ToolResult(false, HermesToolRouting.Mt4ReadDisabledMessage, null);
    }

    protected ToolResult? RefuseIfTradeDisabled()
    {
        if (Options.AllowMt4Trade) return null;
        return new ToolResult(false, HermesToolRouting.Mt4TradeDisabledMessage, null);
    }

    protected Task<ToolResult> RouteAsync(string mcpName, JsonElement args, CancellationToken ct) =>
        HermesToolRouting.RouteAsync(
            Hermes,
            Options.Mt4Backend,
            mcpName,
            args.ValueKind == JsonValueKind.Object ? args : HermesToolRouting.EmptyArgs(),
            nativeFallback: null,
            ct);

    protected static JsonElement Schema(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();
}

public sealed class Mt4StatusTool : Mt4ToolBase
{
    public Mt4StatusTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "mt4_status",
        "Check MT4 bridge / terminal status.",
        Schema("""{"type":"object","properties":{}}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var denied = RefuseIfReadDisabled();
        if (denied is not null) return denied;
        return await RouteAsync("mt4_status", args, ct).ConfigureAwait(false);
    }
}

public sealed class Mt4ListSymbolsTool : Mt4ToolBase
{
    public Mt4ListSymbolsTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "list_symbols",
        "List MT4 symbols.",
        Schema("""{"type":"object","properties":{}}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var denied = RefuseIfReadDisabled();
        if (denied is not null) return denied;
        return await RouteAsync("mt4_list_symbols", args, ct).ConfigureAwait(false);
    }
}

public sealed class Mt4GetMarketDataTool : Mt4ToolBase
{
    public Mt4GetMarketDataTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "get_market_data",
        "Get MT4 market data for a symbol.",
        Schema("""{"type":"object","properties":{"symbol":{"type":"string"},"timeframe":{"type":"string"}},"required":["symbol"]}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var denied = RefuseIfReadDisabled();
        if (denied is not null) return denied;
        return await RouteAsync("mt4_get_market_data", args, ct).ConfigureAwait(false);
    }
}

public sealed class Mt4GetOpenPositionsTool : Mt4ToolBase
{
    public Mt4GetOpenPositionsTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "get_open_positions",
        "List open MT4 positions.",
        Schema("""{"type":"object","properties":{}}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var denied = RefuseIfReadDisabled();
        if (denied is not null) return denied;
        return await RouteAsync("mt4_get_open_positions", args, ct).ConfigureAwait(false);
    }
}

public sealed class Mt4VerifyTicketTool : Mt4ToolBase
{
    public Mt4VerifyTicketTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "verify_ticket",
        "Verify an MT4 ticket.",
        Schema("""{"type":"object","properties":{"ticket":{"type":"integer"}},"required":["ticket"]}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var denied = RefuseIfReadDisabled();
        if (denied is not null) return denied;
        return await RouteAsync("mt4_verify_ticket", args, ct).ConfigureAwait(false);
    }
}

public sealed class Mt4MarketwatchStatusTool : Mt4ToolBase
{
    public Mt4MarketwatchStatusTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "marketwatch_status",
        "MT4 Market Watch status.",
        Schema("""{"type":"object","properties":{}}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var denied = RefuseIfReadDisabled();
        if (denied is not null) return denied;
        return await RouteAsync("mt4_marketwatch_status", args, ct).ConfigureAwait(false);
    }
}

public sealed class Mt4ExportHistoryTool : Mt4ToolBase
{
    public Mt4ExportHistoryTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "export_history",
        "Export MT4 history for a date range.",
        Schema("""{"type":"object","properties":{"from":{"type":"string"},"to":{"type":"string"}},"required":["from","to"]}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var denied = RefuseIfReadDisabled();
        if (denied is not null) return denied;
        return await RouteAsync("mt4_export_history", args, ct).ConfigureAwait(false);
    }
}

public sealed class Mt4GetHistoricalBarsTool : Mt4ToolBase
{
    public Mt4GetHistoricalBarsTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "get_historical_bars",
        "Get historical MT4 bars.",
        Schema("""{"type":"object","properties":{"symbol":{"type":"string"},"timeframe":{"type":"string"},"count":{"type":"integer"}},"required":["symbol"]}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var denied = RefuseIfReadDisabled();
        if (denied is not null) return denied;
        return await RouteAsync("mt4_get_historical_bars", args, ct).ConfigureAwait(false);
    }
}

/// <summary>
/// Execute a trade — requires <c>sl</c>, master <c>AllowMt4Trade</c>, and
/// two-phase <c>confirmed=true</c> <b>before</b> Hermes MCP dispatch (AC #6).
/// </summary>
public sealed class Mt4ExecuteTradeTool : Mt4ToolBase
{
    public Mt4ExecuteTradeTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "execute_trade",
        "Execute an MT4 trade. Requires stop-loss (sl) and confirmed=true on the second call.",
        Schema("""{"type":"object","properties":{"symbol":{"type":"string"},"direction":{"type":"string"},"volume":{"type":"number"},"sl":{"type":"number"},"tp":{"type":"number"},"confirmed":{"type":"boolean"}},"required":["symbol","direction","volume","sl"]}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var tradeDenied = RefuseIfTradeDisabled();
        if (tradeDenied is not null) return tradeDenied;

        if (!HermesToolRouting.TryGetString(args, "symbol", out var symbol))
            return new ToolResult(false, "error: execute_trade requires 'symbol' (string).", null);
        if (!HermesToolRouting.TryGetString(args, "direction", out var direction))
            return new ToolResult(false, "error: execute_trade requires 'direction' (string).", null);

        double volume = 0;
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty("volume", out var volEl)
            && volEl.ValueKind == JsonValueKind.Number)
        {
            volume = volEl.GetDouble();
        }
        else
        {
            return new ToolResult(false, "error: execute_trade requires 'volume' (number).", null);
        }

        if (!TryGetSl(args, out var sl))
            return new ToolResult(false, "error: execute_trade requires a valid 'sl' (stop-loss) number.", null);

        if (!HermesToolRouting.IsConfirmed(args))
        {
            var prompt =
                $"confirm trade: {direction.ToUpperInvariant()} {volume.ToString(CultureInfo.InvariantCulture)} {symbol} at market (sl={sl.ToString(CultureInfo.InvariantCulture)})? reply yes to confirm";
            return new ToolResult(Success: false, Content: prompt, Data: new { needsConfirmation = true, symbol, direction, volume, sl });
        }

        return await RouteAsync("mt4_execute_trade", args, ct).ConfigureAwait(false);
    }

    private static bool TryGetSl(JsonElement args, out double sl)
    {
        sl = 0;
        if (args.ValueKind != JsonValueKind.Object) return false;
        if (!args.TryGetProperty("sl", out var el)) return false;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out sl) && sl != 0)
            return true;
        if (el.ValueKind == JsonValueKind.String
            && double.TryParse(el.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out sl)
            && sl != 0)
            return true;
        return false;
    }
}

public sealed class Mt4ClosePositionTool : Mt4ToolBase
{
    public Mt4ClosePositionTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "close_position",
        "Close an MT4 position. Requires confirmed=true on the second call.",
        Schema("""{"type":"object","properties":{"ticket":{"type":"integer"},"confirmed":{"type":"boolean"}},"required":["ticket"]}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var tradeDenied = RefuseIfTradeDisabled();
        if (tradeDenied is not null) return tradeDenied;

        if (!HermesToolRouting.TryGetInt(args, "ticket", out var ticket))
            return new ToolResult(false, "error: close_position requires 'ticket' (integer).", null);

        if (!HermesToolRouting.IsConfirmed(args))
        {
            return new ToolResult(
                Success: false,
                Content: $"confirm trade: CLOSE ticket {ticket}? reply yes to confirm",
                Data: new { needsConfirmation = true, ticket });
        }

        return await RouteAsync("mt4_close_position", args, ct).ConfigureAwait(false);
    }
}

public sealed class Mt4RunBacktestTool : Mt4ToolBase
{
    public Mt4RunBacktestTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "run_backtest",
        "Run an MT4 backtest. Requires confirmed=true on the second call.",
        Schema("""{"type":"object","properties":{"ea":{"type":"string"},"symbol":{"type":"string"},"from":{"type":"string"},"to":{"type":"string"},"confirmed":{"type":"boolean"}},"required":["ea","symbol","from","to"]}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var tradeDenied = RefuseIfTradeDisabled();
        if (tradeDenied is not null) return tradeDenied;

        HermesToolRouting.TryGetString(args, "ea", out var ea);
        HermesToolRouting.TryGetString(args, "symbol", out var symbol);

        if (!HermesToolRouting.IsConfirmed(args))
        {
            return new ToolResult(
                Success: false,
                Content: $"confirm trade: RUN BACKTEST {ea} on {symbol}? reply yes to confirm",
                Data: new { needsConfirmation = true, ea, symbol });
        }

        return await RouteAsync("mt4_run_backtest", args, ct).ConfigureAwait(false);
    }
}
