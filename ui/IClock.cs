namespace Transcriptor.UI;

/// <summary>Provides wall-clock values used for retention and user-facing timestamps.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateTime LocalNow { get; }
}

/// <summary>Production clock backed by the operating system clock.</summary>
public sealed class SystemClock : IClock
{
    public static SystemClock Instance { get; } = new();

    private SystemClock() { }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public DateTime LocalNow => DateTime.Now;
}
