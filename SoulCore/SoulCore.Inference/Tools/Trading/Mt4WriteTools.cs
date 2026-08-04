using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Inference.Tools.Trading;

/// <summary>
/// <c>execute_trade</c> — open a market order. Master <c>AllowMt4Trade</c> gate +
/// per-trade <c>confirmed=true</c> two-phase confirm + mandatory <c>sl</c> (BED-138).
/// Never auto-executes from a single model call.
/// </summary>
public sealed class ExecuteTradeTool : Mt4ToolBase
{
    private static readonly JsonElement Schema = Mt4ToolSupport.ParseSchema(
        """
        {
          "type": "object",
          "properties": {
            "symbol": { "type": "string", "description": "Symbol to trade, e.g. EURUSD." },
            "direction": { "type": "string", "description": "BUY or SELL." },
            "volume": { "type": "number", "description": "Lot size, e.g. 0.1." },
            "sl": { "type": "number", "description": "Stop-loss price or points (REQUIRED)." },
            "tp": { "type": "number", "description": "Optional take-profit." },
            "confirmed": { "type": "boolean", "description": "Must be true on the second call after user confirms.", "default": false }
          },
          "required": ["symbol", "direction", "volume", "sl"]
        }
        """);

    public ExecuteTradeTool(IMt4Bridge bridge, IOptions<ToolsOptions> options)
        : this(bridge, options, new ComputerControlGate(options))
    {
    }

    public ExecuteTradeTool(IMt4Bridge bridge, IOptions<ToolsOptions> options, IToolsAccessSettings access)
        : base(bridge, options, access)
    {
    }

    public override ToolDefinition Definition { get; } = new(
        Name: "execute_trade",
        Description: "Open an MT4 market trade. Requires stop-loss (sl). First call returns a confirmation prompt; only executes when confirmed=true after the user agrees.",
        Parameters: Schema);

    public override Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            return Task.FromResult(new ToolResult(
                false,
                "error: execute_trade expects a JSON object with symbol, direction, volume, sl.",
                null));
        }

        // SL is validated BEFORE the confirm gate so the model learns the
        // requirement on the first call (and never reaches the bridge).
        if (!Mt4ToolSupport.TryValidateStopLoss(args, out _, out var slErr))
            return Task.FromResult(slErr!);

        if (!Mt4ToolSupport.TryGetRequiredString(args, "symbol", out var symbol, out var err))
            return Task.FromResult(err!);
        if (!Mt4ToolSupport.TryGetRequiredString(args, "direction", out var direction, out err))
            return Task.FromResult(err!);
        if (!Mt4ToolSupport.TryGetRequiredDouble(args, "volume", out var volume, out err))
            return Task.FromResult(err!);
        if (volume <= 0)
            return Task.FromResult(new ToolResult(false, "error: 'volume' must be positive", null));

        var dirNorm = direction.Trim().ToUpperInvariant();
        if (dirNorm is not ("BUY" or "SELL"))
        {
            return Task.FromResult(new ToolResult(
                false,
                "error: 'direction' must be BUY or SELL",
                null));
        }

        var prompt = Mt4ToolSupport.BuildExecuteTradeConfirmPrompt(dirNorm, volume, symbol);
        return ExecuteWriteAsync(args, prompt, ct);
    }
}

/// <summary>
/// <c>close_position</c> — close by ticket. Master trade gate + per-trade confirm.
/// </summary>
public sealed class ClosePositionTool : Mt4ToolBase
{
    private static readonly JsonElement Schema = Mt4ToolSupport.ParseSchema(
        """
        {
          "type": "object",
          "properties": {
            "ticket": { "type": "integer", "description": "MT4 ticket to close." },
            "confirmed": { "type": "boolean", "description": "Must be true on the second call after user confirms.", "default": false }
          },
          "required": ["ticket"]
        }
        """);

    public ClosePositionTool(IMt4Bridge bridge, IOptions<ToolsOptions> options)
        : this(bridge, options, new ComputerControlGate(options))
    {
    }

    public ClosePositionTool(IMt4Bridge bridge, IOptions<ToolsOptions> options, IToolsAccessSettings access)
        : base(bridge, options, access)
    {
    }

    public override ToolDefinition Definition { get; } = new(
        Name: "close_position",
        Description: "Close an open MT4 position by ticket. First call returns a confirmation prompt; only executes when confirmed=true after the user agrees.",
        Parameters: Schema);

    public override Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!Mt4ToolSupport.TryGetRequiredInt64(args, "ticket", out var ticket, out var err))
            return Task.FromResult(err!);
        if (ticket <= 0)
            return Task.FromResult(new ToolResult(false, "error: 'ticket' must be a positive integer", null));

        var prompt = Mt4ToolSupport.BuildClosePositionConfirmPrompt(ticket);
        return ExecuteWriteAsync(args, prompt, ct);
    }
}

/// <summary>
/// <c>run_backtest</c> — run an EA backtest. Master trade gate + confirm
/// (treated as a write because it can mutate terminal state / consume resources).
/// </summary>
public sealed class RunBacktestTool : Mt4ToolBase
{
    private static readonly JsonElement Schema = Mt4ToolSupport.ParseSchema(
        """
        {
          "type": "object",
          "properties": {
            "ea": { "type": "string", "description": "Expert Advisor name." },
            "symbol": { "type": "string", "description": "Symbol to backtest." },
            "from": { "type": "string", "description": "Backtest range start." },
            "to": { "type": "string", "description": "Backtest range end." },
            "confirmed": { "type": "boolean", "description": "Must be true on the second call after user confirms.", "default": false }
          },
          "required": ["ea", "symbol", "from", "to"]
        }
        """);

    public RunBacktestTool(IMt4Bridge bridge, IOptions<ToolsOptions> options)
        : this(bridge, options, new ComputerControlGate(options))
    {
    }

    public RunBacktestTool(IMt4Bridge bridge, IOptions<ToolsOptions> options, IToolsAccessSettings access)
        : base(bridge, options, access)
    {
    }

    public override ToolDefinition Definition { get; } = new(
        Name: "run_backtest",
        Description: "Run an MT4 Expert Advisor backtest. First call returns a confirmation prompt; only executes when confirmed=true after the user agrees.",
        Parameters: Schema);

    public override Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!Mt4ToolSupport.TryGetRequiredString(args, "ea", out var ea, out var err))
            return Task.FromResult(err!);
        if (!Mt4ToolSupport.TryGetRequiredString(args, "symbol", out var symbol, out err))
            return Task.FromResult(err!);
        if (!Mt4ToolSupport.TryGetRequiredString(args, "from", out var from, out err))
            return Task.FromResult(err!);
        if (!Mt4ToolSupport.TryGetRequiredString(args, "to", out var to, out err))
            return Task.FromResult(err!);

        var prompt = Mt4ToolSupport.BuildBacktestConfirmPrompt(ea, symbol, from, to);
        return ExecuteWriteAsync(args, prompt, ct);
    }
}
