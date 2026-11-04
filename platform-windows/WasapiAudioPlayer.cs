using NAudio.Wave;
using Transcriptor.Transcription;

namespace Transcriptor.Platform.Windows;

/// <summary>
/// NAudio/WASAPI implementation of the playback port, used against a decrypted
/// temporal WAV. Playback always starts on creation (same contract as before);
/// pause/resume/seek/position/duration map directly onto WaveOutEvent and
/// AudioFileReader. Variable speed is reported as unsupported until the playback
/// redesign ships its pitch-preserving provider.
/// </summary>
public sealed class WasapiAudioPlayer : IAudioPlayer
{
    private readonly WaveOutEvent _output;
    private readonly AudioFileReader _reader;
    private double _playbackRate = 1;
    private bool _disposed;

    public event Action? Ended;
    public event Action<Exception>? Failed;

    public TimeSpan Duration => _reader.TotalTime;
    public TimeSpan Position => _reader.CurrentTime;
    public double PlaybackRate => _playbackRate;
    public WasapiAudioPlayer(string waveFilePath)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(waveFilePath);
        _reader = new AudioFileReader(waveFilePath);
        _output = new WaveOutEvent();
        _output.PlaybackStopped += OnPlaybackStopped;
        _output.Init(_reader);
        _output.Play();
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        try
        {
            if (e.Exception is not null) Failed?.Invoke(e.Exception);
            else Ended?.Invoke();
        }
        catch { /* Los observadores no deben impedir la liberación. */ }
    }

    public void Pause()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _output.Pause();
    }

    public void Resume()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _output.Play();
    }

    public void Seek(TimeSpan position)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var clamped = TimeSpan.FromSeconds(Math.Clamp(position.TotalSeconds, 0, Math.Max(0, _reader.TotalTime.TotalSeconds)));
        _reader.CurrentTime = clamped;
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _output.Stop();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _output.PlaybackStopped -= OnPlaybackStopped;
        try { _output.Dispose(); }
        finally { _reader.Dispose(); }
    }
}

/// <summary>Factory adapter satisfying IAudioPlayerFactory on Windows.</summary>
public sealed class WasapiAudioPlayerFactory : IAudioPlayerFactory
{
    public IAudioPlayer Create(string waveFilePath) => new WasapiAudioPlayer(waveFilePath);
}
