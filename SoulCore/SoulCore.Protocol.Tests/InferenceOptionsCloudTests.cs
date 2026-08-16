using SoulCore.Config;

namespace SoulCore.Protocol.Tests;

public class InferenceOptionsCloudTests
{
    [Theory]
    [InlineData("https://ollama.com", true)]
    [InlineData("https://ollama.com/", true)]
    [InlineData("https://www.ollama.com/api", true)]
    [InlineData("http://ollama.com", false)]
    [InlineData("http://127.0.0.1:11434", false)]
    [InlineData("http://localhost:11434", false)]
    public void IsOllamaCloudUrl_DetectsHost(string url, bool expected)
    {
        Assert.Equal(expected, InferenceOptions.IsOllamaCloudUrl(url));
        Assert.Equal(expected, new InferenceOptions { BaseUrl = url }.IsCloudEndpoint);
    }

    [Fact]
    public void ResolveEmbeddingBaseUrl_DefaultsLocal_WhenChatIsCloud()
    {
        var opts = new InferenceOptions
        {
            BaseUrl = InferenceOptions.CloudBaseUrl,
            EmbeddingBaseUrl = ""
        };
        Assert.Equal("http://127.0.0.1:11434", opts.ResolveEmbeddingBaseUrl());
    }

    [Fact]
    public void ResolveEmbeddingBaseUrl_HonorsExplicitOverride()
    {
        var opts = new InferenceOptions
        {
            BaseUrl = InferenceOptions.CloudBaseUrl,
            EmbeddingBaseUrl = "http://192.168.1.10:11434"
        };
        Assert.Equal("http://192.168.1.10:11434", opts.ResolveEmbeddingBaseUrl());
    }

    [Fact]
    public void ResolveApiKey_EnvWinsOverConfig()
    {
        lock (typeof(InferenceOptionsCloudTests))
        {
            var previous = Environment.GetEnvironmentVariable(SecretNames.OllamaApiKey);
            try
            {
                Environment.SetEnvironmentVariable(SecretNames.OllamaApiKey, "env-key-value");
                var opts = new InferenceOptions { ApiKey = "config-key" };
                Assert.Equal("env-key-value", opts.ResolveApiKey());
            }
            finally
            {
                Environment.SetEnvironmentVariable(SecretNames.OllamaApiKey, previous);
            }
        }
    }

    [Fact]
    public void SecretNames_OllamaApiKey_IsDocumentedName()
    {
        Assert.Equal("SOULCORE_OLLAMA_API_KEY", SecretNames.OllamaApiKey);
    }
}
