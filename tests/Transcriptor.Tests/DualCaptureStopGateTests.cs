using Transcriptor.Rec;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class DualCaptureStopGateTests
{
    [Fact]
    public async Task TryClaimStop_AllowsExactlyOneShutdownOwner()
    {
        using var gate = new DualCaptureStopGate();

        var claims = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => Task.Run(gate.TryClaimStop)));

        Assert.Single(claims, claim => claim);
    }

    [Fact]
    public async Task Complete_ReleasesEveryConcurrentWaiterWithSameFailure()
    {
        using var gate = new DualCaptureStopGate();
        var failure = new IOException("synthetic capture stop failure");
        var waiters = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => gate.WaitForCompletion(TimeSpan.FromSeconds(5))))
            .ToArray();

        gate.Complete(failure);

        var results = await Task.WhenAll(waiters);
        Assert.All(results, result => Assert.True(result));
        Assert.Same(failure, gate.Failure);
        Assert.True(gate.IsCompleted);
    }

    [Fact]
    public void TrackCountdown_RequiresBothCaptureCallbacksAndIgnoresDuplicateSignals()
    {
        using var gate = new DualCaptureStopGate();

        gate.SignalTrackStopped();
        Assert.False(gate.AreTracksStopped);
        Assert.False(gate.WaitForTracks(TimeSpan.FromMilliseconds(25)));

        gate.SignalTrackStopped();
        gate.SignalTrackStopped();

        Assert.True(gate.AreTracksStopped);
        Assert.True(gate.WaitForTracks(TimeSpan.FromMilliseconds(25)));
    }

    [Fact]
    public void CompleteWithoutFailure_PublishesSuccessfulShutdown()
    {
        using var gate = new DualCaptureStopGate();

        gate.Complete(null);

        Assert.True(gate.IsCompleted);
        Assert.Null(gate.Failure);
    }
}
