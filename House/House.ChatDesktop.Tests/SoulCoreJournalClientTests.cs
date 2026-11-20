using House.ChatDesktop.Services;

namespace House.ChatDesktop.Tests;

public class SoulCoreJournalClientTests
{
    [Fact]
    public void JournalBookRow_Line_IncludesIdAndTitle()
    {
        var row = new JournalBookRow
        {
            Id = "thoughts",
            Title = "Thoughts",
            Purpose = "Private reflections"
        };

        Assert.Equal("Thoughts (thoughts)", row.Line);
        Assert.Contains("thoughts", row.Line, StringComparison.Ordinal);
    }

    [Fact]
    public void JournalEntryRow_Line_PrefersOccurredAtPreview()
    {
        var row = new JournalEntryRow
        {
            Id = 1,
            BookId = "thoughts",
            Body = "A quiet presence note for the desk.",
            OccurredAt = "2026-11-17T12:00:00.000Z",
            CreatedAt = "2026-11-17T12:01:00.000Z"
        };

        Assert.StartsWith("2026-11-17T12:00:00.000Z — ", row.Line, StringComparison.Ordinal);
        Assert.Contains("quiet presence", row.Line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResearchSubjectRow_DoesNotModelPrivateJournalBooks()
    {
        // Research papers stay on ResearchSubjectRow / ResearchPaperRow — no journal book id field.
        var subject = new ResearchSubjectRow
        {
            Id = 9,
            Title = "Neural interfaces",
            Summary = "hobby subject",
            PaperCount = 1
        };
        Assert.DoesNotContain("thoughts", subject.Line, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(typeof(long), subject.Id.GetType());
    }
}
