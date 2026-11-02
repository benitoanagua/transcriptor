using Transcriptor.Desktop;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class RecordingPrivacyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "TranscriptorPrivacyTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void DeleteSession_RemovesOnlySessionInsideConfiguredRoot()
    {
        var session = CreateSession("2026-01-01", "call-a");
        RecordingPrivacy.DeleteSession(_root, session);
        Assert.False(Directory.Exists(session));
    }

    [Fact]
    public void DeleteSession_RejectsPathOutsideConfiguredRoot()
    {
        var outside = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "yo.wav"), "audio");
        try { Assert.Throws<InvalidOperationException>(() => RecordingPrivacy.DeleteSession(_root, outside)); }
        finally { Directory.Delete(outside, recursive: true); }
    }

    [Fact]
    public void ApplyRetention_DeletesOnlyExpiredSessionsWithAudio()
    {
        var expired = CreateSession("2026-01-01", "expired");
        var recent = CreateSession("2026-01-02", "recent");
        File.SetLastWriteTimeUtc(Path.Combine(expired, "yo.wav"), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(Path.Combine(recent, "yo.wav"), new DateTime(2026, 1, 9, 0, 0, 0, DateTimeKind.Utc));
        var deleted = RecordingPrivacy.ApplyRetention(_root, 7, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc));
        Assert.Contains(expired, deleted);
        Assert.False(Directory.Exists(expired));
        Assert.True(Directory.Exists(recent));
    }

    [Fact]
    public void ApplyRetention_RejectsDisabledOrInvalidPolicy()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RecordingPrivacy.ApplyRetention(_root, 0, DateTime.UtcNow));
    }

    private string CreateSession(string date, string name)
    {
        var path = Path.Combine(_root, date, name);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "yo.wav"), "audio");
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
