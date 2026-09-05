using SoulCore.Config;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;

namespace SoulCore.Protocol.Tests;

public class InferenceModelRoutingTests
{
    [Fact]
    public void ResolveChatModel_UsesConfiguredModel()
    {
        var opts = new InferenceOptions { Model = "gemma4:latest" };
        Assert.Equal("gemma4:latest", InferenceModelRouting.ResolveChatModel(opts));
    }

    [Fact]
    public void ResolveToolModel_UeIdle_PrefersToolModel()
    {
        var opts = new InferenceOptions
        {
            Model = "gemma4:latest",
            ToolModel = "qwen2.5:14b",
            ToolModelUeLive = "tiny-tool"
        };
        Assert.Equal("qwen2.5:14b", InferenceModelRouting.ResolveToolModel(opts, ueLive: false));
    }

    [Fact]
    public void ResolveToolModel_UeLive_PrefersSmallFallback()
    {
        var opts = new InferenceOptions
        {
            Model = "gemma4:latest",
            ToolModel = "qwen2.5:14b",
            ToolModelUeLive = "tiny-tool"
        };
        Assert.Equal("tiny-tool", InferenceModelRouting.ResolveToolModel(opts, ueLive: true));
    }

    [Fact]
    public void ResolveToolNumCtx_UeLive_UsesSmallWindow()
    {
        var opts = new InferenceOptions { NumCtx = 16384, ToolNumCtxUeLive = 4096 };
        Assert.Equal(4096, InferenceModelRouting.ResolveToolNumCtx(opts, ueLive: true));
        Assert.Equal(16384, InferenceModelRouting.ResolveToolNumCtx(opts, ueLive: false));
    }

    [Fact]
    public void ShouldSkipEmbeddings_WhenUeLiveAndFlagSet()
    {
        var opts = new InferenceOptions { SkipEmbeddingsWhenUeLive = true };
        Assert.True(InferenceModelRouting.ShouldSkipEmbeddings(opts, ueLive: true));
        Assert.False(InferenceModelRouting.ShouldSkipEmbeddings(opts, ueLive: false));
    }

    [Fact]
    public void ResolveEmbeddingModel_UeLive_UsesUeLiveOverride()
    {
        var opts = new InferenceOptions
        {
            EmbeddingModel = "nomic-embed-text",
            EmbeddingModelUeLive = "tiny-embed"
        };
        Assert.Equal("tiny-embed", InferenceModelRouting.ResolveEmbeddingModel(opts, ueLive: true));
        Assert.Equal("nomic-embed-text", InferenceModelRouting.ResolveEmbeddingModel(opts, ueLive: false));
    }
}
