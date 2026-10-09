using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference.Clients;
using Xunit;

namespace SoulCore.Protocol.Tests;

public class VisionSidecarTests
{
    [Fact]
    public void IsVisionSidecarConfigured_RequiresBaseAndModel()
    {
        Assert.False(new InferenceOptions().IsVisionSidecarConfigured);
        Assert.False(new InferenceOptions
        {
            VisionBaseUrl = "https://api.synthetic.new/openai/v1"
        }.IsVisionSidecarConfigured);
        Assert.True(new InferenceOptions
        {
            VisionBaseUrl = "https://api.synthetic.new/openai/v1",
            VisionModel = "syn:large:vision"
        }.IsVisionSidecarConfigured);
    }

    [Fact]
    public void ResolveVisionBaseUrl_AddsTrailingSlash()
    {
        var opts = new InferenceOptions
        {
            VisionBaseUrl = "https://api.synthetic.new/openai/v1"
        };
        Assert.Equal("https://api.synthetic.new/openai/v1/", opts.ResolveVisionBaseUrl());
    }

    [Fact]
    public void ResolveVisionApiKey_PrefersSoulCoreEnv()
    {
        lock (typeof(VisionSidecarTests))
        {
            var prevSoul = Environment.GetEnvironmentVariable(SecretNames.SyntheticApiKey);
            var prevAlias = Environment.GetEnvironmentVariable("SYNTHETIC_API_KEY");
            try
            {
                Environment.SetEnvironmentVariable(SecretNames.SyntheticApiKey, "soul-key");
                Environment.SetEnvironmentVariable("SYNTHETIC_API_KEY", "alias-key");
                var opts = new InferenceOptions { VisionApiKey = "config-key" };
                Assert.Equal("soul-key", opts.ResolveVisionApiKey());
            }
            finally
            {
                Environment.SetEnvironmentVariable(SecretNames.SyntheticApiKey, prevSoul);
                Environment.SetEnvironmentVariable("SYNTHETIC_API_KEY", prevAlias);
            }
        }
    }

    [Fact]
    public void SecretNames_SyntheticApiKey_IsDocumentedName()
    {
        Assert.Equal("SOULCORE_SYNTHETIC_API_KEY", SecretNames.SyntheticApiKey);
    }

    [Fact]
    public async Task DescribeScreenshot_PostsOpenAiCompatAndReturnsContent()
    {
        string? postedBody = null;
        string? auth = null;
        var handler = new StubHandler(async (req, ct) =>
        {
            auth = req.Headers.Authorization?.ToString();
            postedBody = await req.Content!.ReadAsStringAsync(ct);
            Assert.EndsWith("/chat/completions", req.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            var json = """
                {"choices":[{"message":{"role":"assistant","content":"Login button at center; email field above."}}]}
                """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.synthetic.new/openai/v1/")
        };
        var opts = Options.Create(new InferenceOptions
        {
            VisionBaseUrl = "https://api.synthetic.new/openai/v1",
            VisionModel = "syn:large:vision",
            VisionApiKey = "test-key"
        });
        var client = new OpenAiCompatVisionDescribeClient(
            http,
            opts,
            NullLogger<OpenAiCompatVisionDescribeClient>.Instance);

        Assert.True(client.IsConfigured);
        // Minimal fake JPEG SOI so LooksLikeJpegBase64 can run either way.
        var b64 = Convert.ToBase64String(Encoding.ASCII.GetBytes("not-a-real-image-but-long-enough"));
        var text = await client.DescribeScreenshotAsync("desktop_screenshot", new[] { b64 });

        Assert.Equal("Login button at center; email field above.", text);
        Assert.Equal("Bearer test-key", auth);
        Assert.NotNull(postedBody);
        using var doc = JsonDocument.Parse(postedBody!);
        Assert.Equal("syn:large:vision", doc.RootElement.GetProperty("model").GetString());
        Assert.Contains("image_url", postedBody!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DescribeScreenshot_ReturnsNull_WhenNotConfigured()
    {
        var http = new HttpClient { BaseAddress = new Uri("https://api.synthetic.new/openai/v1/") };
        var client = new OpenAiCompatVisionDescribeClient(
            http,
            Options.Create(new InferenceOptions()),
            NullLogger<OpenAiCompatVisionDescribeClient>.Instance);
        Assert.False(client.IsConfigured);
        Assert.Null(await client.DescribeScreenshotAsync("desktop_screenshot", new[] { "abc" }));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _fn;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> fn) =>
            _fn = fn;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            _fn(request, cancellationToken);
    }
}
