using SoulCore.Inference.Presence;

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
    public void Assignment_round_trips_through_the_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "aw-" + Guid.NewGuid().ToString("N") + ".json");
        var store = new FileAssignedWorkStore(path);
        Assert.Null(store.Current);
        store.SetActive("complete the next two lessons");
        var again = new FileAssignedWorkStore(path);
        Assert.Equal("active", again.Current!.Status);
        Assert.Equal("complete the next two lessons", again.Current.Text);
        again.MarkWaiting("Both lessons are done.");
        var waiting = new FileAssignedWorkStore(path);
        Assert.Equal("waiting", waiting.Current!.Status);
        waiting.Clear();
        Assert.Null(new FileAssignedWorkStore(path).Current);
    }
}
