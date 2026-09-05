using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;
using SoulCore.Memory;

namespace SoulCore.Host;

/// <summary>
/// CLI mode: fill missing episodic embedding vectors via Ollama, then exit (no web host).
/// </summary>
internal static class BackfillEmbeddings
{
    public const int DefaultLimit = 200;

    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var limit = ParseLimit(args);
        var dbPathOverride = ParseDbPath(args);

        var contentRoot = FindHostContentRoot();
        var config = new ConfigurationBuilder()
            .SetBasePath(contentRoot)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables(prefix: "SOULCORE_")
            .Build();

        var memoryOpts = config.GetSection(MemoryOptions.SectionName).Get<MemoryOptions>() ?? new MemoryOptions();
        var inferenceOpts = config.GetSection(InferenceOptions.SectionName).Get<InferenceOptions>() ?? new InferenceOptions();

        var dbPath = string.IsNullOrWhiteSpace(dbPathOverride)
            ? memoryOpts.ResolveDbPath()
            : Path.GetFullPath(dbPathOverride);

        var model = string.IsNullOrWhiteSpace(inferenceOpts.EmbeddingModel)
            ? "nomic-embed-text"
            : inferenceOpts.EmbeddingModel.Trim();

        Console.WriteLine($"BACKFILL_EMBEDDINGS db={dbPath}");
        Console.WriteLine($"BACKFILL_EMBEDDINGS model={model} baseUrl={inferenceOpts.BaseUrl} limit={limit}");

        if (!inferenceOpts.Enabled || !inferenceOpts.EmbeddingsEnabled)
        {
            Console.WriteLine("BACKFILL_EMBEDDINGS FAIL: Inference.Enabled and EmbeddingsEnabled must be true");
            return 1;
        }

        await using var store = new SqliteMemoryStore(dbPath, NullLogger<SqliteMemoryStore>.Instance);
        Console.WriteLine($"BACKFILL_EMBEDDINGS open={store.IsDatabaseOpen}");

        using var http = new HttpClient
        {
            BaseAddress = NormalizeBaseUri(inferenceOpts.BaseUrl),
            Timeout = TimeSpan.FromSeconds(Math.Max(5, inferenceOpts.TimeoutSeconds))
        };

        var embeddings = new OllamaEmbeddingClient(
            http,
            Options.Create(inferenceOpts),
            NullLogger<OllamaEmbeddingClient>.Instance);

        // List once — LEFT JOIN excludes rows that already have vectors (idempotent).
        var missing = await store.ListEpisodicsMissingEmbeddingsAsync(limit, cancellationToken)
            .ConfigureAwait(false);

        var scanned = missing.Count;
        var filled = 0;
        var failed = 0;

        Console.WriteLine($"BACKFILL_EMBEDDINGS candidates={scanned}");

        foreach (var (id, content) in missing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var vector = await embeddings.EmbedAsync(content, cancellationToken).ConfigureAwait(false);
                if (vector.Length == 0)
                    throw new InvalidOperationException("Empty embedding vector returned.");

                await store.StoreEmbeddingAsync(id, vector, model, cancellationToken).ConfigureAwait(false);
                filled++;
                Console.WriteLine($"BACKFILL_EMBEDDINGS filled id={id} dims={vector.Length}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"BACKFILL_EMBEDDINGS failed id={id}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Console.WriteLine($"BACKFILL_EMBEDDINGS summary scanned={scanned} filled={filled} failed={failed}");
        // Non-zero only when every candidate failed (and there was work). Partial success is OK.
        return scanned > 0 && filled == 0 && failed > 0 ? 1 : 0;
    }

    private static int ParseLimit(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--limit", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[i + 1], out var n)
                && n > 0)
            {
                return n;
            }
        }

        return DefaultLimit;
    }

    private static string? ParseDbPath(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], "--db", StringComparison.OrdinalIgnoreCase))
                continue;
            var path = args[i + 1];
            if (!string.IsNullOrWhiteSpace(path) && !path.StartsWith('-'))
                return path;
        }

        return null;
    }

    private static string FindHostContentRoot()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            baseDir,
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..")),
            Directory.GetCurrentDirectory()
        };

        foreach (var c in candidates)
        {
            if (File.Exists(Path.Combine(c, "appsettings.json")))
                return c;
        }

        return Directory.GetCurrentDirectory();
    }

    private static Uri NormalizeBaseUri(string baseUrl)
    {
        var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/') + "/";
        return new Uri(trimmed, UriKind.Absolute);
    }
}
