using SoulCore.Inference.Presence;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Protocol.Tests;

public class PresenceActivityHubTests
{
    [Fact]
    public void GetSnapshot_DefaultsToWithHerself()
    {
        var hub = new PresenceActivityHub(new DesktopViewHub(() => true, Path.Combine(Path.GetTempPath(), "pa-" + Guid.NewGuid().ToString("N"))));
        var snap = hub.GetSnapshot();
        Assert.Equal("With herself", snap.Phrase);
        Assert.Equal("life", snap.Source);
    }

    [Fact]
    public void NoteChat_UserWinsOverLifeLine()
    {
        var hub = new PresenceActivityHub(new DesktopViewHub(() => true, Path.Combine(Path.GetTempPath(), "pa-" + Guid.NewGuid().ToString("N"))));
        hub.NoteChat("user");
        var snap = hub.GetSnapshot();
        Assert.Equal("Listening to Kurt", snap.Phrase);
        Assert.Equal("chat", snap.Source);
    }

    [Fact]
    public void NoteChat_AssistantIsInConversation()
    {
        var hub = new PresenceActivityHub(new DesktopViewHub(() => true, Path.Combine(Path.GetTempPath(), "pa-" + Guid.NewGuid().ToString("N"))));
        hub.NoteChat("assistant");
        Assert.Equal("In conversation", hub.GetSnapshot().Phrase);
    }

    [Fact]
    public void HumanizeDesktop_EyesPrefix()
    {
        var phrase = PresenceActivityHub.HumanizeDesktop("eyes", "eye_capture");
        Assert.Contains("Looking", phrase);
        Assert.Contains("eye_capture", phrase);
    }

    [Fact]
    public void MemorySightDir_IsNotScratchGallery()
    {
        var gallery = DesktopViewHub.DefaultGalleryDirectory();
        var memory = DesktopViewHub.DefaultMemorySightDirectory();
        Assert.NotEqual(gallery, memory);
        Assert.Contains("scratch", gallery);
        Assert.Contains("memory", memory);
        Assert.Contains("sight", memory);
    }
}
