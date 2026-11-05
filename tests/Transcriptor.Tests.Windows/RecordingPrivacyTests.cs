using Transcriptor.Platform.Windows;
using Transcriptor.Transcription;
using Transcriptor.UI;
using Xunit;

namespace Transcriptor.Tests.Windows;

public sealed class RecordingPrivacyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "TranscriptorPrivacyTests", Guid.NewGuid().ToString("N"));
    private static readonly WindowsSessionDirectorySecurity Security = new();

    [Fact]
    public void DeleteSession_RemovesOnlySessionInsideConfiguredRoot()
    {
        var session = CreateSession("2026-01-01", "call-a");
        RecordingPrivacy.DeleteSession(_root, session, Security);
        Assert.False(Directory.Exists(session));
    }

    [Fact]
    public void DeleteSession_RejectsSessionOwnedByActiveTranscription()
    {
        var session = CreateSession("2026-01-01", "active-call");
        using var lease = TranscriptionSessionLease.TryAcquire(session);
        Assert.NotNull(lease);

        Assert.Throws<InvalidOperationException>(() => RecordingPrivacy.DeleteSession(_root, session, Security));
        Assert.True(File.Exists(Path.Combine(session, "yo.wav")));
    }

    [Fact]
    public void DeleteSession_RejectsPathOutsideConfiguredRoot()
    {
        var outside = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "yo.wav"), "audio");
        try
        {
            Assert.Throws<InvalidOperationException>(() => RecordingPrivacy.DeleteSession(_root, outside, Security));
            var legitimate = CreateSession("2026-01-01", "legitimate");
            var victim = Path.Combine(_root, "victim");
            Directory.CreateDirectory(victim);
            var sentinel = Path.Combine(victim, "keep.txt");
            File.WriteAllText(sentinel, "must survive");
            Assert.Throws<InvalidOperationException>(() =>
                RecordingPrivacy.DeleteSession(_root, Path.Combine(_root, "2026-01-01", "legitimate", "..", "..", "victim"), Security));
            Assert.True(File.Exists(sentinel));
            Assert.True(Directory.Exists(legitimate));
            Assert.True(File.Exists(Path.Combine(outside, "yo.wav")));
        }
        finally { Directory.Delete(outside, recursive: true); }
    }

    [Fact]
    public void DeleteSession_RejectsTheParentOfConfiguredRoot()
    {
        var outside = Path.GetDirectoryName(_root)!;
        Assert.Throws<InvalidOperationException>(() => RecordingPrivacy.DeleteSession(_root, outside, Security));
    }

    [Fact]
    public void DeleteSession_RejectsReparsePointInDateAncestor()
    {
        var outside = Path.Combine(Path.GetTempPath(), "TranscriptorOutside", Guid.NewGuid().ToString("N"));
        var outsideDate = Path.Combine(outside, "2026-01-01");
        var outsideSession = Path.Combine(outsideDate, "call-a");
        var linkedDate = Path.Combine(_root, "2026-01-01");
        Directory.CreateDirectory(outsideSession);
        File.WriteAllText(Path.Combine(outsideSession, "yo.wav"), "audio");
        Directory.CreateDirectory(_root);

        try
        {
            try { Directory.CreateSymbolicLink(linkedDate, outsideDate); }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                return; // El agente de CI puede no tener habilitada la creación de enlaces simbólicos.
            }

            Assert.Throws<InvalidOperationException>(
                () => RecordingPrivacy.DeleteSession(_root, Path.Combine(linkedDate, "call-a"), Security));
            Assert.True(File.Exists(Path.Combine(outsideSession, "yo.wav")));
        }
        finally
        {
            if (Directory.Exists(linkedDate)) Directory.Delete(linkedDate);
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
            if (Directory.Exists(outside)) Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void DeleteSession_RejectsReparsePointInsideSession()
    {
        var session = CreateSession("2026-01-01", "linked");
        var outside = Path.Combine(Path.GetTempPath(), "TranscriptorOutside", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        var sentinel = Path.Combine(outside, "keep.txt");
        File.WriteAllText(sentinel, "keep");
        var link = Path.Combine(session, "external");
        try
        {
            try { Directory.CreateSymbolicLink(link, outside); }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                return; // El agente de CI puede no tener habilitada la creación de enlaces simbólicos.
            }

            Assert.Throws<InvalidOperationException>(() => RecordingPrivacy.DeleteSession(_root, session, Security));
            Assert.True(File.Exists(sentinel));
        }
        finally
        {
            if (Directory.Exists(session)) Directory.Delete(session, recursive: true);
            if (Directory.Exists(outside)) Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void GetSessions_EnumeratesOnlyExpectedDateAndSessionLevels()
    {
        var expected = CreateSession("2026-01-01", "call-a");
        var nested = Path.Combine(expected, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "yo.wav"), "not-a-session");
        Assert.Equal(new[] { expected }, RecordingPrivacy.GetSessions(_root, Security));
    }

    [Fact]
    public void ApplyRetention_DeletesOnlyExpiredSessionsWithAudio()
    {
        var expired = CreateSession("2026-01-01", "expired");
        var recent = CreateSession("2026-01-02", "recent");
        File.SetLastWriteTimeUtc(Path.Combine(expired, "yo.wav"), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(Path.Combine(recent, "yo.wav"), new DateTime(2026, 1, 9, 0, 0, 0, DateTimeKind.Utc));
        var deleted = RecordingPrivacy.ApplyRetention(_root, 7, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), Security);
        Assert.Contains(expired, deleted);
        Assert.False(Directory.Exists(expired));
        Assert.True(Directory.Exists(recent));
    }

    [Fact]
    public void ApplyRetention_RejectsDisabledOrInvalidPolicy()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RecordingPrivacy.ApplyRetention(_root, 0, DateTime.UtcNow, Security));
    }

    [Fact]
    public void GetSessions_RecognizesEncryptedAudioTracks()
    {
        var session = Path.Combine(_root, "2026-01-03", "encrypted-call");
        Directory.CreateDirectory(session);
        File.WriteAllBytes(Path.Combine(session, "yo.trn"), [1, 2, 3]);
        Assert.Equal(new[] { session }, RecordingPrivacy.GetSessions(_root, Security));
    }

    [Fact]
    public void ApplyRetention_UsesEncryptedTrackTimestamp()
    {
        var session = Path.Combine(_root, "2026-01-04", "expired-encrypted");
        Directory.CreateDirectory(session);
        var track = Path.Combine(session, "yo.trn");
        File.WriteAllBytes(track, [1, 2, 3]);
        File.SetLastWriteTimeUtc(track, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var deleted = RecordingPrivacy.ApplyRetention(_root, 7,
            new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), Security);

        Assert.Contains(session, deleted);
        Assert.False(Directory.Exists(session));
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
