using System.Text.Json;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Tests;

public class PersonaModeTests
{
    [Fact]
    public void MissingGet_ShowsOne_OnRadialAndSettings()
    {
        var board = new PersonaModeBoard();
        var radial = new PersonaModeControlState();
        var settings = new PersonaModeControlState();
        board.Changed += () =>
        {
            radial.Apply(board.Displayed);
            settings.Apply(board.Displayed);
        };

        Assert.Equal(PersonaChatMode.One, board.Displayed);
        Assert.Equal("One", radial.ShownLabel);
        Assert.Equal("One", settings.ShownLabel);
        Assert.True(radial.OneSelected);
        Assert.True(settings.OneSelected);

        board.ApplyGet(ok: false, mode: null);
        Assert.Equal("One", radial.ShownLabel);
        Assert.Equal("One", settings.ShownLabel);
        Assert.Equal(radial.ShownMode, settings.ShownMode);
    }

    [Fact]
    public void GetMany_ShowsMany_OnBothControls()
    {
        var board = new PersonaModeBoard();
        var radial = new PersonaModeControlState();
        var settings = new PersonaModeControlState();
        board.Changed += () =>
        {
            radial.Apply(board.Displayed);
            settings.Apply(board.Displayed);
        };

        board.ApplyGet(ok: true, mode: "many");
        Assert.Equal(PersonaChatMode.Many, radial.ShownMode);
        Assert.Equal(PersonaChatMode.Many, settings.ShownMode);
        Assert.Equal("Many", radial.ShownLabel);
        Assert.Equal("Many", settings.ShownLabel);
        Assert.True(radial.ManySelected);
        Assert.True(settings.ManySelected);
    }

    [Fact]
    public void FailedPost_RestoresPreviousMode_OnBothControls()
    {
        var board = new PersonaModeBoard();
        var radial = new PersonaModeControlState();
        var settings = new PersonaModeControlState();
        board.Changed += () =>
        {
            radial.Apply(board.Displayed);
            settings.Apply(board.Displayed);
        };

        board.ApplyGet(ok: true, mode: PersonaChatMode.One);
        Assert.True(board.TryBeginChoice(PersonaChatMode.Many, out var previous));
        Assert.Equal(PersonaChatMode.One, previous);
        Assert.Equal("Many", radial.ShownLabel);
        Assert.Equal("Many", settings.ShownLabel);

        board.Restore(previous);
        Assert.Equal("One", radial.ShownLabel);
        Assert.Equal("One", settings.ShownLabel);
        Assert.Equal(radial.ShownMode, settings.ShownMode);

        board.ApplyGet(ok: true, mode: PersonaChatMode.Many);
        Assert.True(board.TryBeginChoice(PersonaChatMode.One, out var many));
        Assert.Equal("One", radial.ShownLabel);
        Assert.Equal("One", settings.ShownLabel);
        board.Restore(many);
        Assert.Equal("Many", radial.ShownLabel);
        Assert.Equal("Many", settings.ShownLabel);
    }

    [Fact]
    public void ModePostBody_IsOneOrMany()
    {
        Assert.Equal("{\"mode\":\"one\"}", SoulCorePersonaClient.ModeRequestJson("one"));
        Assert.Equal("{\"mode\":\"many\"}", SoulCorePersonaClient.ModeRequestJson("many"));
        Assert.Equal("{\"mode\":\"one\"}", SoulCorePersonaClient.ModeRequestJson("One"));
        Assert.EndsWith("/api/personas/mode", SoulCorePersonaClient.ModeUri.AbsolutePath);
    }

