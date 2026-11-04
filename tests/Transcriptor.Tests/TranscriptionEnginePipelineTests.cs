using System.Diagnostics;
using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class TranscriptionEnginePipelineTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "TranscriptorEnginePipelineTests", Guid.NewGuid().ToString("N"));

    public TranscriptionEnginePipelineTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task TranscribeTrackAsync_UsesInjectedNormalizerAndPublishesSrt()
    {
        var source = CreateSource("yo.wav");
        var runner = new FakeWhisperRunner();
        var engine = CreateEngine(runner, new CopyingNormalizer());

        await engine.TranscribeTrackAsync(source, "yo", _directory, CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(_directory, "yo.srt")));
        Assert.Contains("respuesta simulada", File.ReadAllText(Path.Combine(_directory, "yo.srt")));
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.normalized.wav"));
        Assert.Equal(1, runner.Invocations);
        Assert.Equal("es", runner.LastStartInfo!.ArgumentList[3]);
    }

    [Fact]
    public async Task TranscribeTrackAsync_RemovesNormalizedAudioWhenWhisperFails()
    {
        var source = CreateSource("cliente.wav");
        var engine = CreateEngine(new FakeWhisperRunner { Failure = new InvalidOperationException("simulated runner failure") },
            new CopyingNormalizer());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.TranscribeTrackAsync(source, "cliente", _directory, CancellationToken.None));

        Assert.Empty(Directory.EnumerateFiles(_directory, "*.normalized.wav"));
        Assert.False(File.Exists(Path.Combine(_directory, "cliente.srt")));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public async Task TranscribeTrackAsync_RejectsSuccessfulWhisperExitWithoutSrt()
    {
        var source = CreateSource("yo.wav");
        var engine = CreateEngine(new FakeWhisperRunner { SuppressOutput = true }, new CopyingNormalizer());

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            engine.TranscribeTrackAsync(source, "yo", _directory, CancellationToken.None));

        Assert.Empty(Directory.EnumerateFiles(_directory, "*.normalized.wav"));
        Assert.False(File.Exists(Path.Combine(_directory, "yo.srt")));
    }

    [Fact]
    public async Task TranscribeTrackAsync_RemovesPartialNormalizedAudioWhenNormalizerFails()
    {
        var source = CreateSource("yo.wav");
        var engine = CreateEngine(new FakeWhisperRunner(), new FailingNormalizer());

        await Assert.ThrowsAsync<IOException>(() =>
            engine.TranscribeTrackAsync(source, "yo", _directory, CancellationToken.None));

        Assert.Empty(Directory.EnumerateFiles(_directory, "*.normalized.wav"));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public async Task TranscribeTrackAsync_RemovesNormalizedAudioWhenCancelled()
    {
        var source = CreateSource("yo.wav");
        var engine = CreateEngine(new FakeWhisperRunner { Cancel = true }, new CopyingNormalizer());
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            engine.TranscribeTrackAsync(source, "yo", _directory, cancellation.Token));

        Assert.Empty(Directory.EnumerateFiles(_directory, "*.normalized.wav"));
    }

    private string CreateSource(string name)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, [1, 2, 3, 4, 5, 6]);
        return path;
    }

    private TranscriptionEngine CreateEngine(FakeWhisperRunner runner, IAudioNormalizer normalizer)
    {
        var executable = Path.Combine(_directory, "whisper-cli.exe");
        var model = Path.Combine(_directory, "ggml-test.bin");
        File.WriteAllText(executable, "test executable placeholder");
        File.WriteAllText(model, "test model placeholder");
        return new TranscriptionEngine(executable, model, "es", runner, normalizer);
    }

    private sealed class CopyingNormalizer : IAudioNormalizer
    {
        public void Normalize(string sourcePath, string destinationPath) =>
            File.Copy(sourcePath, destinationPath);
    }

    private sealed class FailingNormalizer : IAudioNormalizer
    {
        public void Normalize(string sourcePath, string destinationPath)
        {
            File.WriteAllBytes(destinationPath, [9, 8, 7]);
            throw new IOException("synthetic normalization failure");
        }
    }

    private sealed class FakeWhisperRunner : IWhisperProcessRunner
    {
        public Exception? Failure { get; init; }
        public bool Cancel { get; init; }
        public bool SuppressOutput { get; init; }
        public int Invocations { get; private set; }
        public ProcessStartInfo? LastStartInfo { get; private set; }

        public Task<WhisperProcessResult> RunAsync(
            ProcessStartInfo startInfo,
            CancellationToken cancellationToken = default)
        {
            Invocations++;
            LastStartInfo = startInfo;
            cancellationToken.ThrowIfCancellationRequested();
            if (Cancel) return Task.FromCanceled<WhisperProcessResult>(new CancellationToken(canceled: true));
            if (Failure is not null) throw Failure;

            var outputIndex = startInfo.ArgumentList.IndexOf("-of");
            Assert.True(outputIndex >= 0 && outputIndex + 1 < startInfo.ArgumentList.Count);
            if (!SuppressOutput)
                File.WriteAllText(startInfo.ArgumentList[outputIndex + 1] + ".srt",
                    "1\n00:00:00,000 --> 00:00:01,000\nrespuesta simulada\n");
            return Task.FromResult(new WhisperProcessResult(0, string.Empty, string.Empty));
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
