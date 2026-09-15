using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;
using SoulCore.Inference.Tools.Trading;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// BED-138 — MT4 tools: AllowMt4Read / AllowMt4Trade master gates, per-trade
/// two-phase confirm, SL required, and registry registration. Uses a recording
/// <see cref="IMt4Bridge"/> so we can prove no backend invoke occurs when a
/// gate is closed.
/// </summary>
public class Mt4ToolsTests
{
    private static readonly string[] AllToolNames =
    {
        "mt4_status",
        "list_symbols",
        "get_market_data",
        "get_open_positions",
        "execute_trade",
        "close_position",
        "verify_ticket",
        "marketwatch_status",
        "export_history",
        "get_historical_bars",
        "run_backtest"
    };

    // ── Registry ────────────────────────────────────────────────────────────

    [Fact]
    public void AllElevenTools_AppearInToolRegistry_GetDefinitions()
    {
        var bridge = new RecordingMt4Bridge();
        var options = Options.Create(new ToolsOptions
        {
            AllowMt4Read = true,
            AllowMt4Trade = true,
            Mt4Backend = "hermes"
        });

        var services = new ServiceCollection();
        services.AddSingleton<IMt4Bridge>(bridge);
        services.AddSingleton(options);
        foreach (var t in CreateAllTools(bridge, options))
            services.AddSingleton<ITool>(t);
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IToolRegistry>();
        var names = registry.GetDefinitions().Select(d => d.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var expected in AllToolNames)
            Assert.Contains(expected, names);
    }

    [Fact]
    public void ToMcpName_PrefixesNonMt4Names()
    {
        Assert.Equal("mt4_status", Mt4ToolSupport.ToMcpName("mt4_status"));
        Assert.Equal("mt4_list_symbols", Mt4ToolSupport.ToMcpName("list_symbols"));
        Assert.Equal("mt4_execute_trade", Mt4ToolSupport.ToMcpName("execute_trade"));
    }

    // ── Read gate ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("mt4_status", "{}")]
    [InlineData("list_symbols", "{}")]
    [InlineData("get_market_data", """{"symbol":"EURUSD","timeframe":"H1"}""")]
    [InlineData("get_open_positions", "{}")]
    [InlineData("verify_ticket", """{"ticket":12345}""")]
    [InlineData("marketwatch_status", "{}")]
    [InlineData("export_history", """{"from":"2026-01-01","to":"2026-01-31"}""")]
    [InlineData("get_historical_bars", """{"symbol":"EURUSD","timeframe":"M15","count":10}""")]
    public async Task ReadTools_AllowMt4ReadFalse_RefuseAndDoNotInvokeBridge(
        string toolName,
        string argsJson)
    {
        var bridge = new RecordingMt4Bridge();
        var tool = CreateTool(toolName, bridge, allowRead: false, allowTrade: false);

        var result = await tool.ExecuteAsync(Parse(argsJson));

        Assert.False(result.Success);
        Assert.Contains("AllowMt4Read", result.Content, StringComparison.Ordinal);
        Assert.Empty(bridge.Calls);
    }

    [Theory]
    [InlineData("mt4_status", "{}", "mt4_status")]
    [InlineData("list_symbols", "{}", "mt4_list_symbols")]
    [InlineData("get_market_data", """{"symbol":"EURUSD","timeframe":"H1"}""", "mt4_get_market_data")]
    [InlineData("get_open_positions", "{}", "mt4_get_open_positions")]
    [InlineData("verify_ticket", """{"ticket":99}""", "mt4_verify_ticket")]
    [InlineData("marketwatch_status", "{}", "mt4_marketwatch_status")]
    [InlineData("export_history", """{"from":"2026-01-01","to":"2026-01-31"}""", "mt4_export_history")]
    [InlineData("get_historical_bars", """{"symbol":"EURUSD","timeframe":"M15","count":10}""", "mt4_get_historical_bars")]
    public async Task ReadTools_AllowMt4ReadTrue_DispatchToBridge(
        string toolName,
        string argsJson,
        string expectedMcp)
    {
        var bridge = new RecordingMt4Bridge { NextResult = new ToolResult(true, "ok", null) };
        var tool = CreateTool(toolName, bridge, allowRead: true, allowTrade: false);

        var result = await tool.ExecuteAsync(Parse(argsJson));

        Assert.True(result.Success);
        Assert.Equal("ok", result.Content);
        Assert.Single(bridge.Calls);
        Assert.Equal(expectedMcp, bridge.Calls[0].McpName);
    }

    // ── Trade confirm two-phase ─────────────────────────────────────────────

