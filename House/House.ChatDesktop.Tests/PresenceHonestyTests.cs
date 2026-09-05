using House.ChatDesktop;

namespace House.ChatDesktop.Tests;

public sealed class PresenceHonestyTests
{
    [Theory]
    [InlineData("want[recall]: recall the recent thread and weave it into presence.", "recall", "recall the recent thread and weave it into presence.")]
    [InlineData("want[idle]: sitting quietly (emotion=calm)", "idle", "sitting quietly")]
    [InlineData("want[chat]: talking with Kurt", "chat", "talking with Kurt")]
    public void ParseWantWire_strips_want_envelope_and_emotion_tail(string want, string? category, string expectedPhrase)
    {
        var (cat, phrase) = MainWindow.ParseWantWire(want, category);
        Assert.Equal(category, cat);
        Assert.Equal(expectedPhrase, phrase);
    }

    [Fact]
    public void ParseWantWire_empty_returns_category_only()
    {
        var (cat, phrase) = MainWindow.ParseWantWire(null, "rest");
        Assert.Equal("rest", cat);
        Assert.Equal(string.Empty, phrase);
    }

    [Theory]
    [InlineData("Hey — just wanted to say hi. You around?")]
    [InlineData("I've been thinking about you. Hope your day's okay.")]
    [InlineData("Sitting quietly. Wanted you to know I'm here.")]
    public void IsAutomatedProactiveLine_matches_soul_loop_phrase_bank(string line)
    {
        Assert.True(MainWindow.IsAutomatedProactiveLine(line));
    }

    [Theory]
    [InlineData("Kurt, I'm actually annoyed right now.")]
    [InlineData("")]
    [InlineData(null)]
    public void IsAutomatedProactiveLine_rejects_real_chat_lines(string? line)
    {
        Assert.False(MainWindow.IsAutomatedProactiveLine(line));
    }
}
