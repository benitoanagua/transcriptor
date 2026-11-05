using Transcriptor.UI;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class BoundedLogBufferTests
{
    [Fact]
    public void Append_KeepsTextWithNewlines()
    {
        var buffer = new BoundedLogBuffer();
        buffer.Append("primera");
        buffer.Append("segunda");
        Assert.Equal($"primera{Environment.NewLine}segunda{Environment.NewLine}", buffer.CurrentText);
        Assert.Equal(2, buffer.Count);
    }

    [Fact]
    public void Append_BeyondCapacity_DiscardsOldest()
    {
        var buffer = new BoundedLogBuffer(3);
        for (var i = 1; i <= 5; i++) buffer.Append($"L{i}");
        Assert.Equal(3, buffer.Count);
        Assert.Equal($"L3{Environment.NewLine}L4{Environment.NewLine}L5{Environment.NewLine}", buffer.CurrentText);
    }

    [Fact]
    public void DefaultCapacity_IsAboutTwoThousandLines()
    {
        var buffer = new BoundedLogBuffer();
        Assert.Equal(2000, buffer.Capacity);
        buffer.Append("única");
        Assert.Equal(1, buffer.Count);
    }

    [Fact]
    public void Changed_FiresOnAppendAndClear()
    {
        var buffer = new BoundedLogBuffer(2);
        var changed = 0;
        buffer.Changed += () => changed++;
        buffer.Append("a");
        buffer.Append("b"); // descarta "a"
        buffer.Clear();
        Assert.Equal(3, changed);
        Assert.Equal(string.Empty, buffer.CurrentText);
    }

    [Fact]
    public void Clear_IsIdempotentWithoutEvent()
    {
        var buffer = new BoundedLogBuffer();
        var changed = 0;
        buffer.Changed += () => changed++;
        buffer.Clear();
        Assert.Equal(0, changed);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Constructor_RejectsInvalidCapacity()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedLogBuffer(0));
}
