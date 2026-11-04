namespace Transcriptor.Platform.Windows;

/// <summary>
/// Hardware-independent coordination for the two capture callbacks and concurrent
/// Stop/Dispose callers. The first caller owns shutdown; all others observe its result.
/// </summary>
internal sealed class DualCaptureStopGate : IDisposable
{
    private readonly CountdownEvent _tracksStopped = new(2);
    private readonly ManualResetEventSlim _stopCompleted = new(false);
    private int _stopClaimed;
    private Exception? _failure;

    public bool IsCompleted => _stopCompleted.IsSet;
    public Exception? Failure => Volatile.Read(ref _failure);

    public bool TryClaimStop() => Interlocked.CompareExchange(ref _stopClaimed, 1, 0) == 0;

    public void SignalTrackStopped()
    {
        try { _tracksStopped.Signal(); }
        catch (InvalidOperationException) { /* Defensive: a callback may arrive after a timeout path. */ }
    }

    public bool WaitForTracks(TimeSpan timeout) => _tracksStopped.Wait(timeout);

    public bool WaitForCompletion(TimeSpan timeout) => _stopCompleted.Wait(timeout);

    public void Complete(Exception? failure)
    {
        if (failure is not null)
            Interlocked.CompareExchange(ref _failure, failure, null);
        _stopCompleted.Set();
    }

    public void Dispose()
    {
        // Keep the completion signal alive: concurrent Stop callers may still be
        // waiting on it after the owner has returned. Never dispose an unsignaled
        // track countdown because a late native callback may still arrive.
        if (_tracksStopped.IsSet)
            _tracksStopped.Dispose();
    }
}