    [Fact]
    public async Task ExecuteTrade_Phase1_WithoutConfirmed_ReturnsPrompt_NoBridgeCall()
    {
        var bridge = new RecordingMt4Bridge();
        var tool = CreateTool("execute_trade", bridge, allowRead: false, allowTrade: true);

        var result = await tool.ExecuteAsync(Parse(
            """{"symbol":"EURUSD","direction":"BUY","volume":0.1,"sl":1.0500}"""));

        Assert.False(result.Success);
        Assert.Contains("confirm trade:", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BUY", result.Content, StringComparison.Ordinal);
        Assert.Contains("0.1", result.Content, StringComparison.Ordinal);
        Assert.Contains("EURUSD", result.Content, StringComparison.Ordinal);
        Assert.Contains("reply yes to confirm", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(bridge.Calls);
    }

    [Fact]
    public async Task ExecuteTrade_Phase2_ConfirmedTrue_DispatchesToBridge()
    {
        var bridge = new RecordingMt4Bridge { NextResult = new ToolResult(true, "ticket=42", null) };
        var tool = CreateTool("execute_trade", bridge, allowRead: false, allowTrade: true);

        var result = await tool.ExecuteAsync(Parse(
            """{"symbol":"EURUSD","direction":"buy","volume":0.1,"sl":1.0500,"tp":1.0800,"confirmed":true}"""));

        Assert.True(result.Success);
        Assert.Equal("ticket=42", result.Content);
        Assert.Single(bridge.Calls);
        Assert.Equal("mt4_execute_trade", bridge.Calls[0].McpName);
        // confirmed must be stripped before MCP dispatch
        Assert.False(bridge.Calls[0].Args.TryGetProperty("confirmed", out _));
        Assert.Equal("EURUSD", bridge.Calls[0].Args.GetProperty("symbol").GetString());
    }

    [Fact]
    public async Task ClosePosition_Phase1_WithoutConfirmed_ReturnsPrompt_NoBridgeCall()
    {
        var bridge = new RecordingMt4Bridge();
        var tool = CreateTool("close_position", bridge, allowRead: false, allowTrade: true);

        var result = await tool.ExecuteAsync(Parse("""{"ticket":12345}"""));

        Assert.False(result.Success);
        Assert.Contains("confirm close position", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("12345", result.Content, StringComparison.Ordinal);
        Assert.Empty(bridge.Calls);
    }

    [Fact]
    public async Task ClosePosition_Phase2_ConfirmedTrue_DispatchesToBridge()
    {
        var bridge = new RecordingMt4Bridge { NextResult = new ToolResult(true, "closed", null) };
        var tool = CreateTool("close_position", bridge, allowRead: false, allowTrade: true);

        var result = await tool.ExecuteAsync(Parse("""{"ticket":12345,"confirmed":true}"""));

        Assert.True(result.Success);
        Assert.Single(bridge.Calls);
        Assert.Equal("mt4_close_position", bridge.Calls[0].McpName);
        Assert.False(bridge.Calls[0].Args.TryGetProperty("confirmed", out _));
    }

    [Fact]
    public async Task RunBacktest_Phase1_WithoutConfirmed_ReturnsPrompt_NoBridgeCall()
    {
        var bridge = new RecordingMt4Bridge();
        var tool = CreateTool("run_backtest", bridge, allowRead: false, allowTrade: true);

        var result = await tool.ExecuteAsync(Parse(
            """{"ea":"MyEA","symbol":"EURUSD","from":"2025-01-01","to":"2025-06-01"}"""));

        Assert.False(result.Success);
        Assert.Contains("confirm backtest", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(bridge.Calls);
    }

    [Fact]
    public async Task RunBacktest_Phase2_ConfirmedTrue_DispatchesToBridge()
    {
        var bridge = new RecordingMt4Bridge { NextResult = new ToolResult(true, "pf=1.2", null) };
        var tool = CreateTool("run_backtest", bridge, allowRead: false, allowTrade: true);

        var result = await tool.ExecuteAsync(Parse(
            """{"ea":"MyEA","symbol":"EURUSD","from":"2025-01-01","to":"2025-06-01","confirmed":true}"""));

        Assert.True(result.Success);
        Assert.Single(bridge.Calls);
        Assert.Equal("mt4_run_backtest", bridge.Calls[0].McpName);
    }

    // ── SL required ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("""{"symbol":"EURUSD","direction":"BUY","volume":0.1}""")]
    [InlineData("""{"symbol":"EURUSD","direction":"BUY","volume":0.1,"sl":null}""")]
    [InlineData("""{"symbol":"EURUSD","direction":"BUY","volume":0.1,"sl":0}""")]
    [InlineData("""{"symbol":"EURUSD","direction":"BUY","volume":0.1,"sl":-1.0}""")]
    [InlineData("""{"symbol":"EURUSD","direction":"BUY","volume":0.1,"sl":"nope"}""")]
    public async Task ExecuteTrade_MissingOrInvalidSl_Rejects_NoBridgeCall(string argsJson)
    {
        var bridge = new RecordingMt4Bridge();
        var tool = CreateTool("execute_trade", bridge, allowRead: false, allowTrade: true);

        var result = await tool.ExecuteAsync(Parse(argsJson));

        Assert.False(result.Success);
        Assert.Contains("sl required", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(bridge.Calls);
    }

    [Fact]
    public async Task ExecuteTrade_MissingSl_EvenWithConfirmedTrue_Rejects_NoBridgeCall()
    {
        var bridge = new RecordingMt4Bridge();
        var tool = CreateTool("execute_trade", bridge, allowRead: false, allowTrade: true);

        var result = await tool.ExecuteAsync(Parse(
            """{"symbol":"EURUSD","direction":"BUY","volume":0.1,"confirmed":true}"""));

        Assert.False(result.Success);
        Assert.Contains("sl required", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(bridge.Calls);
    }

    // ── Master trade gate ───────────────────────────────────────────────────

    [Theory]
    [InlineData("execute_trade", """{"symbol":"EURUSD","direction":"BUY","volume":0.1,"sl":1.05,"confirmed":true}""")]
    [InlineData("close_position", """{"ticket":12345,"confirmed":true}""")]
    [InlineData("run_backtest", """{"ea":"MyEA","symbol":"EURUSD","from":"2025-01-01","to":"2025-06-01","confirmed":true}""")]
    public async Task WriteTools_AllowMt4TradeFalse_EvenConfirmed_Refuse_NoBridgeCall(
        string toolName,
        string argsJson)
    {
        var bridge = new RecordingMt4Bridge();
        var tool = CreateTool(toolName, bridge, allowRead: true, allowTrade: false);

        var result = await tool.ExecuteAsync(Parse(argsJson));

        Assert.False(result.Success);
        Assert.Contains("AllowMt4Trade", result.Content, StringComparison.Ordinal);
        Assert.Empty(bridge.Calls);
    }

    [Fact]
    public async Task Defaults_BothGatesClosed_RefuseReadAndTrade()
    {
        // Stock ToolsOptions: AllowMt4Read=false, AllowMt4Trade=false
        var bridge = new RecordingMt4Bridge();
        var options = Options.Create(new ToolsOptions());
        var status = new Mt4StatusTool(bridge, options);
        var trade = new ExecuteTradeTool(bridge, options);

        var r1 = await status.ExecuteAsync(Parse("{}"));
        var r2 = await trade.ExecuteAsync(Parse(
            """{"symbol":"EURUSD","direction":"BUY","volume":0.1,"sl":1.05,"confirmed":true}"""));

        Assert.False(r1.Success);
        Assert.Contains("AllowMt4Read", r1.Content, StringComparison.Ordinal);
        Assert.False(r2.Success);
        Assert.Contains("AllowMt4Trade", r2.Content, StringComparison.Ordinal);
        Assert.Empty(bridge.Calls);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static ITool CreateTool(
        string name,
        IMt4Bridge bridge,
        bool allowRead,
        bool allowTrade)
    {
        var options = Options.Create(new ToolsOptions
        {
            AllowMt4Read = allowRead,
            AllowMt4Trade = allowTrade,
            Mt4Backend = "hermes"
        });
        return CreateAllTools(bridge, options)
            .Single(t => string.Equals(t.Definition.Name, name, StringComparison.Ordinal));
    }

    private static IEnumerable<ITool> CreateAllTools(IMt4Bridge bridge, IOptions<ToolsOptions> options)
    {
        yield return new Mt4StatusTool(bridge, options);
        yield return new ListSymbolsTool(bridge, options);
        yield return new GetMarketDataTool(bridge, options);
        yield return new GetOpenPositionsTool(bridge, options);
        yield return new ExecuteTradeTool(bridge, options);
        yield return new ClosePositionTool(bridge, options);
        yield return new VerifyTicketTool(bridge, options);
        yield return new MarketWatchStatusTool(bridge, options);
        yield return new ExportHistoryTool(bridge, options);
        yield return new GetHistoricalBarsTool(bridge, options);
        yield return new RunBacktestTool(bridge, options);
    }

    private sealed class RecordingMt4Bridge : IMt4Bridge
    {
        private readonly ConcurrentQueue<(string McpName, JsonElement Args)> _calls = new();

        public ToolResult NextResult { get; set; } = new(true, "ok", null);

        public IReadOnlyList<(string McpName, JsonElement Args)> Calls => _calls.ToArray();

        public Task<ToolResult> InvokeAsync(string mcpToolName, JsonElement args, CancellationToken ct = default)
        {
            _calls.Enqueue((mcpToolName, args.Clone()));
            return Task.FromResult(NextResult);
        }
    }
}
