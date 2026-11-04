namespace Transcriptor.Transcription;

/// <summary>
/// Port for playing an already-decrypted local WAV file. Keeps the contract that
/// the UI previously built with NAudio: pause, stop, seek, position/duration and
/// terminal events, while TranscriptionSessionLease and temporary-file cleanup
/// remain owned by the caller.
/// </summary>
public interface IAudioPlayer : IDisposable
{
    /// <summary>Raised exactly once when playback reaches the end of the stream.</summary>
    event Action? Ended;
    /// <summary>Raised exactly once when playback fails before or during playback.</summary>
    event Action<Exception>? Failed;

    TimeSpan Duration { get; }
    TimeSpan Position { get; }
    Double PlaybackRate { get; }

    void Pause();
    void Resume();
    void Seek(TimeSpan position);
    /// <summary>Stops playback and releases the output device; triggering Ended is implementation-defined.</summary>
    void Stop();
}

/// <summary>Platform factory creating a playing session for one WAV file; playback starts immediately.</summary>
public interface IAudioPlayerFactory
{
    IAudioPlayer Create(string waveFilePath);
}
