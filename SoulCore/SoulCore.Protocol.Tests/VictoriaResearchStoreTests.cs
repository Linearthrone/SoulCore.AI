using SoulCore.Inference.Tools.Workflow;
using SoulCore.Memory;
using SoulCore.Memory.Repositories;
using System.Text.Json;

namespace SoulCore.Protocol.Tests;

public class VictoriaResearchStoreTests
{
    [Fact]
    public async Task SubjectAndPaper_RoundTrip_ApaFields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-research-{Guid.NewGuid():N}.db");
        try
        {
            using var session = new SqliteMemorySession(path);
            IVictoriaResearchStore research = new SqliteVictoriaResearchRepository(session);

            var subject = await research.OpenOrCreateSubjectAsync("Neural Interfaces", "BCI thread");
            Assert.True(subject.Id > 0);
            Assert.Equal("neural-interfaces", subject.Slug);

            var again = await research.OpenOrCreateSubjectAsync("Neural Interfaces");
            Assert.Equal(subject.Id, again.Id);

            var paper = await research.UpsertPaperAsync(
                subject.Id,
                "Spike Timing in Cortical Prosthetics",
                "review",
                "This review surveys encoding schemes for neural interfaces.",
                "Introduction\nPrior work on spike timing…",
                "Author, A. (2024). Title. Journal, 1(2), 3–4.",
                "draft");
            Assert.True(paper.Id > 0);
            Assert.Equal("review", paper.Kind);
            Assert.Contains("spike timing", paper.BodyApa, StringComparison.OrdinalIgnoreCase);

            var listed = await research.ListPapersAsync(subject.Id);
            Assert.Single(listed);

            var write = new ResearchPaperWriteTool(research);
            var updated = await write.ExecuteAsync(JsonDocument.Parse(
                $$"""
                {
                  "subject_id": {{subject.Id}},
                  "paper_id": {{paper.Id}},
                  "title": "Spike Timing in Cortical Prosthetics",
                  "kind": "theory",
                  "abstract": "A formal account of timing codes.",
                  "body_apa": "Theory\nLet S(t) denote…",
                  "bibliography_apa": "Author, A. (2024). Title. Journal, 1(2), 3–4.\nBeta, B. (2023). Other. Press.",
                  "status": "draft"
                }
                """).RootElement.Clone());
            Assert.True(updated.Success);

            var got = await research.GetPaperAsync(paper.Id);
            Assert.NotNull(got);
            Assert.Equal("theory", got!.Kind);
            Assert.Contains("Beta, B.", got.BibliographyApa, StringComparison.Ordinal);

            var subjects = await research.ListSubjectsAsync();
            Assert.Contains(subjects, s => s.Id == subject.Id && s.PaperCount == 1);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void Slugify_NormalizesTitle()
    {
        Assert.Equal("neural-interfaces", VictoriaResearchRules.Slugify("Neural Interfaces"));
        Assert.Equal("bci-v2", VictoriaResearchRules.Slugify("  BCI  v2  "));
    }
}
