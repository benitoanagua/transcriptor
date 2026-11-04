using Transcriptor.Desktop;
using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class ApplicationPortsTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "TranscriptorApplicationPortsTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SystemClock_ReportsUtcAndLocalClockKinds()
    {
        var clock = SystemClock.Instance;

        Assert.Equal(TimeSpan.Zero, clock.UtcNow.Offset);
        Assert.Equal(DateTimeKind.Local, clock.LocalNow.Kind);
    }

    [Fact]
    public void RecordingDirectoryFactory_UsesInjectedLocalTimeAndSanitizesReference()
    {
        var clock = new FixedClock(new DateTime(2026, 11, 3, 14, 5, 6, DateTimeKind.Local));

        var directory = RecordingDirectoryFactory.Create(_root, "  Cliente: prueba /  ", clock);

        Assert.Equal(Path.Combine(_root, "2026-11-03", "140506_Cliente-prueba"), directory);
        Assert.True(Directory.Exists(directory));
    }

    [Fact]
    public void LocalSessionStore_GetSessionsFindsEncryptedTracksAtExpectedDepth()
    {
        var expected = Path.Combine(_root, "2026-11-01", "encrypted-call");
        Directory.CreateDirectory(expected);
        File.WriteAllBytes(Path.Combine(expected, "yo.trn"), [1, 2, 3]);

        var nested = Path.Combine(expected, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllBytes(Path.Combine(nested, "cliente.trn"), [4, 5, 6]);

        ISessionStore store = new LocalSessionStore();

        Assert.Equal(new[] { expected }, store.GetSessions(_root));
    }

    [Fact]
    public void LocalSessionStore_ApplyRetentionUsesProvidedUtcTime()
    {
        var expired = CreateEncryptedSession("expired", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var recent = CreateEncryptedSession("recent", new DateTime(2026, 1, 9, 0, 0, 0, DateTimeKind.Utc));

        ISessionStore store = new LocalSessionStore();
        var deleted = store.ApplyRetention(
            _root, 7, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc));

        Assert.Contains(expired, deleted);
        Assert.DoesNotContain(recent, deleted);
        Assert.False(Directory.Exists(expired));
        Assert.True(Directory.Exists(recent));
    }

    [Fact]
    public void LocalSessionStore_DeleteSessionPreservesActiveLeasedSession()
    {
        var session = CreateEncryptedSession("active", DateTime.UtcNow);
        ISessionStore store = new LocalSessionStore();

        using var lease = TranscriptionSessionLease.TryAcquire(session);
        Assert.NotNull(lease);

        Assert.Throws<InvalidOperationException>(() => store.DeleteSession(_root, session));
        Assert.True(File.Exists(Path.Combine(session, "yo.trn")));
    }

    [Fact]
    public void LocalSessionStore_ApplyRetentionPreservesSessionExactlyAtCutoff()
    {
        var now = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        var cutoff = now.AddDays(-7);
        var session = CreateEncryptedSession("boundary", cutoff);
        ISessionStore store = new LocalSessionStore();

        var deleted = store.ApplyRetention(_root, 7, now);

        Assert.DoesNotContain(session, deleted);
        Assert.True(File.Exists(Path.Combine(session, "yo.trn")));
    }

    private sealed class FixedClock(DateTime localNow) : IClock
    {
        public DateTimeOffset UtcNow => new DateTimeOffset(localNow.ToUniversalTime());
        public DateTime LocalNow => localNow;
    }

    private string CreateEncryptedSession(string name, DateTime lastWriteUtc)
    {
        var session = Path.Combine(_root, "2026-11-01", name);
        Directory.CreateDirectory(session);
        var track = Path.Combine(session, "yo.trn");
        File.WriteAllBytes(track, [1, 2, 3]);
        File.SetLastWriteTimeUtc(track, lastWriteUtc);
        return session;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
