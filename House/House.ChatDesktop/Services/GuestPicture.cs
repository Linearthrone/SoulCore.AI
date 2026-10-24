namespace House.ChatDesktop.Services;

/// <summary>
/// Words and the still slot for the VM picture.
/// A later frame replaces the previous one. The caption is a live-guest line while a frame is held.
/// With no frame, the empty line says the guest window is not ready.
/// </summary>
public static class GuestPicture
{
    public const string LiveCaption = "Live guest";

    public const string NotReady = "The guest window is not ready.";

    public static bool IsLiveCaption(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && text.Contains("live", StringComparison.OrdinalIgnoreCase)
        && !text.Contains("not a live guest", StringComparison.OrdinalIgnoreCase);
}

public enum GuestPictureSource
{
    None = 0,
    Vm = 1,
    Desktop = 2,
    Grab = 3
}

/// <summary>
/// The picture well's frame sequence. <see cref="Show"/> copies the new bytes over the previous still.
/// </summary>
public sealed class ViewportStillSlot
{
    private byte[]? _current;
    private byte[]? _previous;

    public int Generation { get; private set; }

    public bool HasFrame { get; private set; }

    public string Caption { get; private set; } = GuestPicture.NotReady;

    public byte[]? Current => _current;

    public byte[]? Previous => _previous;

    /// <summary>Returns true when this frame replaced a still that was already showing.</summary>
    public bool Show(byte[] frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Length == 0)
            return false;

        var next = frame.ToArray();
        var replaced = _current is { Length: > 0 };
        _previous = _current;
        _current = next;
        HasFrame = true;
        Generation++;
        Caption = GuestPicture.LiveCaption;
        return replaced;
    }

    public void Clear()
    {
        _previous = _current;
        _current = null;
        HasFrame = false;
        Caption = GuestPicture.NotReady;
    }
}
