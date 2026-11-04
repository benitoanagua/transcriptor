namespace Transcriptor.Desktop;

/// <summary>
/// Small UI operation guard used to prevent destructive actions while transcription
/// is active. The UI thread owns transitions; the interlocked guard also protects
/// against rapid duplicate command invocations.
/// </summary>
public sealed class TranscriptionUiState
{
    private int _active;

    public bool IsTranscribing => Volatile.Read(ref _active) != 0;
    public bool CanStartRecording => !IsTranscribing;
    public bool CanDeleteSession => !IsTranscribing;
    public bool CanMigrateSession => !IsTranscribing;
    public bool CanRefreshLibrary => !IsTranscribing;
    public bool CanCancel => IsTranscribing;

    public bool TryBegin() => Interlocked.CompareExchange(ref _active, 1, 0) == 0;

    public void Complete() => Interlocked.Exchange(ref _active, 0);
}
