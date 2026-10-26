using SoulCore.Core;

namespace SoulCore.Protocol.Tests;

public class MemorySteeringGuidanceTests
{
    [Fact]
    public void AppendToPreamble_IsIdempotent()
    {
        var once = MemorySteeringGuidance.AppendToPreamble("hello");
        Assert.Contains(MemorySteeringGuidance.Marker, once, StringComparison.Ordinal);
        Assert.Contains("not flavor", once, StringComparison.Ordinal);
        Assert.Contains("CORRECT yourself", once, StringComparison.Ordinal);
        Assert.Equal(once, MemorySteeringGuidance.AppendToPreamble(once));
    }

    [Fact]
    public void MatchProcedures_RanksMaestroSopForClassAsk()
    {
        var sops = new (long, string, string, string)[]
        {
            (1, "Make tea", "when thirsty", "boil water"),
            (2, "Maestro class login", "when getting ready for class",
                "1) open Firefox 2) go to Maestro 3) sign in 4) open today's lesson"),
            (3, "Email triage", "morning mail", "email_inbox"),
        };

        var matches = MemorySteeringGuidance.MatchProcedures(
            "log into Maestro and get ready for class",
            new[] { "Yesterday I opened Firefox, signed into Maestro, then opened the lesson." },
            sops);

        Assert.NotEmpty(matches);
        Assert.Equal(2, matches[0].Id);
        Assert.Contains("Maestro", matches[0].Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Firefox", matches[0].StepsPreview, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildKnownProceduresBlock_InstructsSopGet()
    {
        var block = MemorySteeringGuidance.BuildKnownProceduresBlock(
            new[]
            {
                new MemorySteeringGuidance.KnownProcedureLine(
                    7,
                    "Maestro class login",
                    "before class",
                    "open Firefox → Maestro → sign in")
            });

        Assert.Contains("[Known procedures]", block, StringComparison.Ordinal);
        Assert.Contains("[7]", block, StringComparison.Ordinal);
        Assert.Contains("sop_get", block, StringComparison.Ordinal);
        Assert.Contains("do not invent a beginner path", block, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MatchProcedures_ReturnsEmptyWhenNoOverlap()
    {
        var matches = MemorySteeringGuidance.MatchProcedures(
            "what time is it",
            Array.Empty<string>(),
            new (long, string, string, string)[]
            {
                (1, "Maestro class login", "class", "open Firefox"),
            });

        Assert.Empty(matches);
    }
}
