using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace House.ChatDesktop.Services;

/// <summary>
/// Plays a notification sound when Victoria sends a message while the window
/// is unfocused or minimised. Uses a user-configurable .wav file when set;
/// otherwise falls back to the OS default beep. Cross-platform: on Windows
/// uses <see cref="SoundPlayer"/>; on Linux/macOS falls back to a simple
/// console beep so the feature never crashes the app.
/// </summary>
public sealed class NotificationService : IDisposable
{
    private readonly NotificationSettings _settings;
    private SoundPlayer? _player;
    private bool _disposed;

    public NotificationService(NotificationSettings settings)
    {
        _settings = settings;
        ReloadPlayer();
    }

    /// <summary>
    /// Play the notification sound. Safe to call from any thread — sound
    /// playback is fire-and-forget. Returns false if notifications are muted
    /// or the sound couldn't be loaded.
    /// </summary>
    public bool Play()
    {
        if (_disposed) return false;
        if (!_settings.Enabled) return false;

        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Best-effort console beep on non-Windows platforms.
                if (_settings.UseSystemBeep)
                {
                    try { ConsoleBeepCrossPlatform(880, 150); } catch { }
                }
                return true;
            }

            if (_player is not null)
            {
                _player.Play();
                return true;
            }

            // Fallback: Windows system beep via kernel32 MessageBeep.
            MessageBeep(0xFFFFFFFF);
            return true;
        }
        catch
        {
            // Never crash the UI on a notification failure.
            return false;
        }
    }

    /// <summary>
    /// Reload the internal player after settings change (e.g. user picked a
    /// new sound file in the Settings tab).
    /// </summary>
    public void ReloadPlayer()
    {
        _player?.Dispose();
        _player = null;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        if (!string.IsNullOrWhiteSpace(_settings.SoundPath) && File.Exists(_settings.SoundPath))
        {
            try
            {
                _player = new SoundPlayer(_settings.SoundPath);
                _player.Load();
            }
            catch
            {
                _player = null;
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MessageBeep(uint uType);

    [System.Runtime.Versioning.SupportedOSPlatformGuard("windows")]
    private static void ConsoleBeepCrossPlatform(int frequency, int duration)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.Beep(frequency, duration);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _player?.Dispose();
        _player = null;
    }
}

/// <summary>
/// Notification preferences persisted alongside other UI settings.
/// </summary>
public sealed class NotificationSettings
{
    public bool Enabled { get; set; } = true;
    public bool UseSystemBeep { get; set; } = true;

    /// <summary>
    /// Absolute path to a .wav file. When null/empty/fails-to-load, the
    /// service falls back to the OS system beep.
    /// </summary>
    public string? SoundPath { get; set; }
}