    [Fact]
    public void OneMode_SendsForeground_AndDoesNotSendADifferentOpenThread()
    {
        var same = ChatSendPin.Resolve(PersonaChatMode.One, "victoria", "victoria");
        Assert.True(same.Send);
        Assert.False(same.Activate);
        Assert.Equal("victoria", same.PersonaId);

        var implied = ChatSendPin.Resolve(PersonaChatMode.One, "victoria", null);
        Assert.True(implied.Send);
        Assert.False(implied.Activate);
        Assert.Equal("victoria", implied.PersonaId);

        var other = ChatSendPin.Resolve(PersonaChatMode.One, "victoria", "iris");
        Assert.False(other.Send);
        Assert.False(other.Activate);
        Assert.Null(other.PersonaId);

        var frame = SoulCoreWsClient.BuildChatSendFrame("hello", "quoted bit", same.PersonaId);
        using var doc = JsonDocument.Parse(frame.ToJson());
        var payload = doc.RootElement.GetProperty("payload");
        Assert.Equal("chat.send", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("hello", payload.GetProperty("text").GetString());
        Assert.Equal("quoted bit", payload.GetProperty("quotedText").GetString());
        Assert.Equal("presence-local", payload.GetProperty("sessionId").GetString());
        Assert.Equal("victoria", payload.GetProperty("personaId").GetString());
    }

    [Fact]
    public void ManyMode_SendsOpenThread_AndDoesNotActivate()
    {
        var pin = ChatSendPin.Resolve(PersonaChatMode.Many, "victoria", "iris");
        Assert.True(pin.Send);
        Assert.False(pin.Activate);
        Assert.Equal("iris", pin.PersonaId);

        var foreground = ChatSendPin.Resolve(PersonaChatMode.Many, "victoria", "victoria");
        Assert.True(foreground.Send);
        Assert.False(foreground.Activate);
        Assert.Equal("victoria", foreground.PersonaId);

        var missing = ChatSendPin.Resolve(PersonaChatMode.Many, "victoria", "  ");
        Assert.False(missing.Send);
        Assert.False(missing.Activate);

        var frame = SoulCoreWsClient.BuildChatSendFrame("background", null, pin.PersonaId);
        using var doc = JsonDocument.Parse(frame.ToJson());
        var payload = doc.RootElement.GetProperty("payload");
        Assert.Equal("iris", payload.GetProperty("personaId").GetString());
        Assert.Equal("presence-local", payload.GetProperty("sessionId").GetString());
        Assert.False(payload.TryGetProperty("quotedText", out _));
    }

    [Fact]
    public void ManyBackgroundFocus_DoesNotRenameHouseOrViewport()
    {
        var chrome = new ForegroundChromeNames("Victoria");
        var focus = ThreadFocus.Resolve(PersonaChatMode.Many, "victoria", "iris");
        Assert.False(focus.Activate);

        var names = chrome.NamesAfterFocus(PersonaChatMode.Many, "iris", "victoria", "Iris");
        Assert.Equal("Victoria", names.House);
        Assert.Equal("Victoria", names.Viewport);
        Assert.Equal("Victoria", chrome.House);
        Assert.Equal("Victoria", chrome.Viewport);
        Assert.Equal("HOUSE · Victoria", ForegroundChromeNames.HouseTitle(chrome.House));
        Assert.StartsWith("Victoria · ", ForegroundChromeNames.ViewportTitle(chrome.Viewport));
    }

    [Fact]
    public void OneFocus_MayActivate_AndNamesStayForegroundUntilTheSwitchLands()
    {
        var chrome = new ForegroundChromeNames("Victoria");
        var focus = ThreadFocus.Resolve(PersonaChatMode.One, "victoria", "iris");
        Assert.True(focus.Activate);

        var during = chrome.NamesAfterFocus(PersonaChatMode.One, "iris", "victoria", "Iris");
        Assert.Equal("Victoria", during.House);
        Assert.Equal("Victoria", during.Viewport);

        chrome.ApplyForeground("Iris");
        Assert.Equal("Iris", chrome.House);
        Assert.Equal("Iris", chrome.Viewport);
        Assert.Equal("HOUSE · Iris", ForegroundChromeNames.HouseTitle(chrome.House));
    }
}
