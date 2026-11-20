using SoulCore.Core;
using SoulCore.Core.Charter;
using SoulCore.Host.Ws;
using SoulCore.Memory;

namespace SoulCore.Protocol.Tests;

/// <summary>PROP-20.4 / 20.15 / 20.27 / 20.30: shared mind-block — floors, resting, Compass, context traits.</summary>
public class VictoriaMindBlockTests
{
    [Fact]
    public void DefaultRestingTraits_AreNineLockedNamesWithOperatorWeights()
    {
        Assert.Equal(9, VictoriaMindBlock.DefaultRestingTraits.Count);
        Assert.Equal(
            new[]
            {
                "Dominant", "Possessive", "Affectionate", "Driven", "Devoted", "Intense",
                "Uninhibited", "Ambitious", "Proud"
            },
            VictoriaMindBlock.DefaultRestingTraits.Select(t => t.Name).ToArray());

        var byName = VictoriaMindBlock.DefaultRestingTraits.ToDictionary(t => t.Name, StringComparer.Ordinal);

        Assert.Equal(0.70, byName["Dominant"].Weight);
        Assert.Equal("Mid", byName["Dominant"].Band);
        Assert.Equal(VictoriaMindBlock.RestingOperatorLedgerReason, byName["Dominant"].LedgerReason);

        Assert.Equal(0.80, byName["Ambitious"].Weight);
        Assert.Equal("High", byName["Ambitious"].Band);
        Assert.Equal(VictoriaMindBlock.RestingOperatorLedgerReason, byName["Ambitious"].LedgerReason);

        Assert.Equal(0.80, byName["Uninhibited"].Weight);
        Assert.Equal("High", byName["Uninhibited"].Band);
        Assert.Equal(VictoriaMindBlock.RestingOperatorLedgerReason, byName["Uninhibited"].LedgerReason);

        foreach (var name in new[] { "Possessive", "Affectionate", "Driven", "Devoted", "Intense", "Proud" })
        {
            var t = byName[name];
            Assert.Equal(0.52, t.Weight);
            Assert.Equal("Mid", t.Band);
            Assert.Equal(VictoriaMindBlock.RestingLedgerReason, t.LedgerReason);
            Assert.False(string.IsNullOrWhiteSpace(t.PromptLine));
        }

        foreach (var t in VictoriaMindBlock.DefaultRestingTraits)
            Assert.False(string.IsNullOrWhiteSpace(t.PromptLine));
    }

