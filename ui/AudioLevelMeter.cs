namespace Transcriptor.UI;

public static class AudioLevelMath
{
    public static double LinearToDbfs(double linearAmplitude)
    {
        if (!double.IsFinite(linearAmplitude) || linearAmplitude < 0)
            throw new ArgumentOutOfRangeException(nameof(linearAmplitude));
        return linearAmplitude == 0 ? double.NegativeInfinity : 20 * Math.Log10(linearAmplitude);
    }

    public static double ToMeterPercent(double dbfs) =>
        double.IsNegativeInfinity(dbfs) ? 0 : Math.Clamp((dbfs + 60) * (100d / 60), 0, 100);

    public static string FormatDbfs(double dbfs) =>
        double.IsNegativeInfinity(dbfs) ? "−∞" : dbfs.ToString("0.0", System.Globalization.CultureInfo.CurrentCulture);
}

public sealed class AudioPeakHoldMeter
{
    private readonly IClock _clock;
    private readonly TimeSpan _holdDuration;
    private readonly double _decayDbPerSecond;
    private DateTimeOffset _holdUntil;
    private DateTimeOffset _lastUpdated;
    private DateTimeOffset _clippingUntil;

    public AudioPeakHoldMeter(IClock clock, TimeSpan? holdDuration = null, double decayDbPerSecond = 18)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _holdDuration = holdDuration ?? TimeSpan.FromSeconds(1.5);
        if (_holdDuration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(holdDuration));
        if (!double.IsFinite(decayDbPerSecond) || decayDbPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(decayDbPerSecond));
        _decayDbPerSecond = decayDbPerSecond;
        _lastUpdated = _clock.UtcNow;
    }

    public double CurrentDbfs { get; private set; } = double.NegativeInfinity;
    public double PeakHoldDbfs { get; private set; } = double.NegativeInfinity;
    public bool IsClipping { get; private set; }

    public void Update(double linearPeak)
    {
        var now = _clock.UtcNow;
        CurrentDbfs = AudioLevelMath.LinearToDbfs(linearPeak);
        if (CurrentDbfs >= -0.5)
        {
            IsClipping = true;
            _clippingUntil = now + TimeSpan.FromSeconds(2);
        }
        else if (IsClipping && now >= _clippingUntil)
        {
            IsClipping = false;
        }

        if (CurrentDbfs >= PeakHoldDbfs)
        {
            PeakHoldDbfs = CurrentDbfs;
            _holdUntil = now + _holdDuration;
        }
        else if (now > _holdUntil && double.IsFinite(PeakHoldDbfs))
        {
            var elapsedSeconds = Math.Max(0, (now - _lastUpdated).TotalSeconds);
            PeakHoldDbfs = Math.Max(CurrentDbfs, PeakHoldDbfs - elapsedSeconds * _decayDbPerSecond);
        }
        _lastUpdated = now;
    }

    public void ResetClipping()
    {
        IsClipping = false;
        _clippingUntil = DateTimeOffset.MinValue;
    }

    public void Reset()
    {
        CurrentDbfs = double.NegativeInfinity;
        PeakHoldDbfs = double.NegativeInfinity;
        IsClipping = false;
        _holdUntil = DateTimeOffset.MinValue;
        _lastUpdated = _clock.UtcNow;
        _clippingUntil = DateTimeOffset.MinValue;
    }
}

public sealed class AudioSilenceDetector(IClock clock, double thresholdDbfs = -60, TimeSpan? requiredDuration = null)
{
    private readonly IClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly double _thresholdDbfs = double.IsFinite(thresholdDbfs)
        ? thresholdDbfs
        : throw new ArgumentOutOfRangeException(nameof(thresholdDbfs));
    private readonly TimeSpan _requiredDuration = requiredDuration ?? TimeSpan.FromSeconds(10);
    private DateTimeOffset? _silentSince;

    public bool IsSilent { get; private set; }

    public bool Update(double linearPeak, bool recording, bool paused)
    {
        var dbfs = AudioLevelMath.LinearToDbfs(linearPeak);
        if (!recording || paused || dbfs >= _thresholdDbfs)
        {
            Reset();
            return false;
        }

        _silentSince ??= _clock.UtcNow;
        IsSilent = _clock.UtcNow - _silentSince.Value > _requiredDuration;
        return IsSilent;
    }

    public void Reset()
    {
        _silentSince = null;
        IsSilent = false;
    }
}

public sealed class AudioNoiseFloorEstimator(int capacity = 60)
{
    private readonly Queue<double> _recentDbfs = new();
    private readonly int _capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));

    public double EstimateDbfs
    {
        get
        {
            if (_recentDbfs.Count == 0) return double.NegativeInfinity;
            var sorted = _recentDbfs.Order().ToArray();
            return sorted[(int)((sorted.Length - 1) * 0.1)];
        }
    }

    public void Add(double linearRms)
    {
        var dbfs = AudioLevelMath.LinearToDbfs(linearRms);
        _recentDbfs.Enqueue(dbfs);
        while (_recentDbfs.Count > _capacity) _recentDbfs.Dequeue();
    }

    public void Reset() => _recentDbfs.Clear();
}

public sealed class PeakHistory(int capacity = 128)
{
    private readonly double[] _peaks = new double[capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity))];
    private int _next;
    private int _count;

    public void Append(double linearPeak)
    {
        if (!double.IsFinite(linearPeak) || linearPeak < 0)
            throw new ArgumentOutOfRangeException(nameof(linearPeak));
        _peaks[_next] = Math.Clamp(linearPeak, 0, 1);
        _next = (_next + 1) % _peaks.Length;
        _count = Math.Min(_count + 1, _peaks.Length);
    }

    public double[] Snapshot()
    {
        var values = new double[_count];
        var first = (_next - _count + _peaks.Length) % _peaks.Length;
        for (var index = 0; index < _count; index++)
            values[index] = _peaks[(first + index) % _peaks.Length];
        return values;
    }

    public void Clear()
    {
        Array.Clear(_peaks);
        _next = 0;
        _count = 0;
    }
}
