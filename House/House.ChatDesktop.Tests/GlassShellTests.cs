using House.ChatDesktop.Models;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Tests;

public class GlassShellTests
{
    [Fact]
    public void TranscriptKey_IsPerPersona_AndNotTheLegacySharedThread()
    {
        Assert.Equal("persona:alpha", ChatTranscriptKey.ForPersona(" alpha "));
        Assert.Equal("persona:none", ChatTranscriptKey.ForPersona("  "));
        Assert.NotEqual(ChatTranscriptKey.ForPersona("alpha"), ChatTranscriptKey.ForPersona("beta"));
        Assert.NotEqual(ChatHistoryStore.PresenceConversationId, ChatTranscriptKey.ForPersona("alpha"));
    }

    [Fact]
    public void SecondPersona_DoesNotReadFirstTranscript()
    {
        var path = Path.Combine(Path.GetTempPath(), "presence-chat-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using var store = new ChatHistoryStore(path);
            store.Save(new ChatMessage { Role = "user", Text = "alpha-secret" }, ChatTranscriptKey.ForPersona("alpha"));
            store.Save(new ChatMessage { Role = "assistant", Text = "beta-secret" }, ChatTranscriptKey.ForPersona("beta"));

            var alpha = store.LoadRecent(ChatTranscriptKey.ForPersona("alpha"));
            var beta = store.LoadRecent(ChatTranscriptKey.ForPersona("beta"));

            Assert.Contains(alpha, message => message.Text == "alpha-secret");
            Assert.DoesNotContain(alpha, message => message.Text == "beta-secret");
            Assert.Contains(beta, message => message.Text == "beta-secret");
            Assert.DoesNotContain(beta, message => message.Text == "alpha-secret");
            Assert.Empty(store.LoadRecent(ChatHistoryStore.PresenceConversationId));
            Assert.Throws<ArgumentException>(() =>
                store.Save(new ChatMessage { Role = "user", Text = "shared" }, ChatHistoryStore.PresenceConversationId));
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Theory]
    [InlineData(PresenceHotkeyPlan.ModControl | PresenceHotkeyPlan.ModShift, PresenceHotkeyPlan.VkH, PresenceHotkeyAction.ToggleChatDrawer)]
    [InlineData(PresenceHotkeyPlan.ModControl | PresenceHotkeyPlan.ModShift | PresenceHotkeyPlan.ModNoRepeat, PresenceHotkeyPlan.VkQ, PresenceHotkeyAction.Exit)]
    public void HotkeyPlan_MatchesLockedChords(uint mods, uint vk, PresenceHotkeyAction expected)
    {
        Assert.True(PresenceHotkeyPlan.TryMatch(mods, vk, out var action));
        Assert.Equal(expected, action);
    }

    [Fact]
    public void HotkeyPlan_RejectsPartialChords_AndHidesTaskbarOnlyWhenArmed()
    {
        Assert.False(PresenceHotkeyPlan.TryMatch(PresenceHotkeyPlan.ModControl, PresenceHotkeyPlan.VkH, out _));
        Assert.False(PresenceHotkeyPlan.TryMatch(
            PresenceHotkeyPlan.ModControl | PresenceHotkeyPlan.ModShift | PresenceHotkeyPlan.ModAlt,
            PresenceHotkeyPlan.VkQ,
            out _));
        Assert.True(PresenceHotkeyPlan.TryFromId(PresenceHotkeyPlan.ToggleChatId, out var toggle));
        Assert.Equal(PresenceHotkeyAction.ToggleChatDrawer, toggle);
        Assert.True(PresenceHotkeyPlan.HideFromTaskbar(true));
        Assert.False(PresenceHotkeyPlan.HideFromTaskbar(false));
    }

    [Fact]
    public void TrayMenu_OffersOpenChat_OpenSettings_AndExit()
    {
        Assert.Equal(
            new[] { "Open main drawer", "Open settings", "Exit" },
            PresenceTrayMenu.Items);
    }

    [Fact]
    public void Favorites_CapAtSix_AndRejectDuplicateNames()
    {
        var slots = new List<FavoriteSlot>();
        for (var i = 0; i < 6; i++)
        {
            Assert.True(FavoriteRoster.TryAdd(slots, "id-" + i, "Name " + i, null, out var error), error);
        }

        Assert.False(FavoriteRoster.TryAdd(slots, "id-7", "Name 7", null, out var capError));
        Assert.Contains("six", capError, StringComparison.OrdinalIgnoreCase);
        Assert.False(FavoriteRoster.TryAdd(slots, "id-other", "name 0", null, out var nameError));
        Assert.Contains("unique", nameError, StringComparison.OrdinalIgnoreCase);

        var normalized = FavoriteRoster.Normalize(
        [
            new FavoriteSlot { PersonaId = "a", DisplayName = "Iris" },
            new FavoriteSlot { PersonaId = "b", DisplayName = "iris" },
            new FavoriteSlot { PersonaId = "a", DisplayName = "Other" }
        ]);
        Assert.Single(normalized);
        Assert.Equal("Iris", normalized[0].DisplayName);
    }

    [Fact]
    public void ChromeOpacity_FloorsAt055_AndLightTextDiffersFromDark()
    {
        Assert.Equal(GlassChrome.OpacityFloor, GlassChrome.ClampOpacity(0.2));
        Assert.Equal(1d, GlassChrome.ClampOpacity(2));
        var light = GlassChrome.CreatePalette(light: true, opacity: 0.2);
        var dark = GlassChrome.CreatePalette(light: false, opacity: 0.9);
        Assert.Equal(GlassChrome.OpacityFloor, light.Opacity);
        Assert.True(light.Shell.A >= (byte)Math.Round(GlassChrome.OpacityFloor * 255));
        Assert.True(dark.Shell.A >= (byte)Math.Round(0.9 * 255) - 1);
        Assert.NotEqual(light.Text, dark.Text);
        Assert.Equal(255, light.Text.A);
        Assert.Equal(255, dark.Text.A);

        var settings = new LocalUiSettings { ChromeOpacity = 0.1, Theme = "light" };
        settings.Normalize();
        Assert.Equal(GlassChrome.OpacityFloor, settings.ChromeOpacity);
        Assert.Equal(GlassChrome.LightTheme, settings.Theme);
    }

    [Fact]
    public void Win32Hotkeys_RegisterWhenTheChordIsFree()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var host = new PresenceHotkeyHost();
        var armed = host.TryRegister(out var detail);
        if (!armed && detail.Contains("RegisterHotKey", StringComparison.Ordinal))
            return;

        Assert.True(armed, detail);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // The temp db can stay if SQLite still has the handle for a moment.
        }
    }
}
