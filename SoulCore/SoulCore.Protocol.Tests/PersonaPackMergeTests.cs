using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SoulCore.Core.Persona;
using SoulCore.Host.Persona;

namespace SoulCore.Protocol.Tests;

/// <summary>PROP-15.19 — sparse PUT must merge, not wipe pack fields.</summary>
public class PersonaPackMergeTests
{
    [Fact]
    public void SparseInferenceModel_PreservesIdentityBlurbTraitsAndTitles()
    {
        var existing = BuildLexiLikePack();
        var patch = Parse("""{ "inferenceModel": "mother:latest" }""");

        Assert.True(PersonaPackMerge.TryApplySparsePatch(existing, patch, out var merged, out var error), error);
        Assert.Null(error);

        Assert.Equal("lexi", merged.PersonaId);
        Assert.Equal("Alexia", merged.DisplayName);
        Assert.Equal("Kayleigh", merged.HumanAddress);
        Assert.Equal("lexi", merged.ContactId);
        Assert.Equal("## LIMITS\nNo real-world harm.", merged.IdentityBlurb);
        Assert.Equal("lexi-sandbox", merged.VmWindowTitle);
        Assert.Equal("profiles/lexi", merged.PlaywrightProfileDir);
        Assert.Equal("mother:latest", merged.InferenceModel);
        Assert.Equal(0.8, merged.Traits.Warmth);
        Assert.Equal(0.23, merged.Traits.BoundaryStrictness);
        Assert.Equal(0.7, merged.Traits.RecallBias);
        Assert.True(merged.ToolPolicy.AllowDesktop);
        Assert.False(merged.ToolPolicy.AllowEmail);
    }

    [Fact]
    public void SparseTraitsWarmth_PreservesOtherTraitScales()
    {
        var existing = BuildLexiLikePack();
        var patch = Parse("""{ "traits": { "warmth": 0.95 } }""");

        Assert.True(PersonaPackMerge.TryApplySparsePatch(existing, patch, out var merged, out var error), error);

        Assert.Equal(0.95, merged.Traits.Warmth);
        Assert.Equal(0.55, merged.Traits.Directness);
        Assert.Equal(0.23, merged.Traits.BoundaryStrictness);
        Assert.Equal("## LIMITS\nNo real-world harm.", merged.IdentityBlurb);
        Assert.Equal("gemma4:latest", merged.InferenceModel);
    }

