namespace Transcriptor.UI;

/// <summary>
/// Small UI operation guard used to prevent destructive actions while transcription
/// is active. The UI thread owns transitions; the interlocked guard also protects
/// against rapid duplicate command invocations. Raises <see cref="Changed"/> on
/// every transition so the child ViewModels can refresh their enabled states.
/// </summary>
public sealed class TranscriptionUiState
{
    private int _active;

    /// <summary>Raised when <see cref="TryBegin"/> succeeds or <see cref="Complete"/> clears the guard.</summary>
    public event Action? Changed;

    public bool IsTranscribing => Volatile.Read(ref _active) != 0;

    public bool TryBegin()
    {
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0) return false;
        Changed?.Invoke();
        return true;
    }

    public void Complete()
    {
        if (Interlocked.Exchange(ref _active, 0) != 0) Changed?.Invoke();
    }
}
