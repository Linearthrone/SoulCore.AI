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
