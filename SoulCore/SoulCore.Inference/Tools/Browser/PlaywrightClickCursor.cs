using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// Visible click cursor for Kurt: on-page overlay (headed Chromium) + burn-in on Presence JPEGs.
/// </summary>
public static class PlaywrightClickCursor
{
    /// <summary>Injected on every new document in Victoria's Playwright context.</summary>
    public const string InitScript =
        """
        (() => {
          if (window.__scCursorInstalled) return;
          window.__scCursorInstalled = true;
          const el = document.createElement('div');
          el.id = '__soulcore_click_cursor';
          el.setAttribute('data-soulcore', 'click-cursor');
          el.style.cssText = [
            'position:fixed',
            'z-index:2147483647',
            'width:28px',
            'height:28px',
            'margin:-14px 0 0 -14px',
            'border:3px solid #ff2d55',
            'border-radius:50%',
            'background:rgba(255,45,85,0.35)',
            'box-shadow:0 0 0 2px #fff, 0 0 14px rgba(255,45,85,0.9)',
            'pointer-events:none',
            'left:-100px',
            'top:-100px',
            'transition:left 40ms linear, top 40ms linear, transform 120ms ease',
            'transform:scale(1)'
          ].join(';');
          const ring = document.createElement('div');
          ring.style.cssText = 'position:absolute;left:50%;top:50%;width:2px;height:2px;margin:-1px 0 0 -1px;background:#fff;border-radius:50%;';
          el.appendChild(ring);
          const mount = () => {
            if (!document.documentElement.contains(el))
              document.documentElement.appendChild(el);
          };
          mount();
          new MutationObserver(mount).observe(document.documentElement, { childList: true });
          window.__scShowClick = (x, y) => {
            mount();
            el.style.left = Math.round(x) + 'px';
            el.style.top = Math.round(y) + 'px';
            el.style.transform = 'scale(1.55)';
            setTimeout(() => { el.style.transform = 'scale(1)'; }, 180);
          };
        })();
        """;

    private static readonly Rgba32 Accent = new(255, 45, 85, 255);
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    public static byte[] BurnInMarker(byte[] jpegOrPng, int x, int y)
    {
        if (jpegOrPng is null || jpegOrPng.Length == 0)
            return jpegOrPng ?? Array.Empty<byte>();

        try
        {
            using var image = Image.Load<Rgba32>(jpegOrPng);
            var cx = Math.Clamp(x, 0, Math.Max(0, image.Width - 1));
            var cy = Math.Clamp(y, 0, Math.Max(0, image.Height - 1));

            DrawRing(image, cx, cy, radius: 16, thickness: 3, White);
            DrawRing(image, cx, cy, radius: 15, thickness: 2, Accent);
            DrawRing(image, cx, cy, radius: 5, thickness: 2, Accent);
            DrawCross(image, cx, cy, arm: 20, gap: 7, White, thickness: 3);
            DrawCross(image, cx, cy, arm: 20, gap: 7, Accent, thickness: 2);

            using var ms = new MemoryStream();
            image.Save(ms, new JpegEncoder { Quality = 70 });
            return ms.ToArray();
        }
        catch
        {
            return jpegOrPng;
        }
    }

    private static void DrawRing(Image<Rgba32> image, int cx, int cy, int radius, int thickness, Rgba32 color)
    {
        var rMin = Math.Max(0, radius - thickness);
        var rMax = radius + thickness;
        for (var dy = -rMax; dy <= rMax; dy++)
        {
            for (var dx = -rMax; dx <= rMax; dx++)
            {
                var d2 = dx * dx + dy * dy;
                if (d2 < rMin * rMin || d2 > radius * radius)
                    continue;
                Plot(image, cx + dx, cy + dy, color);
            }
        }
    }

    private static void DrawCross(Image<Rgba32> image, int cx, int cy, int arm, int gap, Rgba32 color, int thickness)
    {
        for (var t = -thickness / 2; t <= thickness / 2; t++)
        {
            for (var i = gap; i <= arm; i++)
            {
                Plot(image, cx - i, cy + t, color);
                Plot(image, cx + i, cy + t, color);
                Plot(image, cx + t, cy - i, color);
                Plot(image, cx + t, cy + i, color);
            }
        }
    }

    private static void Plot(Image<Rgba32> image, int x, int y, Rgba32 color)
    {
        if ((uint)x >= (uint)image.Width || (uint)y >= (uint)image.Height)
            return;
        image[x, y] = color;
    }
}
