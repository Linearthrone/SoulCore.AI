using SoulCore.Inference.Presence;
using SoulCore.Memory;

namespace SoulCore.Protocol.Tests;

public class WorkContinuationTests
{
    [Fact]
    public void Social_reply_does_not_continue()
    {
        Assert.False(WorkContinuation.ShouldKeepGoing("how do you feel", "I'm okay.", usedWorkTools: false));
    }

    [Fact]
    public void Work_without_handoff_continues()
    {
        Assert.True(WorkContinuation.ShouldKeepGoing(
            "complete the lesson",
            "The file is open. Next I'll add the component.",
            usedWorkTools: true));
    }

    [Fact]
    public void Handoff_stops()
    {
        Assert.False(WorkContinuation.ShouldKeepGoing(
            "keep going",
            "Standing by. Your call.",
            usedWorkTools: true));
    }

    [Fact]
    public void User_stop_stops_even_on_tool_cap()
    {
        Assert.False(WorkContinuation.ShouldKeepGoing(
            "stop",
            "[agent loop hit MaxToolIterations cap without a final text turn]",
            usedWorkTools: true));
    }

    [Fact]
    public void Tool_cap_continues_and_hides_the_marker()
    {
        var marker = "[agent loop hit MaxToolIterations cap without a final text turn]";
        Assert.True(WorkContinuation.ShouldKeepGoing("go ahead", marker, usedWorkTools: true));
        Assert.Equal(WorkContinuation.ContinuingLine, WorkContinuation.VisibleText(marker, continuing: true));
        Assert.Equal(WorkContinuation.PausedLine, WorkContinuation.VisibleText(marker, continuing: false));
    }

    [Fact]
    public void Screenshot_phrase_is_looking()
    {
        Assert.Equal("Looking at the screen", WorkContinuation.PhraseForTool("desktop_screenshot"));
        Assert.Equal("Reading a file", WorkContinuation.PhraseForTool("guest_read_file"));
    }

    [Fact]
    public void Lesson_instruction_is_an_assignment()
    {
        Assert.True(WorkContinuation.LooksLikeAssignment(
            "continue the current lesson on the maestro tab please, complete it and the next one then let me know"));
        Assert.False(WorkContinuation.LooksLikeAssignment("is everything ok?"));
        Assert.False(WorkContinuation.LooksLikeAssignment("stop"));
    }

    [Fact]
    public void Check_in_is_both_lessons_done_not_a_promise()
    {
        Assert.False(WorkContinuation.IsCheckIn("I'll let you know when both lessons are done."));
        Assert.True(WorkContinuation.IsCheckIn("Both lessons are done. Ready for the review."));
        Assert.True(WorkContinuation.NeedsKayleigh("I cannot proceed without you deciding the layout."));
    }

    [Fact]
    public async Task Assignment_pointer_round_trips_runningId_only()
    {
        var path = Path.Combine(Path.GetTempPath(), "aw-" + Guid.NewGuid().ToString("N") + ".json");
        var db = Path.Combine(Path.GetTempPath(), "aw-db-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            await using var tasks = new SqliteMemoryStore(db);
            var store = new FileAssignedWorkStore(path);
            Assert.Null(store.RunningId);

            var id = await tasks.CreateAsync(
                "complete the next two lessons",
                "complete the next two lessons",
                "high",
                "maestro",
                ready: true,
                status: "in_progress",
                notes: null);
            var row = await tasks.GetAsync(id);
            Assert.NotNull(row);
            store.Project(row!);
            Assert.Equal(id, store.RunningId);

            var again = new FileAssignedWorkStore(path);
            Assert.Equal(id, again.RunningId);
            Assert.True(File.ReadAllText(path).Contains("runningId", StringComparison.Ordinal));
            Assert.DoesNotContain("\"status\"", File.ReadAllText(path), StringComparison.Ordinal);

            again.Clear();
            Assert.Null(new FileAssignedWorkStore(path).RunningId);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
            try { File.Delete(db); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task Legacy_active_slot_migrates_to_task_row()
    {
        var path = Path.Combine(Path.GetTempPath(), "aw-legacy-" + Guid.NewGuid().ToString("N") + ".json");
        var db = Path.Combine(Path.GetTempPath(), "aw-legdb-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            File.WriteAllText(path,
                """
                {"text":"finish maestro lesson","status":"active","updatedAt":"2026-10-12T00:00:00Z"}
                """);
            await using var tasks = new SqliteMemoryStore(db);
            var store = new FileAssignedWorkStore(path);
            await store.MigrateLegacyAsync(tasks);
            Assert.NotNull(store.RunningId);
            var row = await tasks.GetAsync(store.RunningId!.Value);
            Assert.NotNull(row);
            Assert.Equal("in_progress", row!.Status);
            Assert.Contains("maestro", row.Title, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.ReadAllText(path).Contains("runningId", StringComparison.Ordinal));
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
            try { File.Delete(db); } catch { /* best-effort */ }
        }
    }
}
