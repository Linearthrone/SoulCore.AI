using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Core;
using SoulCore.Core.Safety;
using SoulCore.Host.Loop;
using SoulCore.Memory;

namespace SoulCore.Host.Loop;

/// <summary>
/// CLI evidence for SoulLoop kill switch + richer want categories.
/// Flag off = no want; flag on = varied emotion/episodic scenarios + one tick LastWant.
/// </summary>
internal static class SoulLoopTickEvidence
{
    public static async Task<int> RunAsync(bool enabled)
    {
        var dbPath = Path.Combine(
            Path.GetTempPath(),
            "SoulCore",
            $"soulloop_tick_{Guid.NewGuid():N}.db");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        await using var store = new SqliteMemoryStore(dbPath);
        var hub = new PresenceWsHub(NullLogger<PresenceWsHub>.Instance);
        var driftWatcher = new DriftWatcher(sloMinutes: 15);
        var options = Options.Create(new SoulLoopOptions
        {
            Enabled = enabled,
            EpisodicRecallLimit = 3
        });

        Console.WriteLine($"SOULLOOP_EVIDENCE enabled={enabled}");
        Console.WriteLine($"SOULLOOP_EVIDENCE db={dbPath}");

        if (!enabled)
        {
            var loopOff = new SoulLoopScaffold(
                store,
                store,
                hub,
                options,
                driftWatcher,
                NullLogger<SoulLoopScaffold>.Instance);

            Console.WriteLine($"SOULLOOP_EVIDENCE IsEnabled={loopOff.IsEnabled}");
            await loopOff.TickAsync().ConfigureAwait(false);
            var lastOff = loopOff.LastWant;
            Console.WriteLine($"SOULLOOP_EVIDENCE LastWant={(lastOff is null ? "(null)" : lastOff)}");

            if (lastOff is not null)
            {
                Console.WriteLine("SOULLOOP_EVIDENCE FAIL: expected no want when disabled");
                return 1;
            }

            Console.WriteLine("SOULLOOP_EVIDENCE PASS: disabled → no tick work");
            return 0;
        }

        // Unit-style matrix (no Host): emotion + episodic → distinct categories / phrases.
        if (!RunWantMatrix())
            return 1;

        await store.WriteEpisodicAsync(
            "BED-076 evidence episodic: quiet morning at the desk; remember earlier chat.",
            "system").ConfigureAwait(false);

        await store.SetAsync(new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["valence"] = 0.10,
            ["arousal"] = 0.20,
            ["dominance"] = 0.40,
            ["focus"] = 0.55
        }).ConfigureAwait(false);

        var loop = new SoulLoopScaffold(
            store,
            store,
            hub,
            options,
            driftWatcher,
            NullLogger<SoulLoopScaffold>.Instance);

        Console.WriteLine($"SOULLOOP_EVIDENCE IsEnabled={loop.IsEnabled}");
        await loop.TickAsync().ConfigureAwait(false);

        var last = loop.LastWant;
        Console.WriteLine($"SOULLOOP_EVIDENCE LastWant={(last is null ? "(null)" : last)}");

        if (string.IsNullOrWhiteSpace(last)
            || !last.StartsWith("want[", StringComparison.Ordinal)
            || !last.Contains("[recall]", StringComparison.Ordinal))
        {
            Console.WriteLine("SOULLOOP_EVIDENCE FAIL: expected categorized recall want when enabled");
            return 1;
        }

        Console.WriteLine("SOULLOOP_EVIDENCE PASS: enabled → richer categorized want emitted");
        return 0;
    }

    private static bool RunWantMatrix()
    {
        var empty = Array.Empty<string>();
        var desk = new[] { "quiet morning at the desk" };
        var correction = new[] { "correction: user said that was wrong actually" };
        var remember = new[] { "remember earlier: we talked about the soak" };

        var cases = new (string name, string label, EmotionInfluencePrompt.EmotionFields fields, IReadOnlyList<string> recent, string expectCat)[]
        {
            ("calm-empty", "calm", new(0.0, 0.10, 0.3, 0.3), empty, SoulLoopWantProposal.CategoryReflect),
            ("tense", "tense", new(-0.5, 0.70, 0.2, 0.4), desk, SoulLoopWantProposal.CategorySettle),
            ("low", "low", new(-0.6, 0.15, 0.2, 0.2), desk, SoulLoopWantProposal.CategoryReconnect),
            ("content", "content", new(0.5, 0.30, 0.5, 0.4), desk, SoulLoopWantProposal.CategorySavor),
            ("excited", "excited", new(0.8, 0.75, 0.6, 0.5), desk, SoulLoopWantProposal.CategoryEngage),
            ("clarify", "calm", new(0.0, 0.15, 0.4, 0.4), correction, SoulLoopWantProposal.CategoryClarify),
            ("recall", "neutral", new(0.0, 0.30, 0.4, 0.4), remember, SoulLoopWantProposal.CategoryRecall),
            ("focus-engage", "calm", new(0.0, 0.15, 0.5, 0.80), empty, SoulLoopWantProposal.CategoryEngage),
        };

        var wants = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in cases)
        {
            var cat = SoulLoopWantProposal.Classify(c.label, c.fields, c.recent);
            var want = SoulLoopWantProposal.Propose(c.label, c.fields, c.recent);
            Console.WriteLine($"SOULLOOP_EVIDENCE matrix[{c.name}] category={cat}");
            Console.WriteLine($"SOULLOOP_EVIDENCE matrix[{c.name}] want={want}");

            if (!string.Equals(cat, c.expectCat, StringComparison.Ordinal))
            {
                Console.WriteLine($"SOULLOOP_EVIDENCE FAIL: {c.name} expected category={c.expectCat} got={cat}");
                return false;
            }

            if (!want.StartsWith($"want[{c.expectCat}]:", StringComparison.Ordinal))
            {
                Console.WriteLine($"SOULLOOP_EVIDENCE FAIL: {c.name} want missing category prefix");
                return false;
            }

            wants.Add(want);
        }

        if (wants.Count < 6)
        {
            Console.WriteLine($"SOULLOOP_EVIDENCE FAIL: expected varied wants, unique={wants.Count}");
            return false;
        }

        Console.WriteLine($"SOULLOOP_EVIDENCE matrix PASS uniqueWants={wants.Count}");
        return true;
    }
}
