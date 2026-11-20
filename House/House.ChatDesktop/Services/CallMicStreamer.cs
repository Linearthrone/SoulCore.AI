using NAudio.Wave;

namespace House.ChatDesktop.Services;

/// <summary>PROP-21: continuous mic PCM chunks for Call → Host → Soniox realtime STT.</summary>
public sealed class CallMicStreamer : IDisposable
{
    private WaveInEvent? _waveIn;
    private bool _running;

    public event Action<byte[]>? ChunkReady;

    public bool IsRunning => _running;

    public void Start()
    {
        if (_running) return;
        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 1),
            BufferMilliseconds = 80
        };
        _waveIn.DataAvailable += (_, e) =>
        {
            if (e.BytesRecorded <= 0) return;
            var copy = new byte[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, copy, 0, e.BytesRecorded);
            ChunkReady?.Invoke(copy);
        };
        _waveIn.StartRecording();
        _running = true;
    }

    public void Stop()
    {
        if (!_running) return;
        try { _waveIn?.StopRecording(); } catch { /* ignore */ }
        _waveIn?.Dispose();
        _waveIn = null;
        _running = false;
    }

    public void Dispose() => Stop();
}
