using System.Diagnostics;
using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class TranscriptionEngineRecoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "TranscriptorEngineRecoveryTests", Guid.NewGuid().ToString("N"));
    private readonly string _sessionDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
    private string Session => Path.Combine(_root, _sessionDate, "call-a");

    public TranscriptionEngineRecoveryTests() => Directory.CreateDirectory(Session);

    [Fact]
    public async Task FailedSecondTrack_CanBeRetriedWithoutPublishingIncompleteEncryptedTranscript()
    {
        File.WriteAllBytes(Path.Combine(Session, "yo.wav"), [1, 2, 3, 4]);
        File.WriteAllBytes(Path.Combine(Session, "cliente.wav"), [5, 6, 7, 8]);
        var executable = Path.Combine(_root, "whisper-cli.exe");
        var model = Path.Combine(_root, "model.bin");
        File.WriteAllText(executable, "simulated");
        File.WriteAllText(model, "simulated");

        var failingRunner = new RecoveryWhisperRunner(failTrack: "cliente");
        var first = new TranscriptionEngine(executable, model, "es", failingRunner, new CopyingNormalizer(), new PassThroughSessionSecurity());

        await Assert.ThrowsAsync<AggregateException>(() =>
            first.ProcessPendingCallsAsync(_root));

        Assert.True(File.Exists(Path.Combine(Session, "yo.srt")));
        Assert.False(File.Exists(Path.Combine(Session, "transcripcion.srt.trn")));
        Assert.False(TranscriptionEngine.IsTranscriptComplete(Session));
        Assert.Empty(Directory.EnumerateFiles(Session, "*.normalized.wav"));

        var successfulRunner = new RecoveryWhisperRunner();
        var retry = new TranscriptionEngine(executable, model, "es", successfulRunner, new CopyingNormalizer(), new PassThroughSessionSecurity());
        var processed = await retry.ProcessPendingCallsAsync(_root);

        Assert.Equal(1, processed);
        Assert.True(TranscriptionEngine.IsTranscriptComplete(Session));
        Assert.False(File.Exists(Path.Combine(Session, "yo.srt")));
        Assert.False(File.Exists(Path.Combine(Session, "cliente.srt")));
        Assert.Empty(Directory.EnumerateFiles(Session, "*.normalized.wav"));
    }

    [Fact]
    public async Task ConcurrentRun_SkipsSessionWhileFirstRunOwnsLease()
    {
        File.WriteAllBytes(Path.Combine(Session, "yo.wav"), [1, 2, 3, 4]);
        File.WriteAllBytes(Path.Combine(Session, "cliente.wav"), [5, 6, 7, 8]);
        var executable = Path.Combine(_root, "whisper-cli.exe");
        var model = Path.Combine(_root, "model.bin");
        File.WriteAllText(executable, "simulated");
        File.WriteAllText(model, "simulated");

        using var lease = TranscriptionSessionLease.TryAcquire(Session);
        Assert.NotNull(lease);

        var runner = new RecoveryWhisperRunner();
        var engine = new TranscriptionEngine(executable, model, "es", runner, new CopyingNormalizer(), new PassThroughSessionSecurity());
        var processed = await engine.ProcessPendingCallsAsync(_root);

        Assert.Equal(0, processed);
        Assert.Equal(0, runner.Invocations);
        Assert.True(File.Exists(Path.Combine(Session, "yo.wav")));
    }

    private sealed class CopyingNormalizer : IAudioNormalizer
    {
        public void Normalize(string sourcePath, string destinationPath) =>
            File.Copy(sourcePath, destinationPath);
    }

    private sealed class RecoveryWhisperRunner(string? failTrack = null) : IWhisperProcessRunner
    {
        public int Invocations { get; private set; }

        public async Task<WhisperProcessResult> RunAsync(
            ProcessStartInfo startInfo,
            CancellationToken cancellationToken = default)
        {
            Invocations++;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            var index = startInfo.ArgumentList.IndexOf("-of");
            Assert.True(index >= 0 && index + 1 < startInfo.ArgumentList.Count);
            var outputPrefix = startInfo.ArgumentList[index + 1];
            var track = Path.GetFileName(outputPrefix);
            if (track == failTrack)
                throw new IOException("synthetic failure on second audio track");

            await File.WriteAllTextAsync(outputPrefix + ".srt",
                $"1\n00:00:00,000 --> 00:00:01,000\ntexto simulado de {track}\n", cancellationToken);
            return new WhisperProcessResult(0, string.Empty, string.Empty);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
