using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;
using SoulCore.Inference.Tools.Trading;

namespace SoulCore.Protocol.Tests;

/// <summary>BED-169 — LLMOD HTTP MT4 bridge: param mapping, transport failures.</summary>
public class LlmodHttpMt4BridgeTests
{
    [Fact]
    public void Mt4LlmodArgsMapper_ExecuteTrade_MapsDirectionSlTp()
    {
        using var doc = JsonDocument.Parse(
            """{"symbol":"EURUSD","direction":"SELL","volume":0.1,"sl":1.05,"tp":1.08}""");
        var mapped = Mt4LlmodArgsMapper.ToLlmodParameters("mt4_execute_trade", doc.RootElement);

        Assert.Equal(1, mapped["trade_type"]);
        Assert.Equal(0.1, mapped["volume"]);
        Assert.Equal("EURUSD", mapped["symbol"]);
        Assert.Equal(1.05, mapped["stop_loss"]);
        Assert.Equal(1.08, mapped["take_profit"]);
        Assert.False(mapped.ContainsKey("direction"));
        Assert.False(mapped.ContainsKey("sl"));
        Assert.False(mapped.ContainsKey("tp"));
    }

    [Fact]
    public void Mt4LlmodArgsMapper_RunBacktest_MapsEaFromTo()
    {
        using var doc = JsonDocument.Parse(
            """{"ea":"MyEA","symbol":"EURUSD","from":"2025-01-01","to":"2025-06-01"}""");
        var mapped = Mt4LlmodArgsMapper.ToLlmodParameters("mt4_run_backtest", doc.RootElement);

        Assert.Equal("MyEA", mapped["strategy_name"]);
        Assert.Equal("2025-01-01", mapped["start_date"]);
        Assert.Equal("2025-06-01", mapped["end_date"]);
    }

    [Fact]
    public void TranslateResponse_ParsesStringifiedData_AndInnerSuccess()
    {
        var body =
            """
            {
              "success": true,
              "message": "Command executed successfully",
              "data": "{\"success\":true,\"bridge_active\":true,\"message\":\"MT4 connected\",\"account\":{\"AccountNumber\":12345}}"
            }
            """;

        var result = LlmodHttpMt4Bridge.TranslateResponse("mt4_status", body);

        Assert.True(result.Success);
        Assert.Contains("MT4 connected", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_EndpointDown_ReturnsUnavailable()
    {
        var bridge = MakeBridge(new ThrowingHandler());
        using var doc = JsonDocument.Parse("{}");

        var result = await bridge.InvokeAsync("mt4_status", doc.RootElement);

        Assert.False(result.Success);
        Assert.Equal(LlmodHttpMt4Bridge.UnavailableMessage, result.Content);
    }

    [Fact]
    public async Task InvokeAsync_Http500_ReturnsUnavailable()
    {
        var bridge = MakeBridge(new StaticHandler(HttpStatusCode.InternalServerError, "boom"));
        using var doc = JsonDocument.Parse("{}");

        var result = await bridge.InvokeAsync("mt4_status", doc.RootElement);

        Assert.False(result.Success);
        Assert.Equal(LlmodHttpMt4Bridge.UnavailableMessage, result.Content);
    }

    [Fact]
    public async Task InvokeAsync_Success_PostsCommandWithMappedParams()
    {
        string? capturedBody = null;
        var handler = new CapturingHandler(async req =>
        {
            capturedBody = req.Content is null
                ? null
                : await req.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "success": true,
                      "message": "Command executed successfully",
                      "data": "{\"success\":true,\"message\":\"ticket=42\"}"
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var bridge = MakeBridge(handler);
        using var doc = JsonDocument.Parse(
            """{"symbol":"EURUSD","direction":"BUY","volume":0.1,"sl":1.05,"confirmed":true}""");

        var result = await bridge.InvokeAsync("mt4_execute_trade", doc.RootElement);

        Assert.True(result.Success);
        Assert.Contains("ticket=42", result.Content, StringComparison.Ordinal);
        Assert.NotNull(capturedBody);
        Assert.Contains("\"command\":\"mt4_execute_trade\"", capturedBody, StringComparison.Ordinal);
        Assert.Contains("\"trade_type\":0", capturedBody, StringComparison.Ordinal);
        Assert.Contains("\"stop_loss\":1.05", capturedBody, StringComparison.Ordinal);
        Assert.DoesNotContain("\"direction\"", capturedBody, StringComparison.Ordinal);
    }

    private static LlmodHttpMt4Bridge MakeBridge(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler);
        var options = Options.Create(new ToolsOptions
        {
            Mt4Backend = ToolsOptions.BackendLlmod,
            LlmodMcpEndpoint = "http://127.0.0.1:59999"
        });
        return new LlmodHttpMt4Bridge(http, options, NullLogger<LlmodHttpMt4Bridge>.Instance);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused");
    }

    private sealed class StaticHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;

        public CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            : this(req => Task.FromResult(responder(req)))
        {
        }

        public CapturingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) =>
            _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            _responder(request);
    }

}