    [Fact]
    public void EmptyObject_Rejected()
    {
        var existing = BuildLexiLikePack();
        var patch = Parse("{}");

        Assert.False(PersonaPackMerge.TryApplySparsePatch(existing, patch, out _, out var error));
        Assert.Contains("Empty body", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PersonaIdOnly_RejectedAsEmpty()
    {
        var existing = BuildLexiLikePack();
        var patch = Parse("""{ "personaId": "lexi" }""");

        Assert.False(PersonaPackMerge.TryApplySparsePatch(existing, patch, out _, out var error));
        Assert.Contains("Empty body", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MismatchedPersonaId_Rejected()
    {
        var existing = BuildLexiLikePack();
        var patch = Parse("""{ "personaId": "victoria", "inferenceModel": "x" }""");

        Assert.False(PersonaPackMerge.TryApplySparsePatch(existing, patch, out _, out var error));
        Assert.Contains("must match path", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExplicitNullInferenceModel_ClearsModel_KeepsBlurb()
    {
        var existing = BuildLexiLikePack();
        var patch = Parse("""{ "inferenceModel": null }""");

        Assert.True(PersonaPackMerge.TryApplySparsePatch(existing, patch, out var merged, out var error), error);
        Assert.Null(merged.InferenceModel);
        Assert.Equal("## LIMITS\nNo real-world harm.", merged.IdentityBlurb);
    }

    [Fact]
    public async Task StoreRoundTrip_PartialPutSimulation_DoesNotWipePack()
    {
        var root = Path.Combine(Path.GetTempPath(), "sc-persona-merge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new PersonaPackStore(root, NullLogger<PersonaPackStore>.Instance);
            var seed = BuildLexiLikePack();
            await store.UpsertAsync(seed);

            var existing = await store.GetAsync("lexi");
            Assert.NotNull(existing);

            var patch = Parse("""{ "inferenceModel": "mother:latest" }""");
            Assert.True(PersonaPackMerge.TryApplySparsePatch(existing!, patch, out var merged, out var error), error);

            var saved = await store.UpsertAsync(merged);
            var reloaded = await store.GetAsync("lexi");
            Assert.NotNull(reloaded);

            Assert.Equal("mother:latest", reloaded!.InferenceModel);
            Assert.Equal("Alexia", reloaded.DisplayName);
            Assert.Equal("## LIMITS\nNo real-world harm.", reloaded.IdentityBlurb);
            Assert.Equal(0.23, reloaded.Traits.BoundaryStrictness);
            Assert.Equal("lexi-sandbox", reloaded.VmWindowTitle);
            Assert.Equal(saved.InferenceModel, reloaded.InferenceModel);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task OldPackJson_MissingNewKeys_KeepsStoredSix_AndNewTraitDefaults()
    {
        var root = Path.Combine(Path.GetTempPath(), "sc-persona-old-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "legacy-pack"));
        var path = Path.Combine(root, "legacy-pack", "pack.json");
        await File.WriteAllTextAsync(path, """
            {
              "personaId": "legacy-pack",
              "displayName": "Legacy",
              "humanAddress": "Kayleigh",
              "contactId": "legacy-pack",
              "identityBlurb": "## LIMITS\nKeep this.",
              "toolPolicy": { "allowDesktop": false, "allowBrowser": true, "allowEmail": false },
              "traits": {
                "warmth": 0.81,
                "directness": 0.22,
                "formality": 0.91,
                "playfulness": 0.11,
                "boundaryStrictness": 0.31,
                "recallBias": 0.72
              }
            }
            """);
        try
        {
            var store = new PersonaPackStore(root, NullLogger<PersonaPackStore>.Instance);
            var loaded = await store.GetAsync("legacy-pack");
            Assert.NotNull(loaded);
            Assert.Equal(string.Empty, loaded!.PhysicalDescription);
            Assert.Equal("## LIMITS\nKeep this.", loaded.IdentityBlurb);
            Assert.False(loaded.ToolPolicy.AllowDesktop);
            Assert.True(loaded.ToolPolicy.AllowBrowser);
            Assert.False(loaded.ToolPolicy.AllowEmail);

            var stored = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["warmth"] = 0.81,
                ["directness"] = 0.22,
                ["formality"] = 0.91,
                ["playfulness"] = 0.11,
                ["boundaryStrictness"] = 0.31,
                ["recallBias"] = 0.72
            };
            foreach (var trait in PersonaTraitCatalog.All)
            {
                var actual = trait.Get(loaded.Traits);
                if (stored.TryGetValue(trait.JsonName, out var expected))
                    Assert.True(Math.Abs(expected - actual) <= 1e-9, trait.JsonName);
                else
                    Assert.True(trait.IsDefault(actual), trait.JsonName);
            }
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void SparseNewTrait_PreservesOriginalSix_AndToolFlags()
    {
        var existing = BuildLexiLikePack();
        var patch = Parse("""{ "traits": { "warmth": 0.1, "verbosity": 0.9 } }""");

        Assert.True(PersonaPackMerge.TryApplySparsePatch(existing, patch, out var merged, out var error), error);
        Assert.Equal(0.1, merged.Traits.Warmth);
        Assert.Equal(0.9, merged.Traits.Verbosity);
        Assert.Equal(0.55, merged.Traits.Directness);
        Assert.Equal(0.23, merged.Traits.BoundaryStrictness);
        Assert.Equal(0.7, merged.Traits.RecallBias);
        Assert.True(merged.Traits.Caution > 0);
        Assert.True(PersonaTraitCatalog.All.First(t => t.JsonName == "caution").IsDefault(merged.Traits.Caution));
        Assert.Equal("## LIMITS\nNo real-world harm.", merged.IdentityBlurb);
        Assert.True(merged.ToolPolicy.AllowDesktop);
        Assert.False(merged.ToolPolicy.AllowEmail);
        Assert.True(merged.ToolPolicy.AllowBrowser);
    }

    [Fact]
    public void SparsePhysicalDescription_PreservesBlurbAndTraits()
    {
        var existing = BuildLexiLikePack();
        var patch = Parse("""{ "physicalDescription": "silver hair" }""");

        Assert.True(PersonaPackMerge.TryApplySparsePatch(existing, patch, out var merged, out var error), error);
        Assert.Equal("silver hair", merged.PhysicalDescription);
        Assert.Equal("## LIMITS\nNo real-world harm.", merged.IdentityBlurb);
        Assert.Equal(0.8, merged.Traits.Warmth);
        Assert.False(merged.ToolPolicy.AllowEmail);
    }

    [Fact]
    public async Task PhysicalDescription_Over1000_Rejected_Trimmed1000_Saves()
    {
        var root = Path.Combine(Path.GetTempPath(), "sc-persona-desc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new PersonaPackStore(root, NullLogger<PersonaPackStore>.Instance);
            var pack = BuildLexiLikePack();
            pack.PhysicalDescription = "short";
            await store.UpsertAsync(pack);
            var path = Path.Combine(root, "lexi", "pack.json");
            var before = await File.ReadAllTextAsync(path);

            pack.PhysicalDescription = "  " + new string('c', 1001) + "  ";
            var rejected = await Assert.ThrowsAsync<ArgumentException>(() => store.UpsertAsync(pack));
            Assert.Contains("1000", rejected.Message, StringComparison.Ordinal);
            Assert.Equal(before, await File.ReadAllTextAsync(path));

            var core = new string('b', 1000);
            pack.PhysicalDescription = "  " + core + "\n";
            var saved = await store.UpsertAsync(pack);
            Assert.Equal(core, saved.PhysicalDescription);
            var reloaded = await store.GetAsync("lexi");
            Assert.Equal(core, reloaded!.PhysicalDescription);
            Assert.Equal("## LIMITS\nNo real-world harm.", reloaded.IdentityBlurb);
            Assert.False(reloaded.ToolPolicy.AllowEmail);
            Assert.Equal(0.23, reloaded.Traits.BoundaryStrictness);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* best-effort */ }
        }
    }

    private static PersonaPack BuildLexiLikePack() => new()
    {
        PersonaId = "lexi",
        DisplayName = "Alexia",
        HumanAddress = "Kayleigh",
        ContactId = "lexi",
        IdentityBlurb = "## LIMITS\nNo real-world harm.",
        VmWindowTitle = "lexi-sandbox",
        PlaywrightProfileDir = "profiles/lexi",
        InferenceModel = "gemma4:latest",
        ToolPolicy = new PersonaToolPolicy
        {
            AllowDesktop = true,
            AllowBrowser = true,
            AllowEmail = false
        },
        Traits = new PersonaTraitScales
        {
            Warmth = 0.8,
            Directness = 0.55,
            Formality = 0.35,
            Playfulness = 0.55,
            BoundaryStrictness = 0.23,
            RecallBias = 0.7
        },
        UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
