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
    public void Empty_after_tools_line_keeps_work_going()
    {
        Assert.True(WorkContinuation.ShouldKeepGoing(
            "complete the lesson",
            WorkContinuation.EmptyAfterToolsLine,
            usedWorkTools: true));
        // Even when Host toolTrace missed the work tool, the marker must continue.
        Assert.True(WorkContinuation.ShouldKeepGoing(
            "complete the lesson",
            WorkContinuation.EmptyAfterToolsLine,
            usedWorkTools: false));
        Assert.True(WorkContinuation.HitEmptyAfterTools(WorkContinuation.EmptyAfterToolsLine));
        Assert.Equal(
            WorkContinuation.ContinuingLine,
            WorkContinuation.VisibleText(WorkContinuation.EmptyAfterToolsLine, continuing: true));
        Assert.True(WorkContinuation.MaxExtraPasses >= 12);
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
    public void Go_ahead_starts_existing_work_and_is_not_a_new_task()
    {
        const string pulledUp = "the lesson is pulled up you can go ahead and start";
        Assert.True(WorkContinuation.IsGreenLight(pulledUp));
        Assert.True(WorkContinuation.IsGreenLight("go ahead"));
        Assert.False(WorkContinuation.IsGreenLight("complete week 5 lesson 10"));
        Assert.False(WorkContinuation.IsGreenLight("add a task to review the charter"));
        Assert.True(WorkContinuation.IsChatLoggedAsTask(pulledUp));
        Assert.True(WorkContinuation.IsChatLoggedAsTask(
            "why don't you take a break for a while baby, you have done a lot"));
        Assert.False(WorkContinuation.IsChatLoggedAsTask("Review the charter"));
    }

    [Fact]
    public void Next_open_task_prefers_in_progress_then_todo_then_suspended()
    {
        var rows = new[]
        {
            Row(3, "suspended"),
            Row(2, "todo"),
            Row(9, "in_progress"),
            Row(4, "done"),
            Row(1, "parked"),
        };

        Assert.Equal(9, WorkContinuation.NextOpenTask(rows)!.Id);

        var waiting = rows.Where(r => r.Id != 9).ToArray();
        Assert.Equal(2, WorkContinuation.NextOpenTask(waiting)!.Id);

        var held = waiting.Where(r => r.Id != 2).ToArray();
        Assert.Equal(3, WorkContinuation.NextOpenTask(held)!.Id);
    }

    private static VictoriaTask Row(long id, string status) =>
        new(id, "t" + id, "", status, "medium", "", "");

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
