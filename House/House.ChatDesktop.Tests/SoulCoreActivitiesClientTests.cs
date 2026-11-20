using House.ChatDesktop.Services;

namespace House.ChatDesktop.Tests;

public class SoulCoreActivitiesClientTests
{
    [Fact]
    public void ParseActivitiesList_ReadsDeskColumns()
    {
        var fetch = SoulCoreActivitiesClient.ParseActivitiesList(
            """
            {"count":1,"activities":[{
              "entryNo":12,
              "occurredAt":"2026-11-19T10:00:00.000Z",
              "activity":"leisure",
              "subject":"neural lace",
              "desc":"Found a new paper on soft electrodes."
            }]}
            """);

        Assert.Null(fetch.Problem);
        Assert.Single(fetch.Activities);
        var row = fetch.Activities[0];
        Assert.Equal(12, row.EntryNo);
        Assert.Equal("12", row.EntryText);
        Assert.Equal("2026-11-19T10:00:00.000Z", row.DateTimeText);
        Assert.Equal("leisure", row.ActivityText);
        Assert.Equal("neural lace", row.SubjectText);
        Assert.Contains("soft electrodes", row.DescText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseActivitiesList_MissingArray_IsProblem()
    {
        var fetch = SoulCoreActivitiesClient.ParseActivitiesList("""{"count":0}""");
        Assert.NotNull(fetch.Problem);
        Assert.Empty(fetch.Activities);
    }

    [Fact]
    public void ParseWhileYouWereGone_EmptyIsHonest()
    {
        var fetch = SoulCoreActivitiesClient.ParseWhileYouWereGone(
            """{"since":"2026-11-19T00:00:00.000Z","empty":true,"activities":[],"journalCrumbs":[],"activityCount":0,"journalCount":0}""");

        Assert.Null(fetch.Problem);
        Assert.True(fetch.Empty);
        Assert.Empty(fetch.Activities);
        Assert.Empty(fetch.JournalCrumbs);
        Assert.Equal(
            "Nothing stored while you were gone — empty is honest.",
            PresenceResidueFormatter.FormatWhileYouWereGoneBody(fetch));
    }

    [Fact]
    public void ParseWhileYouWereGone_ListsResidue()
    {
        var fetch = SoulCoreActivitiesClient.ParseWhileYouWereGone(
            """
            {"since":"2026-11-19T00:00:00.000Z","empty":false,
             "activities":[{"entryNo":3,"occurredAt":"2026-11-19T01:00:00.000Z","activity":"research","subject":"optics","desc":"Compared two lens papers."}],
             "journalCrumbs":[{"id":9,"bookId":"thoughts","bodyPreview":"I kept turning over the lens math.","tagsJson":"[\"optics\"]","occurredAt":"2026-11-19T01:05:00.000Z"}],
             "activityCount":1,"journalCount":1}
            """);

        Assert.Null(fetch.Problem);
        Assert.False(fetch.Empty);
        Assert.Single(fetch.Activities);
        Assert.Single(fetch.JournalCrumbs);
        var body = PresenceResidueFormatter.FormatWhileYouWereGoneBody(fetch);
        Assert.Contains("#3", body, StringComparison.Ordinal);
        Assert.Contains("journal", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lens", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("I kept turning over the lens math.", PresenceResidueFormatter.NewestJournalPreview(fetch));
    }

    [Fact]
    public void FormatHonestyLine_DoesNotFabricate()
    {
        var line = PresenceResidueFormatter.FormatHonestyLine(false, null, null, null);
        Assert.Contains("Alive — no", line, StringComparison.Ordinal);
        Assert.Contains("Last tick — none yet", line, StringComparison.Ordinal);
        Assert.Contains("Last want — none yet", line, StringComparison.Ordinal);
        Assert.Contains("Last journal — none yet", line, StringComparison.Ordinal);
    }

    [Fact]
    public void NewestJournalPreview_ActivitiesOnly_DoesNotUseDesc()
    {
        // PROP-18.31 F2: activities Desc must not masquerade as Last journal.
        var fetch = SoulCoreActivitiesClient.ParseWhileYouWereGone(
            """
            {"since":"2026-11-19T00:00:00.000Z","empty":false,
             "activities":[{"entryNo":3,"occurredAt":"2026-11-19T01:00:00.000Z","activity":"research","subject":"optics","desc":"Compared two lens papers."}],
             "journalCrumbs":[],"activityCount":1,"journalCount":0}
            """);

        Assert.Null(fetch.Problem);
        Assert.False(fetch.Empty);
        Assert.Single(fetch.Activities);
        Assert.Empty(fetch.JournalCrumbs);
        Assert.Null(PresenceResidueFormatter.NewestJournalPreview(fetch));

        var line = PresenceResidueFormatter.FormatHonestyLine(
            true,
            DateTimeOffset.Parse("2026-11-19T01:00:00Z"),
            "want optics",
            PresenceResidueFormatter.NewestJournalPreview(fetch));
        Assert.Contains("Last journal — none yet", line, StringComparison.Ordinal);
        Assert.DoesNotContain("lens papers", line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void JournalEntryRow_TopicTagHint_SurfacesTags()
    {
        var hint = JournalEntryRow.TopicTagHint("""["optics","research"]""");
        Assert.NotNull(hint);
        Assert.Contains("optics", hint, StringComparison.Ordinal);
        Assert.Null(JournalEntryRow.TopicTagHint("[]"));
    }
}
