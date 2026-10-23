namespace House.ChatDesktop.Services;

public enum PresenceHotkeyAction
{
    ToggleChatDrawer = 1,
    Exit = 2
}

/// <summary>
/// PROP-16.1 chords. Ctrl+Shift+H toggles the chat drawer. Ctrl+Shift+Q exits.
/// Extra modifiers do not match, so the plan stays the PM lock.
/// </summary>
public static class PresenceHotkeyPlan
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;
    public const uint ModNoRepeat = 0x4000;

    public const uint VkH = 0x48;
    public const uint VkQ = 0x51;

    public const int ToggleChatId = (int)PresenceHotkeyAction.ToggleChatDrawer;
    public const int ExitId = (int)PresenceHotkeyAction.Exit;

    private const uint RelevantMods = ModAlt | ModControl | ModShift | ModWin;

    public static bool HideFromTaskbar(bool hotkeysRegistered) => hotkeysRegistered;

    public static bool TryFromId(int id, out PresenceHotkeyAction action)
    {
        if (id == ToggleChatId)
        {
            action = PresenceHotkeyAction.ToggleChatDrawer;
            return true;
        }

        if (id == ExitId)
        {
            action = PresenceHotkeyAction.Exit;
            return true;
        }

        action = default;
        return false;
    }

    public static bool TryMatch(uint modifiers, uint virtualKey, out PresenceHotkeyAction action)
    {
        var mods = modifiers & RelevantMods;
        var chord = mods == (ModControl | ModShift);
        if (chord && virtualKey == VkH)
        {
            action = PresenceHotkeyAction.ToggleChatDrawer;
            return true;
        }

        if (chord && virtualKey == VkQ)
        {
            action = PresenceHotkeyAction.Exit;
            return true;
        }

        action = default;
        return false;
    }
}

public static class PresenceTrayMenu
{
    public const string OpenChat = "Open main drawer";
    public const string OpenSettings = "Open settings";
    public const string Exit = "Exit";

    public static IReadOnlyList<string> Items { get; } =
    [
        OpenChat,
        OpenSettings,
        Exit
    ];
}
