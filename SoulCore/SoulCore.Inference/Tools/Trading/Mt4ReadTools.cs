using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Trading;

/// <summary><c>mt4_status</c> — connection + account status (read-gated).</summary>
public sealed class Mt4StatusTool : Mt4ToolBase
{
    public Mt4StatusTool(IMt4Bridge bridge, IOptions<ToolsOptions> options)
        : base(bridge, options)
    {
    }

    public override ToolDefinition Definition { get; } = new(
        Name: "mt4_status",
        Description: "Get MT4 / MetaTrader bridge connection and account status.",
        Parameters: Mt4ToolSupport.EmptyObjectSchema());

    public override Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default) =>
        ExecuteReadAsync(args, ct);
}

/// <summary><c>list_symbols</c> — tradeable symbols (read-gated).</summary>
public sealed class ListSymbolsTool : Mt4ToolBase
{
    public ListSymbolsTool(IMt4Bridge bridge, IOptions<ToolsOptions> options)
        : base(bridge, options)
    {
    }

    public override ToolDefinition Definition { get; } = new(
        Name: "list_symbols",
        Description: "List tradeable MT4 symbols available on the connected account.",
        Parameters: Mt4ToolSupport.EmptyObjectSchema());

    public override Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default) =>
        ExecuteReadAsync(args, ct);
}

/// <summary><c>get_market_data</c> — quote / candle snapshot (read-gated).</summary>
public sealed class GetMarketDataTool : Mt4ToolBase
{
    private static readonly JsonElement Schema = Mt4ToolSupport.ParseSchema(
        """
        {
          "type": "object",
          "properties": {
            "symbol": { "type": "string", "description": "Symbol, e.g. EURUSD." },
            "timeframe": { "type": "string", "description": "Timeframe, e.g. M1, M5, H1, D1." }
          },
          "required": ["symbol", "timeframe"]
        }
        """);

    public GetMarketDataTool(IMt4Bridge bridge, IOptions<ToolsOptions> options)
        : base(bridge, options)
    {
    }

    public override ToolDefinition Definition { get; } = new(
        Name: "get_market_data",
        Description: "Get current market data (quote / bar snapshot) for a symbol and timeframe.",
        Parameters: Schema);

    public override Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!Mt4ToolSupport.TryGetRequiredString(args, "symbol", out _, out var err))
            return Task.FromResult(err!);
        if (!Mt4ToolSupport.TryGetRequiredString(args, "timeframe", out _, out err))
            return Task.FromResult(err!);
        return ExecuteReadAsync(args, ct);
    }
}

/// <summary><c>get_open_positions</c> — open trades (read-gated).</summary>
public sealed class GetOpenPositionsTool : Mt4ToolBase
{
    public GetOpenPositionsTool(IMt4Bridge bridge, IOptions<ToolsOptions> options)
        : base(bridge, options)
    {
    }

    public override ToolDefinition Definition { get; } = new(
        Name: "get_open_positions",
        Description: "List currently open MT4 positions / orders.",
        Parameters: Mt4ToolSupport.EmptyObjectSchema());

    public override Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default) =>
        ExecuteReadAsync(args, ct);
}

/// <summary><c>verify_ticket</c> — look up a ticket (read-gated).</summary>
public sealed class VerifyTicketTool : Mt4ToolBase
{
    private static readonly JsonElement Schema = Mt4ToolSupport.ParseSchema(
        """
        {
          "type": "object",
          "properties": {
            "ticket": { "type": "integer", "description": "MT4 order/position ticket number." }
          },
          "required": ["ticket"]
        }
        """);

    public VerifyTicketTool(IMt4Bridge bridge, IOptions<ToolsOptions> options)
        : base(bridge, options)
    {
    }

    public override ToolDefinition Definition { get; } = new(
        Name: "verify_ticket",
        Description: "Verify an MT4 ticket exists and return its status.",
        Parameters: Schema);

    public override Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!Mt4ToolSupport.TryGetRequiredInt64(args, "ticket", out _, out var err))
            return Task.FromResult(err!);
        return ExecuteReadAsync(args, ct);
    }
}

/// <summary><c>marketwatch_status</c> — Market Watch panel state (read-gated).</summary>
public sealed class MarketWatchStatusTool : Mt4ToolBase
{
    public MarketWatchStatusTool(IMt4Bridge bridge, IOptions<ToolsOptions> options)
        : base(bridge, options)
    {
    }

    public override ToolDefinition Definition { get; } = new(
        Name: "marketwatch_status",
        Description: "Get MT4 Market Watch panel status (symbols watched, quotes).",
        Parameters: Mt4ToolSupport.EmptyObjectSchema());

    public override Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default) =>
        ExecuteReadAsync(args, ct);
}

/// <summary><c>export_history</c> — export trade history (read-gated).</summary>
public sealed class ExportHistoryTool : Mt4ToolBase
{
    private static readonly JsonElement Schema = Mt4ToolSupport.ParseSchema(
        """
        {
          "type": "object",
          "properties": {
            "from": { "type": "string", "description": "Range start (ISO-8601 or MT4 date)." },
            "to": { "type": "string", "description": "Range end (ISO-8601 or MT4 date)." }
          },
          "required": ["from", "to"]
        }
        """);

    public ExportHistoryTool(IMt4Bridge bridge, IOptions<ToolsOptions> options)
        : base(bridge, options)
    {
    }

    public override ToolDefinition Definition { get; } = new(
        Name: "export_history",
        Description: "Export MT4 account trade history for a date range.",
        Parameters: Schema);

    public override Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!Mt4ToolSupport.TryGetRequiredString(args, "from", out _, out var err))
            return Task.FromResult(err!);
        if (!Mt4ToolSupport.TryGetRequiredString(args, "to", out _, out err))
            return Task.FromResult(err!);
        return ExecuteReadAsync(args, ct);
    }
}

/// <summary><c>get_historical_bars</c> — OHLCV bars (read-gated).</summary>
public sealed class GetHistoricalBarsTool : Mt4ToolBase
{
    private static readonly JsonElement Schema = Mt4ToolSupport.ParseSchema(
        """
        {
          "type": "object",
          "properties": {
            "symbol": { "type": "string", "description": "Symbol, e.g. EURUSD." },
            "timeframe": { "type": "string", "description": "Timeframe, e.g. M15, H1." },
            "count": { "type": "integer", "description": "Number of bars to return." }
          },
          "required": ["symbol", "timeframe", "count"]
        }
        """);

    public GetHistoricalBarsTool(IMt4Bridge bridge, IOptions<ToolsOptions> options)
        : base(bridge, options)
    {
    }

    public override ToolDefinition Definition { get; } = new(
        Name: "get_historical_bars",
        Description: "Get historical OHLCV bars for a symbol/timeframe.",
        Parameters: Schema);

    public override Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!Mt4ToolSupport.TryGetRequiredString(args, "symbol", out _, out var err))
            return Task.FromResult(err!);
        if (!Mt4ToolSupport.TryGetRequiredString(args, "timeframe", out _, out err))
            return Task.FromResult(err!);
        if (!Mt4ToolSupport.TryGetRequiredInt64(args, "count", out var count, out err))
            return Task.FromResult(err!);
        if (count <= 0)
            return Task.FromResult(new ToolResult(false, "error: 'count' must be a positive integer", null));
        return ExecuteReadAsync(args, ct);
    }
}
