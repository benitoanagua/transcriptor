namespace Transcriptor.Transcription;

/// <summary>
/// Read-only device monitor used for level checks. Implementations must not
/// create session directories or write audio to disk.
/// </summary>
public interface IAudioLevelMonitor : IDisposable
{
    event Action<Exception>? Faulted;
    float MicrophonePeak { get; }
    float MicrophoneRms { get; }
    float SystemPeak { get; }
    float SystemRms { get; }
    void Start();
    void Stop();
}

public interface IAudioLevelMonitorFactory
{
    IAudioLevelMonitor Create(
        string? microphoneDeviceId, string? systemDeviceId, bool includeSystemLoopback);
}
