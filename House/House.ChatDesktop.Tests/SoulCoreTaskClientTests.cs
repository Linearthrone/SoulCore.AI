using House.ChatDesktop.Services;

namespace House.ChatDesktop.Tests;

public class SoulCoreTaskClientTests
{
    [Fact]
    public void ParseListPayload_ReadsTasksArray()
    {
        var fetch = SoulCoreTaskClient.ParseListPayload(
            """{"count":2,"tasks":[{"id":1,"title":"A","status":"todo","surface":"local"},{"id":2,"title":"B","status":"suspended","surface":"vm"}]}""");

        Assert.Null(fetch.Problem);
        Assert.Equal(2, fetch.Tasks.Count);
        Assert.Equal(1, fetch.Tasks[0].Id);
        Assert.Equal("A", fetch.Tasks[0].Title);
        Assert.Equal("todo", fetch.Tasks[0].Status);
        Assert.Contains("todo", fetch.Tasks[0].Line, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseListPayload_EmptyArray_IsOkNotProblem()
    {
        var fetch = SoulCoreTaskClient.ParseListPayload("""{"count":0,"tasks":[]}""");
        Assert.Null(fetch.Problem);
        Assert.Empty(fetch.Tasks);
    }

    [Fact]
    public void ParseListPayload_MissingTasks_IsProblemNotSilentEmpty()
    {
        var fetch = SoulCoreTaskClient.ParseListPayload("""{"ok":true}""");
        Assert.NotNull(fetch.Problem);
        Assert.Contains("missing tasks", fetch.Problem!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fetch.Tasks);
    }

    [Fact]
    public void ParseListPayload_NullTasks_IsProblem()
    {
        var fetch = SoulCoreTaskClient.ParseListPayload("""{"tasks":null}""");
        Assert.NotNull(fetch.Problem);
        Assert.Contains("null", fetch.Problem!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseListPayload_CaseInsensitiveKeys()
    {
        var fetch = SoulCoreTaskClient.ParseListPayload(
            """{"Tasks":[{"Id":7,"Title":"X","Status":"in_progress","Surface":"browser"}]}""");
        Assert.Null(fetch.Problem);
        Assert.Single(fetch.Tasks);
        Assert.Equal(7, fetch.Tasks[0].Id);
        Assert.Equal("X", fetch.Tasks[0].Title);
    }
}
