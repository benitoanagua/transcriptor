using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using Transcriptor.Transcription;
using Transcriptor.UI;
using Transcriptor.UI.ViewModels;
using Xunit;

namespace Transcriptor.Tests;

public sealed class StudioGeometryTests
{
    [Fact]
    public async Task StudioUsesOneColumnAndStaysWithinDesktopWidthLimit()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(StudioHeadlessApp));
        MainWindow? window = null;

        await session.Dispatch(() =>
        {
            window = new MainWindow(CreateServices())
            {
                Width = 720,
                Height = 820
            };
            window.Show();
        }, CancellationToken.None);
        await Task.Delay(100);

        try
        {
            await session.Dispatch(() =>
            {
                var currentWindow = window!;
                Assert.Equal(720, currentWindow.Bounds.Width, precision: 0);
                Assert.Equal(720, currentWindow.MaxWidth, precision: 0);
                Assert.False(currentWindow.CanMaximize);
                Assert.Equal(40, currentWindow.FindControl<Border>("TitleBar")!.Bounds.Height);
                Assert.Equal(32, currentWindow.FindControl<Border>("StatusBar")!.Bounds.Height);
                Assert.True(currentWindow.UseLayoutRounding);

                var captureGrid = currentWindow.FindControl<Grid>("CaptureGrid")!;
                Assert.Single(captureGrid.ColumnDefinitions);
                Assert.Equal(3, captureGrid.RowDefinitions.Count);
                var cards = new[]
                {
                    currentWindow.FindControl<Border>("CaptureCardA")!,
                    currentWindow.FindControl<Border>("CaptureCardB")!,
                    currentWindow.FindControl<Border>("CaptureCardC")!
                };
                for (var index = 0; index < cards.Length; index++)
                {
                    Assert.Equal(0, Grid.GetColumn(cards[index]));
                    Assert.Equal(index, Grid.GetRow(cards[index]));
                    Assert.Equal(captureGrid.Bounds.Width, cards[index].Bounds.Width, precision: 1);
                }

                var record = currentWindow.FindControl<ToggleButton>("RecordButton")!;
                var pause = currentWindow.FindControl<Button>("PauseButton")!;
                var stop = currentWindow.FindControl<Button>("StopButton")!;
                var transcribe = currentWindow.FindControl<Button>("TranscribeButton")!;
                Assert.Equal(32, record.Bounds.Height);
                Assert.Equal(32, pause.Bounds.Height);
                Assert.Equal(32, stop.Bounds.Height);
                Assert.Equal(32, transcribe.Bounds.Height);
                Assert.Equal(40, currentWindow.FindControl<Border>("MicrophoneLane")!.Bounds.Height);
                Assert.Equal(40, currentWindow.FindControl<Border>("SystemLane")!.Bounds.Height);

                var scrollBars = currentWindow.GetVisualDescendants()
                    .OfType<ScrollBar>()
                    .ToArray();
                Assert.Contains(scrollBars, scrollBar => scrollBar.Bounds.Width > 0);

                currentWindow.Width = 360;
                currentWindow.Height = 600;
            }, CancellationToken.None);
            await Task.Delay(100);

            await session.Dispatch(() =>
            {
                var currentWindow = window!;
                Assert.Equal(360, currentWindow.Bounds.Width, precision: 0);
                Assert.Equal(currentWindow.FindControl<Grid>("CaptureGrid")!.Bounds.Width,
                    currentWindow.FindControl<Border>("CaptureCardA")!.Bounds.Width, precision: 1);

                var viewModel = Assert.IsType<MainViewModel>(currentWindow.DataContext);
                viewModel.ShowLibraryPageCommand.Execute(null);
                Assert.True(currentWindow.FindControl<Border>("LibraryPage")!.IsVisible);
                Assert.False(currentWindow.FindControl<ScrollViewer>("StudioPage")!.IsVisible);
                Assert.Equal(1, viewModel.SelectedNavigationIndex);
            }, CancellationToken.None);
        }
        finally
        {
            try
            {
                await session.Dispatch(() => window?.Close(), CancellationToken.None);
            }
            finally
            {
                // Avalonia owns a dispatcher thread; do not block the test synchronization context on teardown.
                await Task.Run(session.Dispose);
            }
        }
    }

    /// <summary>FI-01 por teclado sobre la ventana real: bloqueo por autorización, Ctrl+R, Ctrl+P, navegación y Toast.</summary>
    [Fact]
    public async Task ConsentGateTransportAndToastAreDrivenByKeyboard()
    {
        var root = Path.Combine(Path.GetTempPath(), "transcriptor-ui-keyboard", Guid.NewGuid().ToString("N"));
        var recordingsRoot = Path.Combine(root, "recordings");
        Directory.CreateDirectory(recordingsRoot);
        var session = HeadlessUnitTestSession.StartNew(typeof(StudioHeadlessApp));
        MainWindow? window = null;

        await session.Dispatch(() =>
        {
            window = new MainWindow(CreateKeyboardServices(root, recordingsRoot)) { Width = 720, Height = 820 };
            window.Show();
        }, CancellationToken.None);
        await Task.Delay(150);

        try
        {
            await session.Dispatch(() =>
            {
                var currentWindow = window!;
                var viewModel = Assert.IsType<MainViewModel>(currentWindow.DataContext);
                viewModel.Recorder.SetRecordingsDirectory(recordingsRoot);

                Press(currentWindow, Key.Escape);
                Assert.False(viewModel.DevelopmentBannerVisible);

                // RN-01: sin autorización confirmada no se graba, tampoco por atajo.
                Assert.False(viewModel.Recorder.CanRecordNow);
                Press(currentWindow, Key.R, control: true);
                Assert.False(viewModel.Recorder.IsRecordingActive);

                var consentCheck = currentWindow.FindControl<CheckBox>("ConsentCheck")!;
                consentCheck.Focus();
                consentCheck.IsChecked = true;
                viewModel.Recorder.ConsentGranted = true;
                Assert.True(viewModel.Recorder.ConsentGranted);

                Press(currentWindow, Key.R, control: true);
                Assert.True(viewModel.Recorder.IsRecordingActive);

                Press(currentWindow, Key.P, control: true);
                Assert.True(viewModel.Recorder.IsPaused);
                Press(currentWindow, Key.P, control: true);
                Assert.False(viewModel.Recorder.IsPaused);

                Press(currentWindow, Key.D2, control: true);
                Assert.True(currentWindow.FindControl<Border>("LibraryPage")!.IsVisible);
                Press(currentWindow, Key.D1, control: true);
                Assert.True(currentWindow.FindControl<ScrollViewer>("StudioPage")!.IsVisible);

                viewModel.ShowLibraryPageCommand.Execute(null);
                Assert.Equal(1, viewModel.SelectedNavigationIndex);
                viewModel.SelectedNavigationIndex = 0;
                Assert.Equal(0, viewModel.SelectedNavigationIndex);

                currentWindow.FindControl<Button>("StopButton")!.Focus();
                Press(currentWindow, Key.Space);
            }, CancellationToken.None);
            await Task.Delay(400);

            await session.Dispatch(() =>
            {
                var currentWindow = window!;
                var viewModel = Assert.IsType<MainViewModel>(currentWindow.DataContext);
                Assert.False(viewModel.Recorder.IsRecordingActive);
                Assert.True(currentWindow.FindControl<Transcriptor.UI.Spectrum.SpectrumToast>("SavedSessionToast")!.IsVisible);
                Assert.NotNull(viewModel.Library.SelectedRecording);
            }, CancellationToken.None);
        }
        finally
        {
            try
            {
                await session.Dispatch(() => window?.Close(), CancellationToken.None);
            }
            finally
            {
                // Avalonia owns a dispatcher thread; do not block the test synchronization context on teardown.
                await Task.Run(session.Dispose);
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>Envía una pulsación cruda de tecla a la ventana headless.</summary>
    private static void Press(Window window, Key key, bool control = false)
    {
        var modifiers = control ? RawInputModifiers.Control : RawInputModifiers.None;
        var (physical, symbol) = key switch
        {
            Key.Escape => (PhysicalKey.Escape, "Escape"),
            Key.Space => (PhysicalKey.Space, " "),
            Key.Right => (PhysicalKey.ArrowRight, "ArrowRight"),
            Key.Home => (PhysicalKey.Home, "Home"),
            Key.R => (PhysicalKey.R, "r"),
            Key.P => (PhysicalKey.P, "p"),
            Key.D1 => (PhysicalKey.Digit1, "1"),
            Key.D2 => (PhysicalKey.Digit2, "2"),
            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };
        ((TopLevel)window).KeyPress(key, modifiers, physical, symbol);
    }

    private static UiServices CreateKeyboardServices(string root, string recordingsRoot) => new(
        new KeyboardDeviceCatalog(),
        new TrackPublishingCaptureFactory(),
        new UnsupportedPlayerFactory(),
        new NoOpSecurity(),
        new UnsupportedMigration(),
        new KeyboardCapabilities(),
        SystemClock.Instance,
        new TestPaths(root, recordingsRoot),
        (_, _, _) => throw new NotSupportedException())
    {
        LevelMonitorFactory = new KeyboardLevelMonitorFactory()
    };

    private static UiServices CreateServices()
    {
        var root = Path.Combine(Path.GetTempPath(), "transcriptor-ui-geometry", Guid.NewGuid().ToString("N"));
        var paths = new TestPaths(root, Path.Combine(root, "recordings"));
        return new UiServices(
            new EmptyDeviceCatalog(),
            new UnsupportedCaptureFactory(),
            new UnsupportedPlayerFactory(),
            new NoOpSecurity(),
            new UnsupportedMigration(),
            new TestCapabilities(),
            SystemClock.Instance,
            paths,
            (_, _, _) => throw new NotSupportedException());
    }

    private sealed class StudioHeadlessApp : Application
    {
        public override void Initialize()
        {
            RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://Transcriptor.UI/Theming/Tokens.axaml"))
            {
                Source = new Uri("avares://Transcriptor.UI/Theming/Tokens.axaml")
            });
            Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://Transcriptor.UI/Theming/Icons.axaml"))
            {
                Source = new Uri("avares://Transcriptor.UI/Theming/Icons.axaml")
            });
            Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://Transcriptor.UI.Spectrum/Theming/ControlThemes.axaml"))
            {
                Source = new Uri("avares://Transcriptor.UI.Spectrum/Theming/ControlThemes.axaml")
            });
        }
    }

    private sealed record TestPaths(string Root, string RecordingsRoot) : IDefaultPaths
    {
        public string SettingsPath => Path.Combine(Root, "settings.json");
        public string WhisperExecutablePath => Path.Combine(Root, "whisper");
        public string WhisperModelPath => Path.Combine(Root, "model.bin");
        public string? WhisperExecutablePickerPattern => null;
    }

    private sealed class KeyboardDeviceCatalog : IDeviceCatalog
    {
        private static readonly AudioDeviceOption Microphone = new("key-mic", "Micrófono de prueba");
        private static readonly AudioDeviceOption Output = new("key-out", "Salida de prueba");

        public IReadOnlyList<AudioDeviceOption> GetCaptureDevices() => [Microphone];
        public IReadOnlyList<AudioDeviceOption> GetOutputDevices() => [Output];
        public string? GetDefaultCaptureDeviceId() => Microphone.Id;
        public string? GetDefaultOutputDeviceId() => Output.Id;
    }

    private sealed class KeyboardCapabilities : IPlatformCapabilities
    {
        public bool SupportsSystemLoopback => true;
        public bool SupportsEncryptionAtRest => false;
        public bool SupportsAudioPlayback => false;
        public bool SupportsMixedAudioPlayback => false;
        public bool SupportsVariablePlaybackRate => false;
        public bool PrefersNativeWindowChrome => true;
        public string Name => "Teclado";
    }

    /// <summary>Publica las dos pistas al crear la sesión, de modo que la biblioteca la liste al detener.</summary>
    private sealed class TrackPublishingCaptureFactory : IAudioCaptureFactory
    {
        public IAudioCaptureSession Create(string outputDirectory, string? microphoneDeviceId, string? systemDeviceId)
        {
            Directory.CreateDirectory(outputDirectory);
            var layout = new SessionLayout(outputDirectory);
            File.WriteAllBytes(layout.AudioTrack(SessionLayout.MicrophoneTrackName), []);
            File.WriteAllBytes(layout.AudioTrack(SessionLayout.SystemTrackName), []);
            return new KeyboardCaptureSession();
        }
    }

    private sealed class KeyboardCaptureSession : IAudioCaptureSession
    {
        private bool _paused;

        public string? MicrophoneDeviceName => "Micrófono de prueba";
        public string? SystemDeviceName => "Salida de prueba";
        public TimeSpan Elapsed => TimeSpan.FromSeconds(1);
        public bool IsPaused => _paused;
        public event Action<Exception>? Faulted { add { } remove { } }
        public float MicrophonePeak => 0f;
        public float MicrophoneRms => 0f;
        public float SystemPeak => 0f;
        public float SystemRms => 0f;
        public void Start() { }
        public void Stop() { }
        public void Pause() => _paused = true;
        public void Resume() => _paused = false;
        public void Dispose() { }
    }

    private sealed class KeyboardLevelMonitorFactory : IAudioLevelMonitorFactory
    {
        public IAudioLevelMonitor Create(
            string? microphoneDeviceId, string? systemDeviceId, bool includeSystemLoopback)
            => new KeyboardLevelMonitor();
    }

    private sealed class KeyboardLevelMonitor : IAudioLevelMonitor
    {
        public event Action<Exception>? Faulted { add { } remove { } }
        public float MicrophonePeak => 0f;
        public float MicrophoneRms => 0f;
        public float SystemPeak => 0f;
        public float SystemRms => 0f;
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }

    private sealed class EmptyDeviceCatalog : IDeviceCatalog
    {
        public IReadOnlyList<AudioDeviceOption> GetCaptureDevices() => [];
        public IReadOnlyList<AudioDeviceOption> GetOutputDevices() => [];
        public string? GetDefaultCaptureDeviceId() => null;
        public string? GetDefaultOutputDeviceId() => null;
    }

    private sealed class UnsupportedCaptureFactory : IAudioCaptureFactory
    {
        public IAudioCaptureSession Create(string outputDirectory, string? microphoneDeviceId, string? systemDeviceId)
            => throw new NotSupportedException();
    }

    private sealed class UnsupportedPlayerFactory : IAudioPlayerFactory
    {
        public IAudioPlayer Create(string waveFilePath) => throw new NotSupportedException();
    }

    private sealed class NoOpSecurity : ISessionDirectorySecurity
    {
        public void ValidateDirectoryPath(string directory) { }
        public void ValidateSessionDirectory(string sessionDirectory) { }
        public void RestrictSessionDirectory(string sessionDirectory) { }
    }

    private sealed class UnsupportedMigration : ISessionMigrationService
    {
        public RecordingMigrationResult MigrateSession(string sessionDirectory) => throw new NotSupportedException();
    }

    private sealed class TestCapabilities : IPlatformCapabilities
    {
        public bool SupportsSystemLoopback => false;
        public bool SupportsEncryptionAtRest => false;
        public bool SupportsAudioPlayback => false;
        public bool SupportsMixedAudioPlayback => false;
        public bool SupportsVariablePlaybackRate => false;
        public bool PrefersNativeWindowChrome => true;
        public string Name => "Pruebas";
    }
}
