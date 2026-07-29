using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Hermes;
using SoulCore.Inference;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// BED-144: <see cref="IHermesMcpInvoker.CallMcpToolAsync"/> recovers server-side
/// tool_execution content into <see cref="ToolResult"/>.
/// </summary>
public class HermesCallMcpToolTests
{
    [Fact]
    public async Task CallMcpToolAsync_ParsesJsonContent_AsSuccess()
    {
        var handler = new ScriptedHandler(
            healthOk: true,
            chatBody: """{"choices":[{"finish_reason":"stop","message":{"role":"assistant","content":"{\"success\":true,\"content\":\"ok-from-mcp\"}"}}]}""");
        var client = MakeClient(handler);

        var result = await client.CallMcpToolAsync(
            "mt4_status",
            JsonDocument.Parse("{}").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Contains("ok-from-mcp", result.Content, StringComparison.Ordinal);
        Assert.Equal(1, handler.ChatCount);
        Assert.Contains("mt4_status", handler.LastChat!, StringComparison.Ordinal);
        Assert.Contains("tool_choice", handler.LastChat!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallMcpToolAsync_HealthDown_ReturnsUnavailable()
    {
        var handler = new ScriptedHandler(healthOk: false, chatBody: null);
        var client = MakeClient(handler);

        var result = await client.CallMcpToolAsync(
            "computer_use",
            JsonDocument.Parse("{}").RootElement.Clone());

        Assert.False(result.Success);
        Assert.Equal(IHermesMcpInvoker.UnavailableMessage, result.Content);
        Assert.Equal(0, handler.ChatCount);
    }

    [Fact]
    public async Task NullHermesClient_CallMcpToolAsync_ReturnsUnavailable()
    {
        var client = new NullHermesClient();
        var result = await client.CallMcpToolAsync(
            "browser_bridge_health",
            JsonDocument.Parse("{}").RootElement.Clone());
        Assert.False(result.Success);
        Assert.Equal(IHermesMcpInvoker.UnavailableMessage, result.Content);
    }

    private static HermesHttpClient MakeClient(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:8642/") };
        return new HermesHttpClient(
            http,
            Options.Create(new HermesOptions
            {
                Enabled = true,
                BaseUrl = "http://127.0.0.1:8642",
                Model = "gemma4:64k",
                ApiKey = "test-key",
                MaxTokens = 256
            }),
            Options.Create(new InferenceOptions
            {
                Enabled = true,
                BaseUrl = "http://127.0.0.1:11434",
                Model = "test",
                MaxTokens = 128,
                NumCtx = 65536,
                MaxToolIterations = 8
            }),
            new LoggerFactory().CreateLogger<HermesHttpClient>());
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly bool _healthOk;
        private readonly string? _chatBody;
        public int ChatCount { get; private set; }
        public string? LastChat { get; private set; }

        public ScriptedHandler(bool healthOk, string? chatBody)
        {
            _healthOk = healthOk;
            _chatBody = chatBody;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.Contains("health", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(_healthOk ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent(_healthOk ? """{"status":"ok"}""" : "down", Encoding.UTF8, "application/json")
                };
            }

            ChatCount++;
            LastChat = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_chatBody ?? "{}", Encoding.UTF8, "application/json")
            };
        }
    }
}
