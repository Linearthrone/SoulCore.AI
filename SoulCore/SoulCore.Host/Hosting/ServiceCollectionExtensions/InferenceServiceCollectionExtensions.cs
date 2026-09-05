using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference;
using SoulCore.Host.Inference;
using SoulCore.Host.Ws;

namespace SoulCore.Host.Hosting.ServiceCollectionExtensions;

internal static class InferenceServiceCollectionExtensions
{
    internal static IServiceCollection AddInference(
        this IServiceCollection services,
        InferenceOptions inferenceOptions)
    {
        if (inferenceOptions.Enabled)
        {
            if (inferenceOptions.IsCloudEndpoint && string.IsNullOrWhiteSpace(inferenceOptions.ResolveApiKey()))
            {
                Console.WriteLine(
                    "[SoulCore] BED-187: Inference BaseUrl is Ollama Cloud but SOULCORE_OLLAMA_API_KEY is missing — chat will 401 until set.");
            }

            services.AddHttpClient<OllamaInferenceClient>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<InferenceOptions>>().Value;
                OllamaHttpClientConfiguration.Configure(
                    client,
                    opts.BaseUrl,
                    opts.TimeoutSeconds,
                    InferenceOptions.IsOllamaCloudUrl(opts.BaseUrl) ? opts.ResolveApiKey() : null);
            });
            // BED-126: expose the typed client as IInferenceClient. The 3-arg ctor
            // (http + options + logger) is what HttpClientFactory builds; the
            // tool-loop CompleteWithToolsAsync accepts an IToolRegistry at call time
            // (resolved from the container by the caller, e.g. ChatWebSocketHandler),
            // so the client itself does not need the registry injected to function.
            services.AddTransient<IInferenceClient>(sp => sp.GetRequiredService<OllamaInferenceClient>());
        }
        else
        {
            services.AddSingleton<IInferenceClient, NullInferenceClient>();
        }

        var embeddingsOn = inferenceOptions.Enabled && inferenceOptions.EmbeddingsEnabled;
        if (embeddingsOn)
        {
            services.AddHttpClient<OllamaEmbeddingClient>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<InferenceOptions>>().Value;
                // BED-187: embeddings stay local by default when chat is on Ollama Cloud.
                var embedBase = opts.ResolveEmbeddingBaseUrl();
                var embedKey = InferenceOptions.IsOllamaCloudUrl(embedBase) ? opts.ResolveApiKey() : null;
                OllamaHttpClientConfiguration.Configure(client, embedBase, opts.TimeoutSeconds, embedKey);
            });
            services.AddTransient<IEmbeddingClient>(sp => sp.GetRequiredService<OllamaEmbeddingClient>());
        }
        else
        {
            services.AddSingleton<IEmbeddingClient, NullEmbeddingClient>();
        }

        // BED-158: in-memory per-sessionId chat/tool history for multi-turn pronouns.
        services.AddSingleton<IChatSessionHistoryStore>(sp =>
        {
            var max = sp.GetRequiredService<IOptions<ChatWsOptions>>().Value.MaxSessionHistoryMessages;
            if (max < 2) max = 40;
            return new ChatSessionHistoryStore(max);
        });

        // OllamaInferenceClient's DI ctor requires IUeLiveSignal. Without this, every
        // chat/WS turn throws and Victoria appears "dead" while /health still 200s.
        services.AddSingleton<IUeLiveSignal, UnrealUeLiveSignal>();

        return services;
    }
}
