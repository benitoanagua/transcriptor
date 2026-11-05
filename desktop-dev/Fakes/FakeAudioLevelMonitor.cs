using Transcriptor.Transcription;

namespace Transcriptor.Dev;

// ⚠️ SEÑAL FICTICIA — SOLO DESARROLLO ⚠️
// Genera lecturas sintéticas sin abrir escritores, crear sesiones ni guardar audio.
public sealed class FakeAudioLevelMonitorFactory : IAudioLevelMonitorFactory
{
    public IAudioLevelMonitor Create(
        string? microphoneDeviceId, string? systemDeviceId, bool includeSystemLoopback) =>
        new FakeAudioLevelMonitor(includeSystemLoopback);
}

internal sealed class FakeAudioLevelMonitor : IAudioLevelMonitor
{
    private const int BlockMilliseconds = 100;
    private readonly System.Diagnostics.Stopwatch _clock = new();
    private readonly bool _includeSystem;
    private readonly System.Threading.Timer _timer;
    private volatile float _microphonePeak;
    private volatile float _microphoneRms;
    private volatile float _systemPeak;
    private volatile float _systemRms;
    private volatile bool _started;
    private volatile bool _disposed;

    public event Action<Exception>? Faulted { add { } remove { } }
    public float MicrophonePeak => _microphonePeak;
    public float MicrophoneRms => _microphoneRms;
    public float SystemPeak => _includeSystem ? _systemPeak : 0;
    public float SystemRms => _includeSystem ? _systemRms : 0;

    public FakeAudioLevelMonitor(bool includeSystem)
    {
        _includeSystem = includeSystem;
        _timer = new System.Threading.Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) throw new InvalidOperationException("La prueba de niveles ya está iniciada.");
        _started = true;
        _clock.Restart();
        _timer.Change(0, BlockMilliseconds);
    }

    public void Stop()
    {
        if (!_started) return;
        _started = false;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        _microphonePeak = 0;
        _microphoneRms = 0;
        _systemPeak = 0;
        _systemRms = 0;
        _clock.Stop();
    }

    private void OnTick(object? state)
    {
        if (!_started || _disposed) return;
        var microphone = FakeSignals.MicrophoneBlock(_clock.Elapsed, BlockMilliseconds);
        var system = _includeSystem
            ? FakeSignals.SystemBlock(_clock.Elapsed, BlockMilliseconds)
            : default;
        _microphonePeak = microphone.Peak;
        _microphoneRms = microphone.Rms;
        _systemPeak = system.Peak;
        _systemRms = system.Rms;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        _timer.Dispose();
    }
}
