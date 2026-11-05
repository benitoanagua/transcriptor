using System.Diagnostics;
using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class WhisperConfigurationTests
{
    [Fact]
    public async Task TestWhisperAsyncRunsLocalHelpWithoutLoadingModel()
    {
        using var files = new TestFiles();
        var runner = new TestRunner(new WhisperProcessResult(0, "usage", string.Empty));
        var engine = CreateEngine(files, runner);

        await engine.TestWhisperAsync();

        Assert.Equal(1, runner.Invocations);
        Assert.False(runner.StartInfo!.UseShellExecute);
        Assert.True(runner.StartInfo.RedirectStandardOutput);
        Assert.True(runner.StartInfo.RedirectStandardError);
        Assert.Equal(["--help"], runner.StartInfo.ArgumentList);
    }

    [Fact]
    public async Task TestWhisperAsyncReportsProcessFailure()
    {
        using var files = new TestFiles();
        var engine = CreateEngine(files, new TestRunner(
            new WhisperProcessResult(7, string.Empty, "modelo inválido")));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.TestWhisperAsync());

        Assert.Contains("modelo inválido", error.Message);
    }

    [Fact]
    public async Task TestWhisperAsyncStopsAtTimeout()
    {
        using var files = new TestFiles();
        var runner = new TestRunner(async cancellationToken =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new WhisperProcessResult(0, string.Empty, string.Empty);
        });
        var engine = CreateEngine(files, runner, TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAsync<TimeoutException>(() => engine.TestWhisperAsync());
    }

    [Fact]
    public async Task TestWhisperAsyncDoesNotStartWhenModelIsMissing()
    {
        using var files = new TestFiles();
        File.Delete(files.ModelPath);
        var runner = new TestRunner(new WhisperProcessResult(0, string.Empty, string.Empty));
        var engine = CreateEngine(files, runner);

        await Assert.ThrowsAsync<FileNotFoundException>(() => engine.TestWhisperAsync());

        Assert.Equal(0, runner.Invocations);
    }

    private static TranscriptionEngine CreateEngine(
        TestFiles files, IWhisperProcessRunner runner, TimeSpan? timeout = null) =>
        new(files.ExecutablePath, files.ModelPath, "es", runner, new TestNormalizer(),
            new PassThroughSessionSecurity(), timeout);

    private sealed class TestRunner(WhisperProcessResult result) : IWhisperProcessRunner
    {
        private readonly Func<CancellationToken, Task<WhisperProcessResult>> _run = _ => Task.FromResult(result);

        public TestRunner(Func<CancellationToken, Task<WhisperProcessResult>> run) : this(
            new WhisperProcessResult(0, string.Empty, string.Empty))
        {
            _run = run;
        }

        public int Invocations { get; private set; }
        public ProcessStartInfo? StartInfo { get; private set; }

        public Task<WhisperProcessResult> RunAsync(
            ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
        {
            Invocations++;
            StartInfo = startInfo;
            return _run(cancellationToken);
        }
    }

    private sealed class TestNormalizer : IAudioNormalizer
    {
        public void Normalize(string sourcePath, string destinationPath) { }
    }

    private sealed class TestFiles : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "TranscriptorWhisperTest", Guid.NewGuid().ToString("N"));

        public TestFiles()
        {
            Directory.CreateDirectory(_root);
            ExecutablePath = Path.Combine(_root, OperatingSystem.IsWindows() ? "whisper.exe" : "whisper");
            ModelPath = Path.Combine(_root, "model.bin");
            File.WriteAllBytes(ExecutablePath, []);
            File.WriteAllBytes(ModelPath, []);
        }

        public string ExecutablePath { get; }
        public string ModelPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }
}
