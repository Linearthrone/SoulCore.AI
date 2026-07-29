using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Hermes;
using SoulCore.Inference;
using SoulCore.Inference.Tools.Browser;
using SoulCore.Inference.Tools.Desktop;
using SoulCore.Inference.Tools.Trading;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// BED-144 integration tests: mock Hermes HTTP, verify desktop / browser / mt4
/// tools route through <see cref="IHermesMcpInvoker.CallMcpToolAsync"/> and
/// translate server-side tool_execution content into <see cref="ToolResult"/>.
/// Updated for gate+backend ctor shape (Wave 27 DI).
/// </summary>
public class HermesMcpRoutingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static HermesOptions MakeHermesOptions() => new()
    {
        Enabled = true,
        BaseUrl = "http://127.0.0.1:8642",
        Model = "gemma4:64k",
        MaxTokens = 256,
        ApiKey = "test-key",
        ToolChoice = "auto"
    };

    private static InferenceOptions MakeInferenceOptions() => new()
    {
        Enabled = true,
        BaseUrl = "http://127.0.0.1:11434",
        Model = "test-model",
        MaxToolIterations = 8,
        MaxTokens = 128,
        NumCtx = 65536,
        ThinkEnabled = false
    };

    private static HermesHttpClient MakeClient(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:8642/")
        };
        var logger = new LoggerFactory().CreateLogger<HermesHttpClient>();
        return new HermesHttpClient(
            http,
            Options.Create(MakeHermesOptions()),
            Options.Create(MakeInferenceOptions()),
            logger);
    }

    private static ToolsOptions MakeToolsOptions(
        string desktop = "hermes",
        string browser = "hermes",
        string mt4 = "hermes",
        bool allowMt4Read = true,
        bool allowMt4Trade = true,
        bool allowComputerControl = true) => new()
    {
        AllowDesktopCapture = true,
        AllowComputerControl = allowComputerControl,
        DesktopBackend = desktop,
        AllowBrowserCapture = true,
        BrowserBackend = browser,
        AllowMt4Read = allowMt4Read,
        AllowMt4Trade = allowMt4Trade,
        Mt4Backend = mt4
    };

    private static DesktopScreenshotTool MakeDesktopShot(HermesHttpClient hermes, IOptions<ToolsOptions> opts)
    {
        var gate = new ComputerControlGate(opts.Value.AllowDesktopCapture, opts.Value.AllowComputerControl);
        return new DesktopScreenshotTool(gate, new HermesDesktopControlBackend(hermes));
    }

    private static DesktopClickTool MakeDesktopClick(HermesHttpClient hermes, IOptions<ToolsOptions> opts)
    {
        var gate = new ComputerControlGate(opts.Value.AllowDesktopCapture, opts.Value.AllowComputerControl);
        return new DesktopClickTool(gate, new HermesDesktopControlBackend(hermes));
    }

    private static BrowserCaptureTabTool MakeBrowserCapture(HermesHttpClient hermes, IOptions<ToolsOptions> opts)
        => new(new HermesBrowserBridge(hermes), opts);

    private static Mt4StatusTool MakeMt4Status(HermesHttpClient hermes, IOptions<ToolsOptions> opts)
        => new(new HermesMt4Bridge(hermes), opts);

    private static ExecuteTradeTool MakeExecuteTrade(HermesHttpClient hermes, IOptions<ToolsOptions> opts)
        => new(new HermesMt4Bridge(hermes), opts);

    private static string OpenAIContentResponse(string content) =>
        JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    finish_reason = "stop",
                    message = new
                    {
                        role = "assistant",
                        content,
                        tool_calls = (object?)null
                    }
                }
            }
        }, JsonOptions);

    [Fact]
    public async Task DesktopScreenshot_HermesBackend_RoutesThroughComputerUse_Mcp()
    {
        var handler = new McpScriptedHandler(
            healthOk: true,
            chatBodies: new[]
            {
                OpenAIContentResponse("""{"success":true,"content":"screenshot saved: /tmp/desk.png"}""")
            });
        var hermes = MakeClient(handler);
        var tool = MakeDesktopShot(hermes, Options.Create(MakeToolsOptions()));

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"monitor":0}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Contains("screenshot", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, handler.ChatCallCount);
        Assert.NotNull(handler.LastChatBody);
        using var doc = JsonDocument.Parse(handler.LastChatBody!);
        Assert.True(doc.RootElement.TryGetProperty("tools", out var tools));
        Assert.Contains("computer_use", tools.GetRawText(), StringComparison.Ordinal);
        Assert.True(doc.RootElement.TryGetProperty("tool_choice", out var tc));
        Assert.Equal(JsonValueKind.Object, tc.ValueKind);
        Assert.Equal("computer_use", tc.GetProperty("function").GetProperty("name").GetString());
        Assert.Contains("\"action\"", handler.LastChatBody!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BrowserCaptureTab_HermesBackend_RoutesThroughBrowserBridge_Mcp()
    {
        var handler = new McpScriptedHandler(
            healthOk: true,
            chatBodies: new[]
            {
                OpenAIContentResponse("tab captured: /tmp/tab.png\n<html>ok</html>")
            });
        var hermes = MakeClient(handler);
        var tool = MakeBrowserCapture(hermes, Options.Create(MakeToolsOptions()));

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"tab":0}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Contains("tab captured", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(handler.LastChatBody);
        Assert.Contains("browser_bridge_capture_tab", handler.LastChatBody!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mt4Status_HermesBackend_RoutesThroughMt4Status_Mcp()
    {
        var handler = new McpScriptedHandler(
            healthOk: true,
            chatBodies: new[]
            {
                OpenAIContentResponse("""{"success":true,"content":"MT4 connected build=1380"}""")
            });
        var hermes = MakeClient(handler);
        var tool = MakeMt4Status(hermes, Options.Create(MakeToolsOptions(allowMt4Read: true)));

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Contains("MT4 connected", result.Content, StringComparison.Ordinal);
        Assert.NotNull(handler.LastChatBody);
        Assert.Contains("mt4_status", handler.LastChatBody!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HermesDown_AllThreeTools_ReturnUnavailable()
    {
        var handler = new McpScriptedHandler(healthOk: false, chatBodies: Array.Empty<string>());
        var hermes = MakeClient(handler);
        var opts = Options.Create(MakeToolsOptions(allowMt4Read: true));

        var desk = await MakeDesktopShot(hermes, opts)
            .ExecuteAsync(JsonDocument.Parse("{}").RootElement.Clone());
        var browser = await MakeBrowserCapture(hermes, opts)
            .ExecuteAsync(JsonDocument.Parse("{}").RootElement.Clone());
        var mt4 = await MakeMt4Status(hermes, opts)
            .ExecuteAsync(JsonDocument.Parse("{}").RootElement.Clone());

        Assert.False(desk.Success);
        Assert.False(browser.Success);
        Assert.False(mt4.Success);
        Assert.Equal(IHermesMcpInvoker.UnavailableMessage, desk.Content);
        Assert.Equal(IHermesMcpInvoker.UnavailableMessage, browser.Content);
        Assert.Equal(IHermesMcpInvoker.UnavailableMessage, mt4.Content);
        Assert.Equal(0, handler.ChatCallCount);
    }

    [Fact]
    public async Task NullHermesClient_CallMcpTool_ReturnsUnavailable()
    {
        var client = new NullHermesClient();
        var result = await client.CallMcpToolAsync(
            "computer_use",
            JsonDocument.Parse("{}").RootElement.Clone());

        Assert.False(result.Success);
        Assert.Equal(IHermesMcpInvoker.UnavailableMessage, result.Content);
    }

    [Fact]
    public async Task ExecuteTrade_WithoutConfirmed_DoesNotCallHermes()
    {
        var handler = new McpScriptedHandler(healthOk: true, chatBodies: Array.Empty<string>());
        var hermes = MakeClient(handler);
        var tool = MakeExecuteTrade(hermes, Options.Create(MakeToolsOptions(allowMt4Trade: true)));

        var result = await tool.ExecuteAsync(JsonDocument.Parse(
            """{"symbol":"EURUSD","direction":"buy","volume":0.1,"sl":1.05}""").RootElement.Clone());

        Assert.False(result.Success);
        Assert.Contains("confirm trade", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.HealthCallCount);
        Assert.Equal(0, handler.ChatCallCount);
    }

    [Fact]
    public async Task ExecuteTrade_MissingSl_Refuses_DoesNotCallHermes()
    {
        var handler = new McpScriptedHandler(healthOk: true, chatBodies: Array.Empty<string>());
        var hermes = MakeClient(handler);
        var tool = MakeExecuteTrade(hermes, Options.Create(MakeToolsOptions(allowMt4Trade: true)));

        var result = await tool.ExecuteAsync(JsonDocument.Parse(
            """{"symbol":"EURUSD","direction":"buy","volume":0.1,"confirmed":true}""").RootElement.Clone());

        Assert.False(result.Success);
        Assert.Contains("sl", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.ChatCallCount);
    }

    [Fact]
    public async Task ExecuteTrade_ConfirmedTrue_RoutesToHermesMt4ExecuteTrade()
    {
        var handler = new McpScriptedHandler(
            healthOk: true,
            chatBodies: new[]
            {
                OpenAIContentResponse("""{"success":true,"content":"ticket=1001 opened"}""")
            });
        var hermes = MakeClient(handler);
        var tool = MakeExecuteTrade(hermes, Options.Create(MakeToolsOptions(allowMt4Trade: true)));

        var result = await tool.ExecuteAsync(JsonDocument.Parse(
            """{"symbol":"EURUSD","direction":"buy","volume":0.1,"sl":1.05,"confirmed":true}""")
            .RootElement.Clone());

        Assert.True(result.Success);
        Assert.Contains("ticket=1001", result.Content, StringComparison.Ordinal);
        Assert.NotNull(handler.LastChatBody);
        Assert.Contains("mt4_execute_trade", handler.LastChatBody!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteTrade_AllowMt4TradeFalse_EvenConfirmed_Refuses()
    {
        var handler = new McpScriptedHandler(healthOk: true, chatBodies: Array.Empty<string>());
        var hermes = MakeClient(handler);
        var tool = MakeExecuteTrade(hermes, Options.Create(MakeToolsOptions(allowMt4Trade: false)));

        var result = await tool.ExecuteAsync(JsonDocument.Parse(
            """{"symbol":"EURUSD","direction":"buy","volume":0.1,"sl":1.05,"confirmed":true}""")
            .RootElement.Clone());

        Assert.False(result.Success);
        Assert.Contains("AllowMt4Trade", result.Content, StringComparison.Ordinal);
        Assert.Equal(0, handler.ChatCallCount);
    }

    [Fact]
    public async Task CallMcpToolAsync_ServerSideContent_TranslatesToToolResult()
    {
        var handler = new McpScriptedHandler(
            healthOk: true,
            chatBodies: new[]
            {
                OpenAIContentResponse("MCP result bytes=ok")
            });
        var hermes = MakeClient(handler);

        var result = await hermes.CallMcpToolAsync(
            "computer_use",
            JsonDocument.Parse("""{"action":"screenshot"}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Equal("MCP result bytes=ok", result.Content);
    }

    [Fact]
    public async Task DesktopClick_ControlGateClosed_DoesNotCallHermes()
    {
        var handler = new McpScriptedHandler(healthOk: true, chatBodies: Array.Empty<string>());
        var hermes = MakeClient(handler);
        var tool = MakeDesktopClick(hermes, Options.Create(MakeToolsOptions(allowComputerControl: false)));

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"x":10,"y":20}""").RootElement.Clone());

        Assert.False(result.Success);
        Assert.Contains("AllowComputerControl", result.Content, StringComparison.Ordinal);
        Assert.Equal(0, handler.ChatCallCount);
    }

    private sealed class McpScriptedHandler : HttpMessageHandler
    {
        private readonly bool _healthOk;
        private readonly Queue<string> _chatBodies;

        public int HealthCallCount { get; private set; }
        public int ChatCallCount { get; private set; }
        public string? LastChatBody { get; private set; }

        public McpScriptedHandler(bool healthOk, IEnumerable<string> chatBodies)
        {
            _healthOk = healthOk;
            _chatBodies = new Queue<string>(chatBodies);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            if (path.Contains("health", StringComparison.OrdinalIgnoreCase))
            {
                HealthCallCount++;
                return Task.FromResult(new HttpResponseMessage(
                    _healthOk ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent(_healthOk ? """{"ok":true}""" : "down")
                });
            }

            ChatCallCount++;
            LastChatBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();

            if (_chatBodies.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(OpenAIContentResponse("no more scripts"))
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_chatBodies.Dequeue())
            });
        }
    }
}
