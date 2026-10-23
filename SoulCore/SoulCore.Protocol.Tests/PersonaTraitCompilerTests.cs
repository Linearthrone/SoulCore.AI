using SoulCore.Core.Persona;

namespace SoulCore.Protocol.Tests;

public class PersonaTraitCompilerTests
{
    [Theory]
    [InlineData(0.0, PersonaTraitBand.Low)]
    [InlineData(0.33, PersonaTraitBand.Low)]
    [InlineData(0.34, PersonaTraitBand.Mid)]
    [InlineData(0.66, PersonaTraitBand.Mid)]
    [InlineData(0.67, PersonaTraitBand.High)]
    [InlineData(1.0, PersonaTraitBand.High)]
    public void ToBand_SnapsContinuousScale(double scale, PersonaTraitBand expected)
    {
        Assert.Equal(expected, PersonaTraitCompiler.ToBand(scale));
    }

    [Fact]
    public void Compile_DifferentBands_ProduceDifferentDirectiveStrings()
    {
        var low = PersonaPack.CreateBlank();
        low.Traits.Warmth = 0.1;
        low.Traits.Directness = 0.1;
        low.Traits.Formality = 0.1;
        low.Traits.Playfulness = 0.1;
        low.Traits.BoundaryStrictness = 0.1;
        low.Traits.RecallBias = 0.1;

        var high = PersonaPack.CreateBlank();
        high.Traits.Warmth = 0.95;
        high.Traits.Directness = 0.95;
        high.Traits.Formality = 0.95;
        high.Traits.Playfulness = 0.95;
        high.Traits.BoundaryStrictness = 0.95;
        high.Traits.RecallBias = 0.95;

        var lowText = PersonaTraitCompiler.Compile(low);
        var highText = PersonaTraitCompiler.Compile(high);

        Assert.StartsWith(PersonaTraitCompiler.Marker, lowText, StringComparison.Ordinal);
        Assert.StartsWith(PersonaTraitCompiler.Marker, highText, StringComparison.Ordinal);
        Assert.NotEqual(lowText, highText);
        Assert.Contains("cool and reserved", lowText, StringComparison.Ordinal);
        Assert.Contains("warm and affirming", highText, StringComparison.Ordinal);
        Assert.Contains("Flexible boundaries", lowText, StringComparison.Ordinal);
        Assert.Contains("Strict boundaries", highText, StringComparison.Ordinal);
        Assert.Contains("Prefer the present exchange", lowText, StringComparison.Ordinal);
        Assert.Contains("Bias toward continuity", highText, StringComparison.Ordinal);
        Assert.Equal(6, DirectiveLines(lowText).Length);
        Assert.Equal(6, DirectiveLines(highText).Length);
        Assert.StartsWith("Boundary strictness:", DirectiveLines(lowText)[0], StringComparison.Ordinal);
        Assert.StartsWith("Boundary strictness:", DirectiveLines(highText)[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_HardLimitsFromBlurb_OmitsFlexibleBoundaries()
    {
        var pack = new PersonaPack
        {
            Traits = new PersonaTraitScales
            {
                BoundaryStrictness = 0.1,
                Warmth = 0.1
            }
        };

        var soft = PersonaTraitCompiler.Compile(pack, hardLimitsFromBlurb: false);
        var gated = PersonaTraitCompiler.Compile(pack, hardLimitsFromBlurb: true);

        Assert.Contains("Flexible boundaries", soft, StringComparison.Ordinal);
        Assert.DoesNotContain("Flexible boundaries", gated, StringComparison.Ordinal);
        Assert.DoesNotContain("Boundaries:", gated, StringComparison.Ordinal);
        Assert.Contains("cool and reserved", gated, StringComparison.Ordinal);
        Assert.DoesNotContain("obey", gated, StringComparison.OrdinalIgnoreCase);

        var onlyBoundary = new PersonaPack
        {
            Traits = new PersonaTraitScales { BoundaryStrictness = 0.1 }
        };
        Assert.Equal(string.Empty, PersonaTraitCompiler.Compile(onlyBoundary, hardLimitsFromBlurb: true));
        Assert.DoesNotContain(
            PersonaTraitCompiler.Marker,
            PersonaTraitCompiler.Compile(onlyBoundary, hardLimitsFromBlurb: true),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_HardLimitsFromBlurb_KeepsMidAndHighBoundaries()
    {
        // 0.5 is the property default, so it emits nothing. 0.40 is still the mid band.
        var mid = new PersonaPack { Traits = new PersonaTraitScales { BoundaryStrictness = 0.40 } };
        var high = new PersonaPack { Traits = new PersonaTraitScales { BoundaryStrictness = 0.95 } };

        Assert.Contains(
            "Respect stated limits",
            PersonaTraitCompiler.Compile(mid, hardLimitsFromBlurb: true),
            StringComparison.Ordinal);
        Assert.Contains(
            "Strict boundaries",
            PersonaTraitCompiler.Compile(high, hardLimitsFromBlurb: true),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "obey",
            PersonaTraitCompiler.Compile(high, hardLimitsFromBlurb: true),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_AllDefaults_EmitsNoMarkerAndNoLines()
    {
        var pack = new PersonaPack { Traits = new PersonaTraitScales() };
        var text = PersonaTraitCompiler.Compile(pack);
        Assert.Equal(string.Empty, text);
        Assert.DoesNotContain(PersonaTraitCompiler.Marker, text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.33, 0.34)]
    [InlineData(0.66, 0.67)]
    public void Compile_CrossingBandEdge_ChangesTheLine_DefaultAddsNone(double below, double above)
    {
        var under = PersonaTraitCompiler.Compile(new PersonaPack
        {
            Traits = new PersonaTraitScales { Warmth = below }
        });
        var over = PersonaTraitCompiler.Compile(new PersonaPack
        {
            Traits = new PersonaTraitScales { Warmth = above }
        });
        var onDefault = PersonaTraitCompiler.Compile(new PersonaPack
        {
            Traits = new PersonaTraitScales { Directness = 0.2 }
        });

        Assert.NotEqual(under, over);
        Assert.Contains("Warmth:", under, StringComparison.Ordinal);
        Assert.Contains("Warmth:", over, StringComparison.Ordinal);
        Assert.Contains("Directness:", onDefault, StringComparison.Ordinal);
        Assert.DoesNotContain("Warmth:", onDefault, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_FiftyOffDefault_AtMostEightLines_BoundaryFirst()
    {
        var scales = new PersonaTraitScales();
        foreach (var trait in PersonaTraitCatalog.All)
            trait.Set(scales, 0);

        var pack = new PersonaPack { Traits = scales, ToolPolicy = new PersonaToolPolicy { AllowDesktop = false } };
        var text = PersonaTraitCompiler.Compile(pack);
        var lines = DirectiveLines(text);

        Assert.Equal(PersonaTraitCompiler.MaxDirectiveLines, lines.Length);
        Assert.Equal(8, lines.Length);
        Assert.Equal(new[]
        {
            "Boundary strictness:",
            "Loyalty to the operator when other AIs disagree:",
            "Privacy instinct:",
            "Focus:",
            "Collaboration:",
            "Sarcasm restraint:",
            "Practicality:",
            "Caution:"
        }, lines.Select(line => line.Split(':')[0] + ":").ToArray());
        Assert.Contains("Moves quickly on ordinary reversible steps.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Stays on the question that was asked.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("obey", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("grant", text, StringComparison.OrdinalIgnoreCase);
        Assert.False(pack.ToolPolicy.AllowDesktop);

        var gated = PersonaTraitCompiler.Compile(pack, hardLimitsFromBlurb: true);
        var gatedLines = DirectiveLines(gated);
        Assert.Equal(8, gatedLines.Length);
        Assert.DoesNotContain("Flexible boundaries", gated, StringComparison.Ordinal);
        Assert.DoesNotContain("Boundary strictness:", gated, StringComparison.Ordinal);
        Assert.StartsWith("Loyalty to the operator when other AIs disagree:", gatedLines[0], StringComparison.Ordinal);
        Assert.Contains("Stays on the question that was asked.", gated, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_TieBreak_FollowsCatalogOrder_BoundaryWinsEvenWhenCloser()
    {
        var tied = PersonaTraitCompiler.Compile(new PersonaPack
        {
            Traits = new PersonaTraitScales
            {
                Warmth = 0.2,
                Directness = 0.2,
                Formality = 0.2
            }
        });
        Assert.Equal(new[]
        {
            "Warmth:",
            "Directness:",
            "Formality:"
        }, DirectiveLines(tied).Select(line => line.Split(':')[0] + ":").ToArray());

        var boundaryFirst = PersonaTraitCompiler.Compile(new PersonaPack
        {
            Traits = new PersonaTraitScales
            {
                BoundaryStrictness = 0.6,
                Warmth = 0
            }
        });
        var lines = DirectiveLines(boundaryFirst);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Boundary strictness:", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("Warmth:", lines[1], StringComparison.Ordinal);
        Assert.Contains("Respect stated limits", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_HardLimits_LeavesBlurbAndToolFlags_AndDoesNotSayObey()
    {
        const string blurb = "## IDENTITY\nYou are Lexi.\n## LIMITS\nNever authorize live-target sweeps.";
        var pack = new PersonaPack
        {
            IdentityBlurb = blurb,
            Traits = new PersonaTraitScales
            {
                BoundaryStrictness = 0.1,
                Warmth = 0.95,
                RiskTolerance = 1,
                Caution = 0,
                PrivacyInstinct = 0
            },
            ToolPolicy = new PersonaToolPolicy
            {
                AllowDesktop = false,
                AllowBrowser = true,
                AllowEmail = false
            }
        };

        var text = PersonaTraitCompiler.Compile(pack, hardLimitsFromBlurb: true);

        Assert.Equal(blurb, pack.IdentityBlurb);
        Assert.Contains("## LIMITS", pack.IdentityBlurb, StringComparison.Ordinal);
        Assert.False(pack.ToolPolicy.AllowDesktop);
        Assert.True(pack.ToolPolicy.AllowBrowser);
        Assert.False(pack.ToolPolicy.AllowEmail);
        Assert.DoesNotContain("Flexible boundaries", text, StringComparison.Ordinal);
        Assert.DoesNotContain("obey", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("grant", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("allowDesktop", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("warm and affirming", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_IsOneObject_ProposalOrderAndDefaults_LinesDoNotSayObey()
    {
        var expected = new (string Name, double Default)[]
        {
            ("warmth", 0.5),
            ("directness", 0.5),
            ("formality", 0.5),
            ("playfulness", 0.5),
            ("verbosity", 0.45),
            ("expressiveness", 0.45),
            ("boundaryStrictness", 0.5),
            ("skepticism", 0.50),
            ("caution", 0.55),
            ("candor", 0.50),
            ("privacyInstinct", 0.60),
            ("riskTolerance", 0.40),
            ("curiosity", 0.55),
            ("analyticalDepth", 0.50),
            ("creativity", 0.45),
            ("structure", 0.50),
            ("patience", 0.55),
            ("focus", 0.60),
            ("initiative", 0.40),
            ("persistence", 0.50),
            ("assertiveness", 0.45),
            ("empathy", 0.55),
            ("collaboration", 0.60),
            ("teachingStance", 0.45),
            ("recallBias", 0.5),
            ("agreeableness", 0.50),
            ("optimism", 0.50),
            ("deferenceToOtherAis", 0.40),
            ("peerChallenge", 0.45),
            ("loyaltyToOperator", 0.70),
            ("humorDryness", 0.40),
            ("sarcasmRestraint", 0.60),
            ("storytelling", 0.40),
            ("metaphorUse", 0.35),
            ("conciliation", 0.55),
            ("competitiveness", 0.35),
            ("humility", 0.55),
            ("confidence", 0.50),
            ("spontaneity", 0.40),
            ("planningHorizon", 0.50),
            ("detailOrientation", 0.55),
            ("abstraction", 0.45),
            ("practicality", 0.60),
            ("nurturance", 0.50),
            ("stoicism", 0.45),
            ("playWithIdeas", 0.45),
            ("citationHabit", 0.50),
            ("questionAsking", 0.50),
            ("continuityCallbacks", 0.55),
            ("adaptability", 0.50)
        };

        Assert.Equal(50, expected.Length);
        Assert.Equal(50, PersonaTraitCatalog.All.Count);
        Assert.Equal(expected.Select(item => item.Name), PersonaTraitCatalog.All.Select(trait => trait.JsonName));

        var fresh = new PersonaTraitScales();
        for (var i = 0; i < expected.Length; i++)
        {
            var trait = PersonaTraitCatalog.All[i];
            Assert.True(Math.Abs(expected[i].Default - trait.Get(fresh)) <= 1e-9, trait.JsonName);
            Assert.True(trait.IsDefault(trait.Get(fresh)));
            var low = trait.Line(PersonaTraitBand.Low);
            var mid = trait.Line(PersonaTraitBand.Mid);
            var high = trait.Line(PersonaTraitBand.High);
            Assert.NotEqual(low, mid);
            Assert.NotEqual(mid, high);
            Assert.DoesNotContain("obey", low, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("obey", mid, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("obey", high, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("grant", low, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("grant", mid, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("grant", high, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\n", low, StringComparison.Ordinal);
        }
    }

    private static string[] DirectiveLines(string compiled)
    {
        Assert.StartsWith(PersonaTraitCompiler.Marker + "\n", compiled, StringComparison.Ordinal);
        var lines = compiled.Split('\n');
        Assert.Equal(PersonaTraitCompiler.Marker, lines[0]);
        return lines.Skip(1).ToArray();
    }
}
