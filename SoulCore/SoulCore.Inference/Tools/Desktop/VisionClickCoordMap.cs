namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Last vision JPEG grid vs guest framebuffer. Models often click in the
/// attached (possibly downscaled) image without multiplying — Host remaps.
/// </summary>
public interface IVisionClickCoordMap
{
    void NoteFrame(int sourceWidth, int sourceHeight, int visionWidth, int visionHeight);

    /// <summary>Map a tool click to guest framebuffer pixels.</summary>
    ToolImagePayload.ClickCoordResolution MapToGuest(int x, int y);
}

/// <summary>Process-wide last desktop vision frame for click remap.</summary>
public sealed class VisionClickCoordMap : IVisionClickCoordMap
{
    private readonly object _gate = new();
    private int _sourceW;
    private int _sourceH;
    private int _visionW;
    private int _visionH;

    public void NoteFrame(int sourceWidth, int sourceHeight, int visionWidth, int visionHeight)
    {
        lock (_gate)
        {
            _sourceW = sourceWidth;
            _sourceH = sourceHeight;
            _visionW = visionWidth;
            _visionH = visionHeight;
        }
    }

    public ToolImagePayload.ClickCoordResolution MapToGuest(int x, int y)
    {
        int sw, sh, vw, vh;
        lock (_gate)
        {
            sw = _sourceW;
            sh = _sourceH;
            vw = _visionW;
            vh = _visionH;
        }

        return ToolImagePayload.ResolveClickToGuest(x, y, sw, sh, vw, vh);
    }
}
