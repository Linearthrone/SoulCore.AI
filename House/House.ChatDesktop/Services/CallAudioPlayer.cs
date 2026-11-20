using NAudio.Wave;

namespace House.ChatDesktop.Services;

/// <summary>PROP-21: play Soniox PCM s16le chunks from voice.audio frames.</summary>
public sealed class CallAudioPlayer : IDisposable
{
    private readonly object _gate = new();
    private BufferedWaveProvider? _buffer;
    private WaveOutEvent? _out;
    private int _sampleRate = 24000;

    public void EnsureFormat(int sampleRate)
    {
        if (sampleRate <= 0) sampleRate = 24000;
        lock (_gate)
        {
            if (_out is not null && _sampleRate == sampleRate)
                return;
            DisposeUnlocked();
            _sampleRate = sampleRate;
            var format = new WaveFormat(sampleRate, 16, 1);
            _buffer = new BufferedWaveProvider(format)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromSeconds(8)
            };
            _out = new WaveOutEvent();
            _out.Init(_buffer);
            _out.Play();
        }
    }

    public void AddPcm(byte[] pcm)
    {
        if (pcm.Length == 0) return;
        lock (_gate)
        {
            EnsureFormat(_sampleRate);
            _buffer?.AddSamples(pcm, 0, pcm.Length);
        }
    }

    public void Stop()
    {
        lock (_gate)
            DisposeUnlocked();
    }

    public void Dispose() => Stop();

    private void DisposeUnlocked()
    {
        try { _out?.Stop(); } catch { /* ignore */ }
        _out?.Dispose();
        _out = null;
        _buffer = null;
    }
}
