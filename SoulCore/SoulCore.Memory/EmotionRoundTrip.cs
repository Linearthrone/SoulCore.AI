using Microsoft.Extensions.Logging.Abstractions;

namespace SoulCore.Memory;

/// <summary>
/// Scripted evidence: write emotion → dispose store → reopen → read back.
/// Asserts <c>emotion_state.revision</c> increments on SetAsync.
/// </summary>
public static class EmotionRoundTrip
{
    public static async Task<int> RunAsync(string? dbPath = null, CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrWhiteSpace(dbPath)
            ? Path.Combine(
                Path.GetTempPath(),
                "SoulCore",
                "emotion-roundtrip",
                $"rt-{Guid.NewGuid():N}.db")
            : Path.GetFullPath(dbPath);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (File.Exists(path))
            File.Delete(path);

        var expected = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["valence"] = 0.42,
            ["arousal"] = 0.55,
            ["dominance"] = 0.61,
            ["focus"] = 0.77
        };

        Console.WriteLine($"EmotionRoundTrip db: {path}");

        long revisionBefore;
        long revisionAfter;
        await using (var store = new SqliteMemoryStore(path, NullLogger<SqliteMemoryStore>.Instance))
        {
            Console.WriteLine($"open1 IsDatabaseOpen={store.IsDatabaseOpen}");
            revisionBefore = await store.GetRevisionAsync(cancellationToken).ConfigureAwait(false);
            await store.SetAsync(expected, cancellationToken).ConfigureAwait(false);
            revisionAfter = await store.GetRevisionAsync(cancellationToken).ConfigureAwait(false);
            var mid = await store.GetAsync(cancellationToken).ConfigureAwait(false);
            Console.WriteLine(
                $"write-read same process: valence={mid["valence"]} arousal={mid["arousal"]} dominance={mid["dominance"]} focus={mid.GetValueOrDefault("focus")} revision_before={revisionBefore} revision_after={revisionAfter}");
        }

        var revisionOk = revisionAfter == revisionBefore + 1;
        Console.WriteLine(
            $"revision_check: before={revisionBefore} after={revisionAfter} revision_ok={revisionOk.ToString().ToLowerInvariant()}");

        // Process-boundary simulation: new connection / new store instance after dispose.
        await using (var store2 = new SqliteMemoryStore(path, NullLogger<SqliteMemoryStore>.Instance))
        {
            var loaded = await store2.GetAsync(cancellationToken).ConfigureAwait(false);
            var revisionReopen = await store2.GetRevisionAsync(cancellationToken).ConfigureAwait(false);
            Console.WriteLine(
                $"after reopen: valence={loaded["valence"]} arousal={loaded["arousal"]} dominance={loaded["dominance"]} focus={loaded.GetValueOrDefault("focus")} revision={revisionReopen} revision_ok={revisionOk.ToString().ToLowerInvariant()}");

            var valuesPass =
                NearlyEqual(loaded["valence"], expected["valence"])
                && NearlyEqual(loaded["arousal"], expected["arousal"])
                && NearlyEqual(loaded["dominance"], expected["dominance"])
                && NearlyEqual(loaded.GetValueOrDefault("focus"), expected["focus"]);

            var pass = valuesPass && revisionOk && revisionReopen == revisionAfter;

            if (!revisionOk)
                Console.WriteLine($"FAIL: revision did not increment (before={revisionBefore}, after={revisionAfter})");
            else if (revisionReopen != revisionAfter)
                Console.WriteLine($"FAIL: revision mismatch after reopen (expected={revisionAfter}, got={revisionReopen})");
            else if (!valuesPass)
                Console.WriteLine("FAIL: emotion mismatch after reopen");
            else
                Console.WriteLine("PASS: emotion persisted across store restart");

            return pass ? 0 : 1;
        }
    }

    private static bool NearlyEqual(double a, double b) => Math.Abs(a - b) < 1e-9;
}
