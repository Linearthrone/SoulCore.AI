using House.ChatDesktop.Models;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Tests;

public sealed class ThreadArchiveTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly string _path;
    private readonly ChatHistoryStore _store;

    public ThreadArchiveTests()
    {
        var dir = Path.Combine(Path.GetTempPath(), "house-thread-archive");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".db");
        _store = new ChatHistoryStore(_path);
    }

    public void Dispose()
    {
        _store.Dispose();
        TryDelete(_path);
        TryDelete(_path + "-wal");
        TryDelete(_path + "-shm");
    }

    [Fact]
    public async Task FailedPost_DoesNotHideTheThread()
    {
        Save("alpha", "user", "keep-user", 1);
        Save("alpha", "assistant", "keep-assistant", 2);
        var key = ChatTranscriptKey.ForPersona("alpha");
        var before = Texts(_store.LoadRecent(key));

        string? posted = null;
        var outcome = await ThreadArchive.ExecuteAsync(
            _store,
            "alpha",
            (summary, _) =>
            {
                posted = summary;
                return Task.FromResult(false);
            });

        Assert.False(outcome.Hidden);
        Assert.Equal("keep-user\nkeep-assistant", posted);
        Assert.Equal(before, Texts(_store.LoadRecent(key)));
        Assert.True(_store.IsThreadListed(key));
        Assert.Equal(2, _store.CountStored(key));

        var thrown = await ThreadArchive.ExecuteAsync(
            _store,
            "alpha",
            (_, _) => throw new InvalidOperationException("host down"));

        Assert.False(thrown.Hidden);
        Assert.Equal(before, Texts(_store.LoadRecent(key)));
        Assert.True(_store.IsThreadListed(key));
        Assert.Equal(2, _store.CountStored(key));
    }

    [Fact]
    public async Task SuccessfulPost_HidesRows_DropsThread_AndKeepsThemInTheDb()
    {
        Save("alpha", "user", "first", 1);
        Save("alpha", "assistant", "one", 2);
        Save("alpha", "system", "noise", 3);
        Save("alpha", "user", "  second  ", 4);
        Save("alpha", "assistant", "  two  ", 5);
        var key = ChatTranscriptKey.ForPersona("alpha");
        var storedBefore = _store.CountStored(key);

        var listedDuringPost = true;
        var outcome = await ThreadArchive.ExecuteAsync(
            _store,
            "alpha",
            (summary, _) =>
            {
                listedDuringPost = _store.IsThreadListed(key);
                Assert.Equal("second\ntwo", summary);
                return Task.FromResult(true);
            });

        Assert.True(listedDuringPost);
        Assert.True(outcome.Hidden);
        Assert.Equal(key, outcome.TranscriptKey);
        Assert.Equal("second\ntwo", outcome.Summary);
        Assert.False(_store.IsThreadListed(key));
        Assert.Empty(_store.LoadRecent(key));
        Assert.Equal(storedBefore, _store.CountStored(key));

        var kept = _store.LoadIncludingHidden(key);
        Assert.Equal(storedBefore, kept.Count);
        Assert.Contains(kept, message => message.Text == "first");
        Assert.Contains(kept, message => message.Text == "  second  ");
        Assert.Contains(kept, message => message.Text == "noise");
    }

    [Fact]
    public async Task LaterMessage_ShowsOnlyRowsAfterTheMark()
    {
        Save("alpha", "user", "old-user", 1);
        Save("alpha", "assistant", "old-assistant", 2);
        var key = ChatTranscriptKey.ForPersona("alpha");

        var first = await ThreadArchive.ExecuteAsync(_store, "alpha", (_, _) => Task.FromResult(true));
        Assert.True(first.Hidden);
        Assert.Equal("old-user\nold-assistant", first.Summary);
        Assert.Empty(_store.LoadRecent(key));
        Assert.False(_store.IsThreadListed(key));

        Save("alpha", "user", "new-user", 10);
        Save("alpha", "assistant", "new-assistant", 11);

        Assert.True(_store.IsThreadListed(key));
        Assert.Equal(["new-user", "new-assistant"], Texts(_store.LoadRecent(key)));
        Assert.Equal(4, _store.CountStored(key));
        Assert.Equal(
            ["old-user", "old-assistant", "new-user", "new-assistant"],
            Texts(_store.LoadIncludingHidden(key)));

        var second = await ThreadArchive.ExecuteAsync(
            _store,
            "alpha",
            (_, _) => Task.FromResult(false));

        Assert.False(second.Hidden);
        Assert.Equal("new-user\nnew-assistant", second.Summary);
        Assert.Equal(["new-user", "new-assistant"], Texts(_store.LoadRecent(key)));
        Assert.True(_store.IsThreadListed(key));
        Assert.Equal(4, _store.CountStored(key));
    }

    [Fact]
    public async Task OtherPersona_TranscriptIsUnchanged()
    {
        Save("beta", "user", "beta-user", 1);
        Save("beta", "assistant", "beta-assistant", 2);
        Save("alpha", "user", "alpha-user", 3);
        Save("alpha", "assistant", "alpha-assistant", 4);

        var beta = ChatTranscriptKey.ForPersona("beta");
        var alpha = ChatTranscriptKey.ForPersona("alpha");
        var betaBefore = _store.LoadRecent(beta).Select(message => (message.Id, message.Role, message.Text)).ToArray();
        var betaCount = _store.CountStored(beta);

        var outcome = await ThreadArchive.ExecuteAsync(_store, "alpha", (_, _) => Task.FromResult(true));

        Assert.True(outcome.Hidden);
        Assert.False(_store.IsThreadListed(alpha));
        Assert.Empty(_store.LoadRecent(alpha));
        Assert.True(_store.IsThreadListed(beta));
        Assert.Equal(betaCount, _store.CountStored(beta));
        Assert.Equal(
            betaBefore,
            _store.LoadRecent(beta).Select(message => (message.Id, message.Role, message.Text)).ToArray());
        Assert.Equal(["beta-user", "beta-assistant"], Texts(_store.LoadIncludingHidden(beta)));
    }

    [Fact]
    public async Task EmptyExchange_SendsArchived_AndALaterMessageComesBack()
    {
        Save("alpha", "user", "   ", 1);
        Save("alpha", "assistant", "\n\t", 2);
        var key = ChatTranscriptKey.ForPersona("alpha");
        Assert.True(_store.IsThreadListed(ChatTranscriptKey.ForPersona("never-archived")));

        string? posted = null;
        var outcome = await ThreadArchive.ExecuteAsync(
            _store,
            "alpha",
            (summary, _) =>
            {
                posted = summary;
                return Task.FromResult(true);
            });

        Assert.Equal("Archived.", posted);
        Assert.True(outcome.Hidden);
        Assert.False(_store.IsThreadListed(key));
        Assert.Empty(_store.LoadRecent(key));
        Assert.Equal(2, _store.CountStored(key));

        Save("alpha", "user", "after", 5);
        Assert.True(_store.IsThreadListed(key));
        Assert.Equal(["after"], Texts(_store.LoadRecent(key)));
        Assert.Equal(3, _store.LoadIncludingHidden(key).Count);
    }

    [Fact]
    public void Summary_Trims_JoinsLastLines_AndCapsAt2000()
    {
        Assert.Equal("hello\nthere", ThreadArchive.BuildSummary("  hello  ", "  there  "));
        Assert.Equal("only-user", ThreadArchive.BuildSummary(" only-user ", "   "));
        Assert.Equal("only-assistant", ThreadArchive.BuildSummary(null, " only-assistant "));
        Assert.Equal("Archived.", ThreadArchive.BuildSummary(" ", null));
        Assert.Equal("Archived.", ThreadArchive.BuildSummary(null, null));

        var exact = ThreadArchive.BuildSummary(new string('u', 1000), new string('a', 999));
        Assert.Equal(2000, exact.Length);

        var user = new string('u', 1500);
        var assistant = new string('a', 1500);
        var capped = ThreadArchive.BuildSummary(user, assistant);
        Assert.Equal(2000, capped.Length);
        Assert.StartsWith(user, capped, StringComparison.Ordinal);
        Assert.Equal(2000, ThreadArchive.BuildSummary(new string('x', 2001), "").Length);
    }

    [Fact]
    public void ArchiveRequest_PostsSummaryObjectOnThePersonaRoute()
    {
        Assert.Equal("{\"summary\":\"Archived.\"}", SoulCorePersonaClient.ArchiveRequestJson("Archived."));
        Assert.Equal("{\"summary\":\"a\\nb\"}", SoulCorePersonaClient.ArchiveRequestJson("a\nb"));

        var uri = SoulCorePersonaClient.ArchiveUri("mentor");
        Assert.EndsWith("/api/personas/mentor/archive", uri.AbsoluteUri, StringComparison.Ordinal);
    }

    private void Save(string personaId, string role, string text, int offsetSeconds)
    {
        _store.Save(
            new ChatMessage
            {
                Role = role,
                Text = text,
                At = T0.AddSeconds(offsetSeconds)
            },
            ChatTranscriptKey.ForPersona(personaId));
    }

    private static string[] Texts(IReadOnlyList<ChatMessage> rows) =>
        rows.Select(message => message.Text).ToArray();

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // The next test uses its own file.
        }
    }
}
