using SoulCore.Config;

namespace SoulCore.Inference.Tooling;

/// <summary>
/// Resolves chat / tool / embed Ollama model names from <see cref="InferenceOptions"/>
/// and whether Unreal is currently live (VRAM policy).
/// </summary>
public static class InferenceModelRouting
{
    public static string ResolveChatModel(InferenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return FirstNonEmpty(options.Model, "gemma4:latest");
    }

    /// <summary>
    /// Tool-loop model: <see cref="InferenceOptions.ToolModelUeLive"/> when UE live,
    /// else <see cref="InferenceOptions.ToolModel"/>, else chat model.
    /// </summary>
    public static string ResolveToolModel(InferenceOptions options, bool ueLive)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (ueLive && !string.IsNullOrWhiteSpace(options.ToolModelUeLive))
            return options.ToolModelUeLive.Trim();
        if (!string.IsNullOrWhiteSpace(options.ToolModel))
            return options.ToolModel.Trim();
        return ResolveChatModel(options);
    }

    public static string ResolveEmbeddingModel(InferenceOptions options, bool ueLive)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (ueLive && !string.IsNullOrWhiteSpace(options.EmbeddingModelUeLive))
            return options.EmbeddingModelUeLive.Trim();
        return FirstNonEmpty(options.EmbeddingModel, "nomic-embed-text");
    }

    /// <summary>
    /// Tool-loop <c>num_ctx</c>: prefer <see cref="InferenceOptions.ToolNumCtxUeLive"/> when UE live.
    /// </summary>
    public static int ResolveToolNumCtx(InferenceOptions options, bool ueLive)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (ueLive && options.ToolNumCtxUeLive > 0)
            return options.ToolNumCtxUeLive;
        return options.NumCtx;
    }

    public static bool ShouldSkipEmbeddings(InferenceOptions options, bool ueLive)
    {
        ArgumentNullException.ThrowIfNull(options);
        return ueLive && options.SkipEmbeddingsWhenUeLive;
    }

    private static string FirstNonEmpty(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
