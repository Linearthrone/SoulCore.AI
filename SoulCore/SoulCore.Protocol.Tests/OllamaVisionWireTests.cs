using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;
using SoulCore.Protocol;

namespace SoulCore.Protocol.Tests;

public class OllamaVisionWireTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    [Fact]
    public async Task ScreenshotToolResult_AddsUserVisionFollowUp_WithImagesOnWire()
    {
        var png = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0, 0, 0 });
        var handler = new VisionScriptedHandler(new[]
        {
            OpenAiForceScreenshot(),
            ChatDone()
        });
        var registry = new ScriptedRegistry(
            ("desktop_screenshot", _ => new ToolResult(
                true,
                "captured 1280x800",
                new { bytes = Convert.FromBase64String(png), format = "png" })));
        var client = MakeClient(handler, registry);

        await client.CompleteWithToolsAsync(
            new List<ChatMessage> { new() { Role = "user", Content = "take a screenshot" } },
            new[] { ScreenshotToolDef() },
            registry,
            loopOptions: new ToolLoopOptions { ForceToolName = "desktop_screenshot" });

        Assert.True(handler.CallCount >= 2);
        var followUp = handler.CapturedRequests[1];
        Assert.Contains("api/chat", followUp.Path, StringComparison.Ordinal);
        var userVision = followUp.Messages.LastOrDefault(m =>
            m.Role == "user" && (m.Content ?? "").Contains("[Vision]", StringComparison.Ordinal));
        Assert.NotNull(userVision);
        Assert.NotNull(userVision!.Images);
        Assert.Single(userVision.Images!);
    }

    private static OllamaInferenceClient MakeClient(HttpMessageHandler handler, IToolRegistry registry)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        return new OllamaInferenceClient(
            http,
            Options.Create(new InferenceOptions
            {
                Enabled = true,
                BaseUrl = "http://127.0.0.1:11434",
                Model = "gemma4:latest",
                MaxToolIterations = 8,
                ThinkEnabled = false
            }),
            new LoggerFactory().CreateLogger<OllamaInferenceClient>(),
            registry);
    }

    private static ToolDefinition ScreenshotToolDef() => new(
        "desktop_screenshot",
        "shot",
        JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement.Clone());

    private static string OpenAiForceScreenshot() =>
        """{"choices":[{"message":{"role":"assistant","content":"","tool_calls":[{"function":{"name":"desktop_screenshot","arguments":"{}"}}]}}]}""";

    private static string ChatDone() =>
        """{"message":{"role":"assistant","content":"done","tool_calls":null}}""";

    private sealed class VisionScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;
        public int CallCount { get; private set; }
        public List<CapturedVisionRequest> CapturedRequests { get; } = new();

        public VisionScriptedHandler(IEnumerable<string> responses) =>
            _responses = new Queue<string>(responses);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var body = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            var messages = new List<CapturedVisionMessage>();
            if (body is not null)
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("messages", out var msgs))
                {
                    foreach (var m in msgs.EnumerateArray())
                    {
                        List<string>? images = null;
                        if (m.TryGetProperty("images", out var imgs) && imgs.ValueKind == JsonValueKind.Array)
                        {
                            images = imgs.EnumerateArray()
                                .Select(i => i.GetString() ?? "")
                                .Where(s => s.Length > 0)
                                .ToList();
                        }

                        messages.Add(new CapturedVisionMessage(
                            m.TryGetProperty("role", out var r) ? r.GetString() ?? "" : "",
                            m.TryGetProperty("content", out var c) ? c.GetString() : null,
                            images));
                    }
                }
            }

            CapturedRequests.Add(new CapturedVisionRequest(
                request.RequestUri?.AbsolutePath ?? "",
                messages));

            var json = _responses.Count > 0
                ? _responses.Dequeue()
                : """{"message":{"role":"assistant","content":"done","tool_calls":null}}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        }
    }

    private sealed record CapturedVisionMessage(string Role, string? Content, List<string>? Images);
    private sealed record CapturedVisionRequest(string Path, IReadOnlyList<CapturedVisionMessage> Messages);

    private sealed class ScriptedRegistry : IToolRegistry
    {
        private readonly Dictionary<string, Func<JsonElement, ToolResult>> _handlers;

        public ScriptedRegistry(params (string Name, Func<JsonElement, ToolResult>)[] handlers)
        {
            _handlers = new Dictionary<string, Func<JsonElement, ToolResult>>(StringComparer.Ordinal);
            foreach (var (name, fn) in handlers)
                _handlers[name] = fn;
        }

        public IReadOnlyList<ToolDefinition> GetDefinitions() => Array.Empty<ToolDefinition>();

        public Task<ToolResult> ExecuteAsync(string name, JsonElement args, CancellationToken ct = default)
        {
            if (_handlers.TryGetValue(name, out var fn))
                return Task.FromResult(fn(args));
            return Task.FromResult(new ToolResult(false, $"Unknown tool '{name}'.", null));
        }
    }
}
