using Transcriptor.Transcription;
using Transcriptor.Transcription.Tests;
using Transcriptor.UI;
using Transcriptor.UI.ViewModels;
using Xunit;

namespace Transcriptor.Tests;

public sealed class PlaybackTemporaryCleanupTests
{
    [Fact]
    public void SweepDeletesOnlyExactPlaybackWavsInUnlockedRecognizedSessions()
    {
        var root = Path.Combine(Path.GetTempPath(), "TranscriptorPlaybackCleanupTests", Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "2026-01-01", "call");
        Directory.CreateDirectory(session);
        File.WriteAllBytes(Path.Combine(session, "yo.trn"), [1]);
        var expectedTemporary = Path.Combine(session, $".{Guid.NewGuid():N}.playback.wav");
        var expectedRawTemporary = Path.Combine(session, $".{Guid.NewGuid():N}.audio.tmp");
        var unrelatedPlayback = Path.Combine(session, ".not-a-guid.playback.wav");
        var unrelatedAudioTemporary = Path.Combine(session, ".not-a-guid.audio.tmp");
        var nonHiddenPlayback = Path.Combine(session, $"{Guid.NewGuid():N}.playback.wav");
        File.WriteAllBytes(expectedTemporary, [1]);
        File.WriteAllBytes(expectedRawTemporary, [1]);
        File.WriteAllBytes(unrelatedPlayback, [1]);
        File.WriteAllBytes(unrelatedAudioTemporary, [1]);
        File.WriteAllBytes(nonHiddenPlayback, [1]);

        try
        {
            var removed = PlaybackTemporaryCleanup.Sweep(CreateStore(), root, new TestLogSink());

            Assert.Equal(2, removed);
            Assert.False(File.Exists(expectedTemporary));
            Assert.False(File.Exists(expectedRawTemporary));
            Assert.True(File.Exists(unrelatedPlayback));
            Assert.True(File.Exists(unrelatedAudioTemporary));
            Assert.True(File.Exists(nonHiddenPlayback));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SweepSkipsSessionsWithAnActiveLease()
    {
        var root = Path.Combine(Path.GetTempPath(), "TranscriptorPlaybackCleanupTests", Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "2026-01-01", "call");
        Directory.CreateDirectory(session);
        File.WriteAllBytes(Path.Combine(session, "yo.trn"), [1]);
        var temporary = Path.Combine(session, $".{Guid.NewGuid():N}.playback.wav");
        File.WriteAllBytes(temporary, [1]);

        try
        {
            using var lease = TranscriptionSessionLease.TryAcquire(session);
            Assert.NotNull(lease);

            var removed = PlaybackTemporaryCleanup.Sweep(CreateStore(), root, new TestLogSink());

            Assert.Equal(0, removed);
            Assert.True(File.Exists(temporary));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ISessionStore CreateStore() =>
        new LocalSessionStore(new PassThroughSessionSecurity(), new ReversibleSessionMigration());

    private sealed class TestLogSink : ILogSink
    {
        public void Log(string message) { }
    }
}