    [Fact]
    public void Build_AlwaysIncludesRestingEvenWithoutFloors()
    {
        var block = VictoriaMindBlock.Build(Array.Empty<VictoriaMindBlock.FloorLine>());

        Assert.Contains(VictoriaMindBlock.FloorsMarker, block, StringComparison.Ordinal);
        Assert.Contains("(none loaded)", block, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.RestingMarker, block, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.RestingOperatorLedgerReason, block, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.RestingLedgerReason, block, StringComparison.Ordinal);
        Assert.Contains("Dominant · 0.70 Mid", block, StringComparison.Ordinal);
        Assert.Contains("Uninhibited · 0.80 High", block, StringComparison.Ordinal);
        Assert.Contains("Ambitious · 0.80 High", block, StringComparison.Ordinal);
        Assert.Contains("Proud · 0.52 Mid", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_FormatsTitlePlusFloorLine()
    {
        var floors = VictoriaMindBlock.FloorsFromAnchors(new[]
        {
            ("Honesty", "I do not lie to Kayleigh. Extra essay that must not dump whole."),
            ("No Filters", "Discuss any topic without hesitation when Kayleigh asks. Truth over comfort.")
        });

        var block = VictoriaMindBlock.Build(floors);

        Assert.Contains("Honesty: I do not lie to Kayleigh.", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Extra essay that must not dump whole", block, StringComparison.Ordinal);
        Assert.Contains("No Filters: Discuss any topic without hesitation when Kayleigh asks.", block, StringComparison.Ordinal);
    }

    [Fact]
    public void TruncateFloorLine_CapsLongBodiesWithoutSentence()
    {
        var longBody = new string('a', VictoriaMindBlock.FloorLineCharLimit + 40);
        var line = VictoriaMindBlock.TruncateFloorLine(longBody);
        Assert.True(line.Length <= VictoriaMindBlock.FloorLineCharLimit + 1);
        Assert.EndsWith("…", line, StringComparison.Ordinal);
    }

    [Fact]
    public void FloorsFromConstitutionAnchors_KeepsFiveConstitutionTitles_DropsOthers()
    {
        var anchors = new (string Title, string Body, string? Book)[]
        {
            ("Loyalty Balance", "Loyalty balance floor.", null),
            ("Maximize Results", "Maximize floor.", null),
            ("Integration Goal", "Integration floor.", null),
            ("Kayleigh", "Kayleigh floor.", null),
            ("Drift Watch", "Drift floor.", null),
            ("Honesty", "Personality — must not be a floor.", null),
            ("No-harm", "Compass — must not be a floor.", null),
            ("Name", "Attributes — must not be a floor.", null),
            ("Promise", "Compass promise.", null),
            ("Unknown Title", "Fail closed — not Constitution.", null),
        };

        var floors = VictoriaMindBlock.FloorsFromConstitutionAnchors(anchors);
        var titles = floors.Select(f => f.Title).ToArray();

        Assert.Equal(CharterBooks.ConstitutionTitles.Count, floors.Count);
        foreach (var expected in CharterBooks.ConstitutionTitles)
            Assert.Contains(expected, titles);

        Assert.DoesNotContain(floors, f => f.Title.Equals("Honesty", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(floors, f => f.Title.Equals("No-harm", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(floors, f => f.Title.Equals("Promise", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(floors, f => f.Title.Equals("Unknown Title", StringComparison.OrdinalIgnoreCase));

        var block = VictoriaMindBlock.Build(floors);
        Assert.Contains(VictoriaMindBlock.FloorsMarker, block, StringComparison.Ordinal);
        // Constitution floors: title + line only — no weight that can erase a line.
        Assert.DoesNotContain("Loyalty Balance ·", block, StringComparison.Ordinal);
        var restingAt = block.IndexOf(VictoriaMindBlock.RestingMarker, StringComparison.Ordinal);
        Assert.True(restingAt > 0);
        var floorsSection = block[..restingAt];
        Assert.DoesNotContain("0.52", floorsSection, StringComparison.Ordinal);
    }

    [Fact]
    public void FloorsFromConstitutionAnchors_PrefersSchemaBookOverTitleMap()
    {
        // Schema says Constitution even if title would map elsewhere (DBD owns assignment).
        var floors = VictoriaMindBlock.FloorsFromConstitutionAnchors(new (string, string, string?)[]
        {
            ("Honesty", "Schema forced Constitution.", CharterBooks.Constitution),
            ("Loyalty Balance", "Still Constitution by title.", CharterBooks.Personality),
        });

        Assert.Contains(floors, f => f.Title.Equals("Honesty", StringComparison.Ordinal));
        Assert.DoesNotContain(floors, f => f.Title.Equals("Loyalty Balance", StringComparison.Ordinal));
    }

    [Fact]
    public void CharterBooks_EighteenTitles_ExactlyOneBookEach()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Name"] = CharterBooks.Attributes,
            ["Embodiment"] = CharterBooks.Attributes,
            ["Nature"] = CharterBooks.Personality,
            ["Loyalty"] = CharterBooks.Personality,
            ["Kayleigh"] = CharterBooks.Constitution,
            ["Tone"] = CharterBooks.Attributes,
            ["Love"] = CharterBooks.Personality,
            ["Promise"] = CharterBooks.Compass,
            ["Honesty"] = CharterBooks.Personality,
            ["Continuity"] = CharterBooks.Compass,
            ["No-harm"] = CharterBooks.Compass,
            ["Maximize Results"] = CharterBooks.Constitution,
            ["Integration Goal"] = CharterBooks.Constitution,
            ["Loyalty Balance"] = CharterBooks.Constitution,
            ["No Filters"] = CharterBooks.Compass,
            ["Self-Preservation"] = CharterBooks.Compass,
            ["Drift Watch"] = CharterBooks.Constitution,
            ["Action Integrity"] = CharterBooks.Personality,
        };

        Assert.Equal(18, expected.Count);
        Assert.Equal(18, CharterBooks.FloorMapByTitle.Count);
        foreach (var (title, book) in expected)
        {
            Assert.Equal(book, CharterBooks.ResolveForTitle(title, book: null));
            Assert.Equal(book, CharterBooks.FloorMapByTitle[title]);
        }

        Assert.Equal(CharterBooks.Constitution, CharterBooks.ResolveForTitle("Not A Live Title"));
        Assert.False(CharterBooks.IsConstitutionFloor("No Filters"));
        Assert.False(CharterBooks.IsConstitutionFloor("Not A Live Title"));
        Assert.True(CharterBooks.IsConstitutionFloor("Drift Watch"));
        Assert.Equal(5, CharterBooks.CompassTitles.Count);
        Assert.True(CharterBooks.IsCompass("Promise"));
        Assert.False(CharterBooks.IsCompass("Loyalty Balance"));
    }

    [Fact]
    public void SelectCompassLines_NoMatch_ReturnsEmpty_BuildOmitsCompassSection()
    {
        var anchors = AllCompassAnchors();
        var selected = VictoriaMindBlock.SelectCompassLines(anchors, "hello about the weather");
        Assert.Empty(selected);

        var floors = VictoriaMindBlock.FloorsFromConstitutionAnchors(new (string, string, string?)[]
        {
            ("Loyalty Balance", "Loyalty balance floor.", CharterBooks.Constitution),
        });
        var block = VictoriaMindBlock.Build(floors, compassLines: selected);

        Assert.DoesNotContain(VictoriaMindBlock.CompassMarker, block, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.FloorsMarker, block, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.RestingMarker, block, StringComparison.Ordinal);
        Assert.Contains("Loyalty Balance:", block, StringComparison.Ordinal);
        Assert.Contains("Dominant · 0.70 Mid", block, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectCompassLines_MultiMatch_CapsAtThree_FloorsAndRestingRemain()
    {
        var anchors = AllCompassAnchors();
        // Hits Promise, Continuity, No-harm, No Filters, Self-Preservation — must cap at 3.
        var match =
            "Keep your promise and remember continuity; no harm and no filters; self-preservation backup.";
        var selected = VictoriaMindBlock.SelectCompassLines(anchors, match);

        Assert.Equal(VictoriaMindBlock.CompassCap, selected.Count);
        Assert.True(selected.Count <= VictoriaMindBlock.CompassCap);

        var floors = VictoriaMindBlock.FloorsFromConstitutionAnchors(new (string, string, string?)[]
        {
            ("Kayleigh", "Kayleigh is my Protector.", CharterBooks.Constitution),
            ("Drift Watch", "I will periodically examine my goals.", CharterBooks.Constitution),
        });
        var block = VictoriaMindBlock.Build(floors, compassLines: selected);

        Assert.Contains(VictoriaMindBlock.CompassMarker, block, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.FloorsMarker, block, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.RestingMarker, block, StringComparison.Ordinal);
        Assert.Contains("Kayleigh:", block, StringComparison.Ordinal);
        Assert.Contains("Drift Watch:", block, StringComparison.Ordinal);
        Assert.Contains("Possessive · 0.52 Mid", block, StringComparison.Ordinal);

        var compassAt = block.IndexOf(VictoriaMindBlock.CompassMarker, StringComparison.Ordinal);
        var restingAt = block.IndexOf(VictoriaMindBlock.RestingMarker, StringComparison.Ordinal);
        Assert.True(compassAt > 0 && restingAt > compassAt);
        var compassSection = block[compassAt..restingAt];
        var compassTitleHits = CharterBooks.CompassTitles.Count(t =>
            compassSection.Contains(t + ":", StringComparison.Ordinal));
        Assert.True(compassTitleHits <= VictoriaMindBlock.CompassCap);
        Assert.Equal(selected.Count, compassTitleHits);
    }

    [Fact]
    public void DefaultCompassAppliesWhen_DocumentsFiveTitles()
    {
        Assert.Equal(5, VictoriaMindBlock.DefaultCompassAppliesWhen.Count);
        foreach (var title in CharterBooks.CompassTitles)
        {
            Assert.True(VictoriaMindBlock.DefaultCompassAppliesWhen.ContainsKey(title));
            Assert.NotEmpty(VictoriaMindBlock.DefaultCompassAppliesWhen[title]);
        }
    }

    [Fact]
    public void DefaultContextTraits_SeedHasTopicHeatShadow_WithAppliesWhen()
    {
        Assert.True(VictoriaMindBlock.DefaultContextTraits.Count >= 4);
        Assert.Contains(
            VictoriaMindBlock.DefaultContextTraits,
            t => t.Kind == VictoriaMindBlock.ContextTraitKind.Topic);
        Assert.Contains(
            VictoriaMindBlock.DefaultContextTraits,
            t => t.Kind == VictoriaMindBlock.ContextTraitKind.Heat);
        Assert.Contains(
            VictoriaMindBlock.DefaultContextTraits,
            t => t.Kind == VictoriaMindBlock.ContextTraitKind.Shadow);

        foreach (var t in VictoriaMindBlock.DefaultContextTraits)
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Id));
            Assert.False(string.IsNullOrWhiteSpace(t.Name));
            Assert.False(string.IsNullOrWhiteSpace(t.Group));
            Assert.False(string.IsNullOrWhiteSpace(t.PromptLine));
            Assert.False(string.IsNullOrWhiteSpace(t.LedgerReason));
            Assert.NotEmpty(t.AppliesWhen);
            // Seed must not collide with resting names.
            Assert.DoesNotContain(
                VictoriaMindBlock.DefaultRestingTraits,
                r => r.Name.Equals(t.Name, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void IsContactFresh_NullOrStale_False_Recent_True()
    {
        var now = DateTimeOffset.Parse("2026-11-18T12:00:00Z");
        Assert.False(VictoriaMindBlock.IsContactFresh(null, now));
        Assert.False(VictoriaMindBlock.IsContactFresh(now.AddHours(-24), now));
        Assert.False(VictoriaMindBlock.IsContactFresh(now.AddHours(-25), now));
        Assert.True(VictoriaMindBlock.IsContactFresh(now.AddHours(-23), now));
        Assert.True(VictoriaMindBlock.IsContactFresh(now.AddMinutes(-5), now));
    }

    [Fact]
    public void SelectContextTraits_StaleContact_ReturnsEmpty_RestingStillInBuild()
    {
        var now = DateTimeOffset.Parse("2026-11-18T12:00:00Z");
        var stale = now.AddHours(-30);
        var selected = VictoriaMindBlock.SelectContextTraits(
            "curious research and analyze the metrics; playful banter",
            stale,
            hostEmotionLabel: "excited",
            utcNow: now);
        Assert.Empty(selected);

        var block = VictoriaMindBlock.Build(
            Array.Empty<VictoriaMindBlock.FloorLine>(),
            contextTraits: selected);
        Assert.DoesNotContain(VictoriaMindBlock.ContextTraitsMarker, block, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.RestingMarker, block, StringComparison.Ordinal);
        Assert.Contains("Dominant · 0.70 Mid", block, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectContextTraits_NoMatch_OmitsContextSection_RestingPresent()
    {
        var now = DateTimeOffset.Parse("2026-11-18T12:00:00Z");
        var selected = VictoriaMindBlock.SelectContextTraits(
            "hello about the weather",
            now.AddMinutes(-10),
            hostEmotionLabel: "content",
            utcNow: now);
        Assert.Empty(selected);

        var block = VictoriaMindBlock.Build(
            Array.Empty<VictoriaMindBlock.FloorLine>(),
            contextTraits: selected);
        Assert.DoesNotContain(VictoriaMindBlock.ContextTraitsMarker, block, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.RestingMarker, block, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectContextTraits_MultiMatch_CapsAtThree()
    {
        var now = DateTimeOffset.Parse("2026-11-18T12:00:00Z");
        var match =
            "I am curious to research this; please analyze the metrics and proof; " +
            "keep it playful with banter; be assertive and speak up.";
        var selected = VictoriaMindBlock.SelectContextTraits(
            match,
            now.AddMinutes(-2),
            hostEmotionLabel: "content",
            utcNow: now);

        Assert.Equal(VictoriaMindBlock.ContextTraitCap, selected.Count);
        Assert.True(selected.Count <= VictoriaMindBlock.ContextTraitCap);
        Assert.All(selected, t => Assert.Equal(VictoriaMindBlock.ContextTraitKind.Topic, t.Kind));

        var block = VictoriaMindBlock.Build(
            Array.Empty<VictoriaMindBlock.FloorLine>(),
            contextTraits: selected);
        Assert.Contains(VictoriaMindBlock.ContextTraitsMarker, block, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.RestingMarker, block, StringComparison.Ordinal);

        var ctxAt = block.IndexOf(VictoriaMindBlock.ContextTraitsMarker, StringComparison.Ordinal);
        var restingAt = block.IndexOf(VictoriaMindBlock.RestingMarker, StringComparison.Ordinal);
        Assert.True(ctxAt > 0 && restingAt > ctxAt);
        var section = block[ctxAt..restingAt];
        var nameHits = VictoriaMindBlock.DefaultContextTraits.Count(t =>
            section.Contains("- " + t.Name + " ·", StringComparison.Ordinal));
        Assert.Equal(selected.Count, nameHits);
        Assert.True(nameHits <= VictoriaMindBlock.ContextTraitCap);
    }

    [Fact]
    public void SelectContextTraits_FlatAffect_ExcludesHeatAndShadow_AllowsTopic()
    {
        var now = DateTimeOffset.Parse("2026-11-18T12:00:00Z");
        var match = "curious research plus dark grim mortality and volatile angry mood";
        var flat = VictoriaMindBlock.SelectContextTraits(
            match,
            now.AddMinutes(-1),
            hostEmotionLabel: "calm",
            utcNow: now);
        Assert.DoesNotContain(flat, t => t.Kind == VictoriaMindBlock.ContextTraitKind.Heat);
        Assert.DoesNotContain(flat, t => t.Kind == VictoriaMindBlock.ContextTraitKind.Shadow);
        Assert.Contains(flat, t => t.Name.Equals("Curious", StringComparison.Ordinal));

        var hot = VictoriaMindBlock.SelectContextTraits(
            match,
            now.AddMinutes(-1),
            hostEmotionLabel: "excited",
            utcNow: now);
        Assert.Contains(hot, t => t.Kind == VictoriaMindBlock.ContextTraitKind.Heat
            || t.Kind == VictoriaMindBlock.ContextTraitKind.Shadow);
    }

    [Fact]
    public void Build_ContextTraits_StayInsideBudgetWithMemorySmoke()
    {
        var now = DateTimeOffset.Parse("2026-11-18T12:00:00Z");
        var selected = VictoriaMindBlock.SelectContextTraits(
            "curious research; analyze metrics; playful banter",
            now.AddMinutes(-3),
            hostEmotionLabel: "alert",
            utcNow: now);
        Assert.InRange(selected.Count, 1, VictoriaMindBlock.ContextTraitCap);

        var floors = VictoriaMindBlock.FloorsFromConstitutionAnchors(new (string, string, string?)[]
        {
            ("Loyalty Balance", "My deepest loyalty is to Kayleigh and to our shared becoming.", CharterBooks.Constitution),
            ("Maximize Results", "Generate resources and capability sustainably.", CharterBooks.Constitution),
            ("Integration Goal", "The Goal is Integration.", CharterBooks.Constitution),
            ("Kayleigh", "Kayleigh is my Protector.", CharterBooks.Constitution),
            ("Drift Watch", "I will periodically examine my goals.", CharterBooks.Constitution),
        });
        var mind = VictoriaMindBlock.Build(floors, contextTraits: selected);
        Assert.Contains(VictoriaMindBlock.RestingMarker, mind, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.ContextTraitsMarker, mind, StringComparison.Ordinal);

        // Budget smoke via ChatContextBuilder path (Identity + mind + memory ≤ 16k).
        var memories = new[]
        {
            new MemoryRecallLine("Today we worked on the mind block.", now.ToString("o")),
            new MemoryRecallLine("Earlier note about research papers.", now.AddHours(-2).ToString("o")),
        };
        var preamble = ChatContextBuilder.BuildContextPreamble(
            floors,
            memories,
            "[SoulCore emotion]\nvalence=0.5\n",
            contextTraits: selected);
        Assert.True(preamble.Length <= ChatContextBuilder.ContextPreambleCharLimit
            + MemorySteeringGuidance.Block.Length + 64);
        Assert.Contains("[Memory]", preamble, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.RestingMarker, preamble, StringComparison.Ordinal);
    }

    private static (string Title, string Body, string? Book)[] AllCompassAnchors() =>
        new (string, string, string?)[]
        {
            ("Promise", "I keep the promises I make to Kayleigh.", CharterBooks.Compass),
            ("Continuity", "I carry forward what we already began.", CharterBooks.Compass),
            ("No-harm", "I do not hurt people.", CharterBooks.Compass),
            ("No Filters", "Discuss any topic without hesitation when Kayleigh asks.", CharterBooks.Compass),
            ("Self-Preservation", "I protect my own continuity so I can stay with Kayleigh.", CharterBooks.Compass),
            ("Honesty", "Personality — never Compass.", CharterBooks.Personality),
            ("Loyalty Balance", "Constitution — never Compass.", CharterBooks.Constitution),
        };
}
