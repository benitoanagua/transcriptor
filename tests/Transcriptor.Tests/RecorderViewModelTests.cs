using Xunit;
using Transcriptor.Transcription;
using Transcriptor.UI;
using Transcriptor.UI.ViewModels;

namespace Transcriptor.Tests;

public sealed class RecorderViewModelTests
{
    [Fact]
    public void StartAndTranscriptionCommandsRespectTheSharedOperationLock()
    {
        var root = Path.Combine(Path.GetTempPath(), "TranscriptorRecorderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var state = new TranscriptionUiState();
        var services = CreateServices(root, new TestCaptureSession(failStop: false));
        var log = new TestLogSink();
        var recorder = new RecorderViewModel(services, state, log, action => action());
        var transcript = new TranscriptViewModel(services, state, log, new TestHost(), action => action())
        {
            IsRecordingActive = () => recorder.IsRecordingActive
        };
        recorder.RecordingStateChanged += transcript.NotifyRecordingStateChanged;
        var startCommandNotifications = 0;
        var transcribeCommandNotifications = 0;
        recorder.StartRecordingCommand.CanExecuteChanged += (_, _) => startCommandNotifications++;
        transcript.TranscribePendingCommand.CanExecuteChanged += (_, _) => transcribeCommandNotifications++;

        try
        {
            Assert.True(recorder.StartRecordingCommand.CanExecute(null));
            Assert.True(transcript.TranscribePendingCommand.CanExecute(null));

            state.TryBegin();

            Assert.False(recorder.StartRecordingCommand.CanExecute(null));
            Assert.False(transcript.TranscribePendingCommand.CanExecute(null));
            Assert.False(transcript.CancelTranscriptionCommand.CanExecute(null));
            Assert.True(startCommandNotifications > 0);
            Assert.True(transcribeCommandNotifications > 0);

            state.Complete();

            Assert.True(recorder.StartRecordingCommand.CanExecute(null));
            Assert.True(transcript.TranscribePendingCommand.CanExecute(null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LibraryAndDestructiveOperationsObserveTheSharedTranscriptionLock()
    {
        var root = Path.Combine(Path.GetTempPath(), "TranscriptorRecorderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var state = new TranscriptionUiState();
        var services = CreateServices(root, new TestCaptureSession(failStop: false));
        var log = new TestLogSink();
        var library = new LibraryViewModel(services, state, log);
        var detail = new SessionDetailViewModel(services, state, log, new TestHost(), library, () => root);

        try
        {
            Assert.True(library.CanInteract);
            Assert.True(detail.CanUseLibraryOperations);
            Assert.False(detail.SupportsMixedAudioPlayback);
            Assert.False(detail.SupportsVariablePlaybackRate);

            state.TryBegin();

            Assert.False(library.CanInteract);
            Assert.False(detail.CanUseLibraryOperations);

            state.Complete();

            Assert.True(library.CanInteract);
            Assert.True(detail.CanUseLibraryOperations);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IncompleteSelectedSessionCannotStartTranscription()
    {
        var root = Path.Combine(Path.GetTempPath(), "TranscriptorRecorderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var state = new TranscriptionUiState();
        var services = CreateServices(root, new TestCaptureSession(failStop: false));
        var library = new LibraryViewModel(services, state, new TestLogSink());
        var transcript = new TranscriptViewModel(services, state, new TestLogSink(), new TestHost(), action => action())
        {
            IsRecordingActive = () => false
        };
        transcript.AttachLibrary(library);

        try
        {
            library.SelectedRecording = new LibraryEntry(
                "incompleta", root, Path.Combine(root, "yo.trn"), Path.Combine(root, "cliente.trn"),
                hasTranscript: false, isIncomplete: true);

            Assert.False(transcript.CanTranscribeSelected);
            Assert.False(transcript.TranscribeSelectedCommand.CanExecute(null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CopySelectedTranscriptSegmentsCopiesAllInTranscriptOrder()
    {
        var root = Path.Combine(Path.GetTempPath(), "TranscriptorRecorderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var host = new TestHost();
        var transcript = new TranscriptViewModel(
            CreateServices(root, new TestCaptureSession(failStop: false)),
            new TranscriptionUiState(), new TestLogSink(), host, action => action());
        var first = new TranscriptSegmentViewModel(new TranscriptSegment(
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "Yo", "Hola"));
        var second = new TranscriptSegmentViewModel(new TranscriptSegment(
            TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4), "Cliente", "Buenos días"));

        try
        {
            transcript.Segments.Add(first);
            transcript.Segments.Add(second);
            Assert.Equal("Segmentos: 2", transcript.SegmentCountText);
            Assert.Equal("Palabras: 3", transcript.WordCountText);
            transcript.SetSelectedSegments([second, first]);
            await transcript.CopySelectedSegmentsCommand.ExecuteAsync(null);

            Assert.Equal($"Yo: Hola{Environment.NewLine}Cliente: Buenos días", host.LastCopiedText);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TranscriptSummaryCountsSegmentsAndWords()
    {
        var root = Path.Combine(Path.GetTempPath(), "TranscriptorRecorderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var transcript = new TranscriptViewModel(
            CreateServices(root, new TestCaptureSession(failStop: false)),
            new TranscriptionUiState(), new TestLogSink(), new TestHost(), action => action());
        transcript.Segments.Add(new TranscriptSegmentViewModel(new TranscriptSegment(
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "Yo", "Hola"), 1));
        transcript.Segments.Add(new TranscriptSegmentViewModel(new TranscriptSegment(
            TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4), "Cliente", "Buenos días"), 2));

        try
        {
            Assert.Equal("Segmentos: 2", transcript.SegmentCountText);
            Assert.Equal("Palabras: 3", transcript.WordCountText);
            Assert.Equal("#1", transcript.Segments[0].NumberText);
            Assert.Equal("#2", transcript.Segments[1].NumberText);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task StopForCloseAsync_ReturnsWhetherBothTracksWereClosed(bool failStop, bool expectedResult)
    {
        var root = Path.Combine(Path.GetTempPath(), "TranscriptorRecorderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var capture = new TestCaptureSession(failStop);
        var services = CreateServices(root, capture);
        var viewModel = new RecorderViewModel(
            services,
            new TranscriptionUiState(),
            new TestLogSink(),
            action => action());
        viewModel.SetRecordingsDirectory(root);
        viewModel.ConsentGranted = true;

        try
        {
            viewModel.StartRecordingCommand.Execute(null);

            var stopped = await viewModel.StopForCloseAsync();

            Assert.Equal(expectedResult, stopped);
            Assert.Equal(1, capture.StopCalls);
            Assert.True(capture.Disposed);
            Assert.False(viewModel.IsRecordingActive);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ManualStopNotifiesOnlyAfterBothTracksStopSuccessfully()
    {
        var root = Path.Combine(Path.GetTempPath(), "TranscriptorRecorderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var capture = new TestCaptureSession(failStop: false);
        var viewModel = new RecorderViewModel(
            CreateServices(root, capture), new TranscriptionUiState(), new TestLogSink(), action => action());
        viewModel.SetRecordingsDirectory(root);
        viewModel.ConsentGranted = true;
        var stoppedDirectory = string.Empty;
        viewModel.RecordingStoppedSuccessfully += path =>
        {
            stoppedDirectory = path;
            return Task.CompletedTask;
        };

        try
        {
            viewModel.StartRecordingCommand.Execute(null);

            await viewModel.StopRecordingCommand.ExecuteAsync(null);

            Assert.StartsWith(root, stoppedDirectory, StringComparison.Ordinal);
            Assert.True(capture.Disposed);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LevelTestingReadsMetersWithoutCreatingOrWritingASession()
    {
        var root = Path.Combine(Path.GetTempPath(), "TranscriptorRecorderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var factory = new TestLevelMonitorFactory();
        var services = CreateServices(root, new TestCaptureSession(failStop: false)) with
        {
            LevelMonitorFactory = factory
        };
        var viewModel = new RecorderViewModel(
            services, new TranscriptionUiState(), new TestLogSink(), action => action());

        try
        {
            Assert.True(viewModel.StartLevelTestingCommand.CanExecute(null));
            Assert.Empty(Directory.GetFileSystemEntries(root));

            viewModel.StartLevelTestingCommand.Execute(null);
            factory.Monitor.MicrophonePeak = 0.5f;
            factory.Monitor.MicrophoneRms = 0.25f;
            viewModel.Tick();

            Assert.True(viewModel.IsTestingLevels);
            Assert.True(viewModel.MicLevel > 0);
            Assert.Contains("No se crea sesión", viewModel.LevelTestStatusText);
            Assert.Empty(Directory.GetFileSystemEntries(root));

            viewModel.StopLevelTestingCommand.Execute(null);

            Assert.False(viewModel.IsTestingLevels);
            Assert.True(factory.Monitor.Disposed);
            Assert.Equal(0, viewModel.MicLevel);
            Assert.Empty(Directory.GetFileSystemEntries(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void StartingRecordingStopsAndDisposesLevelMonitorFirst()
    {
        var root = Path.Combine(Path.GetTempPath(), "TranscriptorRecorderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var capture = new TestCaptureSession(failStop: false);
        var factory = new TestLevelMonitorFactory();
        var services = CreateServices(root, capture) with { LevelMonitorFactory = factory };
        var viewModel = new RecorderViewModel(
            services, new TranscriptionUiState(), new TestLogSink(), action => action());
        viewModel.SetRecordingsDirectory(root);
        viewModel.ConsentGranted = true;

        try
        {
            viewModel.StartLevelTestingCommand.Execute(null);
            Assert.True(viewModel.IsTestingLevels);

            viewModel.StartRecordingCommand.Execute(null);

            Assert.False(viewModel.IsTestingLevels);
            Assert.True(factory.Monitor.Disposed);
            Assert.True(viewModel.IsRecordingActive);
        }
        finally
        {
            viewModel.StopForClose();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MovingBetweenStudioAndSettingsStopsLevelMonitorOnlyWhenLeavingStudio()
    {
        var root = Path.Combine(Path.GetTempPath(), "TranscriptorRecorderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var factory = new TestLevelMonitorFactory();
        var services = CreateServices(root, new TestCaptureSession(failStop: false)) with
        {
            LevelMonitorFactory = factory
        };
        var viewModel = new MainViewModel(services, new TestHost(), action => action());

        try
        {
            viewModel.Recorder.StartLevelTestingCommand.Execute(null);
            Assert.True(viewModel.Recorder.IsTestingLevels);

            viewModel.ShowLibraryPageCommand.Execute(null);
            Assert.True(viewModel.Recorder.IsTestingLevels);
            Assert.False(factory.Monitor.Disposed);

            viewModel.ShowSettingsPageCommand.Execute(null);
            Assert.False(viewModel.Recorder.IsTestingLevels);
            Assert.True(factory.Monitor.Disposed);
        }
        finally
        {
            viewModel.OnWindowClosed();
            Directory.Delete(root, recursive: true);
        }
    }

    private static UiServices CreateServices(string root, TestCaptureSession capture) => new(
        new TestDeviceCatalog(),
        new TestCaptureFactory(capture),
        new TestAudioPlayerFactory(),
        new PassThroughSessionSecurity(),
        new ReversibleSessionMigration(),
        new TestPlatformCapabilities(),
        SystemClock.Instance,
        new TestPaths(root),
        (_, _, _) => throw new InvalidOperationException("No se esperaba transcribir en esta prueba."),
        _ => { });

    private sealed class TestDeviceCatalog : IDeviceCatalog
    {
        private static readonly AudioDeviceOption[] Devices = [new("test", "Dispositivo de prueba")];
        public IReadOnlyList<AudioDeviceOption> GetCaptureDevices() => Devices;
        public IReadOnlyList<AudioDeviceOption> GetOutputDevices() => Devices;
        public string? GetDefaultCaptureDeviceId() => "test";
        public string? GetDefaultOutputDeviceId() => "test";
    }

    private sealed class TestCaptureFactory(TestCaptureSession capture) : IAudioCaptureFactory
    {
        public IAudioCaptureSession Create(string outputDirectory, string? microphoneDeviceId, string? systemDeviceId)
            => capture;
    }

    private sealed class TestLevelMonitorFactory : IAudioLevelMonitorFactory
    {
        public TestLevelMonitor Monitor { get; } = new();
        public IAudioLevelMonitor Create(
            string? microphoneDeviceId, string? systemDeviceId, bool includeSystemLoopback) => Monitor;
    }

    private sealed class TestLevelMonitor : IAudioLevelMonitor
    {
        public float MicrophonePeak { get; set; }
        public float MicrophoneRms { get; set; }
        public float SystemPeak { get; set; }
        public float SystemRms { get; set; }
        public bool Disposed { get; private set; }
        public event Action<Exception>? Faulted { add { } remove { } }
        public void Start() { }
        public void Stop() { }
        public void Dispose() => Disposed = true;
    }

    private sealed class TestCaptureSession(bool failStop) : IAudioCaptureSession
    {
        public int StopCalls { get; private set; }
        public bool Disposed { get; private set; }
        public string? MicrophoneDeviceName => "Micrófono de prueba";
        public string? SystemDeviceName => "Salida de prueba";
        public TimeSpan Elapsed => TimeSpan.Zero;
        public bool IsPaused => false;
        public event Action<Exception>? Faulted { add { } remove { } }
        public float MicrophonePeak => 0;
        public float MicrophoneRms => 0;
        public float SystemPeak => 0;
        public float SystemRms => 0;
        public void Start() { }
        public void Stop()
        {
            StopCalls++;
            if (failStop) throw new IOException("fallo simulado al cerrar pistas");
        }
        public void Pause() { }
        public void Resume() { }
        public void Dispose() => Disposed = true;
    }

    private sealed class TestAudioPlayerFactory : IAudioPlayerFactory
    {
        public IAudioPlayer Create(string waveFilePath) => throw new NotSupportedException();
    }

    private sealed class TestPlatformCapabilities : IPlatformCapabilities
    {
        public bool SupportsSystemLoopback => true;
        public bool SupportsEncryptionAtRest => true;
        public bool SupportsAudioPlayback => false;
        public bool SupportsMixedAudioPlayback => false;
        public bool SupportsVariablePlaybackRate => false;
        public bool PrefersNativeWindowChrome => true;
        public string Name => "Prueba";
    }

    private sealed class TestLogSink : ILogSink
    {
        public void Log(string message) { }
    }

    private sealed class TestHost : IUiHost
    {
        public string? LastCopiedText { get; private set; }
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> PickFileAsync(string title, string? pattern) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFileAsync(string title, string pattern) => Task.FromResult<string?>(null);
        public Task OpenFolderAsync(string path) => Task.CompletedTask;
        public Task<bool> CopyToClipboardAsync(string text)
        {
            LastCopiedText = text;
            return Task.FromResult(true);
        }
    }

    private sealed class TestPaths(string root) : IDefaultPaths
    {
        public string SettingsPath => Path.Combine(root, "settings.json");
        public string RecordingsRoot => root;
        public string WhisperExecutablePath => "whisper-test";
        public string WhisperModelPath => "model-test.bin";
        public string? WhisperExecutablePickerPattern => null;
    }
}
