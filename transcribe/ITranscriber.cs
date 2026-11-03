namespace Transcriptor.Transcription;

/// <summary>Application port for processing pending recording sessions.</summary>
public interface ITranscriber
{
    Task<int> ProcessPendingCallsAsync(
        string recordingsRoot,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
