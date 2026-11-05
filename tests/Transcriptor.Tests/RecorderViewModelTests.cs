using Xunit;
using Transcriptor.Transcription;
using Transcriptor.Transcription.Tests;
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
        public bool PrefersNativeWindowChrome => true;
        public string Name => "Prueba";
    }

    private sealed class TestLogSink : ILogSink
    {
        public void Log(string message) { }
    }

    private sealed class TestHost : IUiHost
    {
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> PickFileAsync(string title, string? pattern) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFileAsync(string title, string pattern) => Task.FromResult<string?>(null);
        public Task OpenFolderAsync(string path) => Task.CompletedTask;
        public Task<bool> CopyToClipboardAsync(string text) => Task.FromResult(true);
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
