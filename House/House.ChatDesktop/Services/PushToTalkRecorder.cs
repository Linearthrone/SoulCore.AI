using NAudio.Wave;

namespace House.ChatDesktop.Services;

/// <summary>Push-to-talk mic capture to a temp WAV file (16 kHz mono PCM).</summary>
public sealed class PushToTalkRecorder : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _tempPath;
    private bool _recording;

    public bool IsRecording => _recording;

    public void Start()
    {
        if (_recording) return;

        _tempPath = Path.Combine(Path.GetTempPath(), $"soulcore-ptt-{Guid.NewGuid():N}.wav");
        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 1),
            BufferMilliseconds = 50
        };
        _writer = new WaveFileWriter(_tempPath, _waveIn.WaveFormat);
        _waveIn.DataAvailable += (_, e) =>
        {
            if (_writer is not null && e.BytesRecorded > 0)
                _writer.Write(e.Buffer, 0, e.BytesRecorded);
        };
        _waveIn.StartRecording();
        _recording = true;
    }

    public byte[] Stop()
    {
        if (!_recording)
            return Array.Empty<byte>();

        try { _waveIn?.StopRecording(); } catch { /* ignore */ }

        _writer?.Dispose();
        _writer = null;
        _waveIn?.Dispose();
        _waveIn = null;
        _recording = false;

        try
        {
            if (_tempPath is not null && File.Exists(_tempPath))
            {
                var bytes = File.ReadAllBytes(_tempPath);
                try { File.Delete(_tempPath); } catch { /* best-effort */ }
                _tempPath = null;
                return bytes;
            }
        }
        catch
        {
            _tempPath = null;
        }

        return Array.Empty<byte>();
    }

    public void Dispose()
    {
        if (_recording) Stop();
        _writer?.Dispose();
        _waveIn?.Dispose();
        if (_tempPath is not null)
        {
            try { File.Delete(_tempPath); } catch { /* ignore */ }
        }
    }
}
