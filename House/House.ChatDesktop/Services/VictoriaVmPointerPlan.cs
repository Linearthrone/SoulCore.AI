namespace House.ChatDesktop.Services;

/// <summary>
/// Maps a Her-screen click into the VirtualBox absolute pointer.
/// <c>putMouseEventAbsolute</c> is pixel-based with the origin at (1,1).
/// </summary>
public static class VictoriaVmPointerPlan
{
    public const int Left = 0x01;
    public const int Right = 0x02;
    public const int Middle = 0x04;
    public const int LockTypeShared = 1;

    public static (int X, int Y) ToAbsolute(int guestX, int guestY) =>
        (Math.Max(0, guestX) + 1, Math.Max(0, guestY) + 1);

    /// <summary>
    /// Mouse Integration keeps rewriting the guest pointer while the Windows cursor
    /// sits over Presence. A button change has to be sent with the cursor parked
    /// outside that window or the press is cleared before the guest sees it.
    /// </summary>
    public static bool ShouldPark(int previousButtons, int nextButtons) =>
        previousButtons != nextButtons;
}
