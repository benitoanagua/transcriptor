namespace Transcriptor.Transcription;

/// <summary>
/// Port represented by an active dual-track capture session. Implementations write
/// both tracks directly into <see cref="EncryptedAudioTrackWriter"/> containers
/// created by the session itself, keep the coordinated two-stop contract that
/// <c>DualCaptureStopGate</c> owns, and report read-only level measurements that
/// must never alter the bytes written to disk.
/// </summary>
public interface IAudioCaptureSession : IDisposable
{
    /// <summary>Human readable names of the devices actually captured, when known.</summary>
    string? MicrophoneDeviceName { get; }
    string? SystemDeviceName { get; }

    /// <summary>Capture wall time, frozen while paused.</summary>
    TimeSpan Elapsed { get; }
    bool IsPaused { get; }

    /// <summary>Reported when capture or encryption fails; both tracks end up closed.</summary>
    event Action<Exception>? Faulted;

    void Start();
    /// <summary>Stops both tracks coordinately; throws when the encrypted tracks cannot be completed.</summary>
    void Stop();
    void Pause();
    void Resume();

    /// <summary>Peak level for the microphone track, 0..1 linear scale (legacy contract preserved).</summary>
    float MicrophonePeak { get; }
    /// <summary>RMS level for the microphone track, 0..1 linear scale.</summary>
    float MicrophoneRms { get; }
    /// <summary>Peak level for the system track, 0..1 linear scale.</summary>
    float SystemPeak { get; }
    /// <summary>RMS level for the system track, 0..1 linear scale.</summary>
    float SystemRms { get; }
}

/// <summary>Platform factory for capture sessions over concrete devices (<c>null</c> = platform default).</summary>
public interface IAudioCaptureFactory
{
    IAudioCaptureSession Create(string outputDirectory, string? microphoneDeviceId, string? systemDeviceId);
}
