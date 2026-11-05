using Transcriptor.UI;
using Xunit;

namespace Transcriptor.Tests;

public sealed class AudioLevelTests
{
    [Fact]
    public void LinearAmplitude_ConvertsToDbfsIncludingSilenceAndFullScale()
    {
        Assert.True(double.IsNegativeInfinity(AudioLevelMath.LinearToDbfs(0)));
        Assert.Equal(0, AudioLevelMath.LinearToDbfs(1), precision: 10);
        Assert.Equal(0, AudioLevelMath.ToMeterPercent(double.NegativeInfinity));
        Assert.Equal(100, AudioLevelMath.ToMeterPercent(0));
    }

    [Fact]
    public void PeakHold_HoldsThenDecaysAndClippingCanBeReset()
    {
        var clock = new TestClock();
        var meter = new AudioPeakHoldMeter(clock, TimeSpan.FromSeconds(1), decayDbPerSecond: 10);

        meter.Update(1);
        Assert.Equal(0, meter.PeakHoldDbfs);
        Assert.True(meter.IsClipping);

        clock.Advance(TimeSpan.FromMilliseconds(900));
        meter.Update(0.1);
        Assert.Equal(0, meter.PeakHoldDbfs);

        clock.Advance(TimeSpan.FromMilliseconds(300));
        meter.Update(0.1);
        Assert.InRange(meter.PeakHoldDbfs, -4, -2);
        Assert.True(meter.IsClipping);

        meter.ResetClipping();
        Assert.False(meter.IsClipping);
    }

    [Fact]
    public void SilenceDetector_WarnsOnlyAfterTenContinuousUnpausedSeconds()
    {
        var clock = new TestClock();
        var detector = new AudioSilenceDetector(clock);

        Assert.False(detector.Update(0, recording: true, paused: false));
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.False(detector.Update(0, recording: true, paused: false));
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(detector.Update(0, recording: true, paused: false));

        Assert.False(detector.Update(0, recording: true, paused: true));
        clock.Advance(TimeSpan.FromSeconds(11));
        Assert.False(detector.Update(0, recording: true, paused: false));
    }

    [Fact]
    public void PeakHistory_KeepsOnlyTheNewestValuesInChronologicalOrder()
    {
        var history = new PeakHistory(3);

        history.Append(0.1);
        history.Append(0.2);
        history.Append(0.3);
        history.Append(0.4);

        Assert.Equal([0.2, 0.3, 0.4], history.Snapshot());
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.UnixEpoch;
        public DateTime LocalNow => UtcNow.LocalDateTime;
        public void Advance(TimeSpan amount) => UtcNow += amount;
    }
}
