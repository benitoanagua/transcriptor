using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
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
        var windowClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await session.Dispatch(() =>
        {
            window = new MainWindow(CreateServices())
            {
                Width = 720,
                Height = 820
            };
            window.Closed += (_, _) =>
            {
                windowClosed.TrySetResult(true);
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

                var record = currentWindow.FindControl<Button>("RecordButton")!;
                var pause = currentWindow.FindControl<Button>("PauseButton")!;
                var stop = currentWindow.FindControl<Button>("StopButton")!;
                Assert.Equal(56, record.Bounds.Width);
                Assert.Equal(40, pause.Bounds.Width);
                Assert.Equal(40, stop.Bounds.Width);
                Assert.Equal(40, currentWindow.FindControl<Border>("MicrophoneLane")!.Bounds.Height);
                Assert.Equal(40, currentWindow.FindControl<Border>("SystemLane")!.Bounds.Height);

                var scrollBars = currentWindow.GetVisualDescendants()
                    .OfType<ScrollBar>()
                    .ToArray();
                Assert.True(
                    scrollBars.Any(scrollBar => Math.Abs(scrollBar.Bounds.Width - 6) < 0.1),
                    string.Join("; ", scrollBars.Select(scrollBar =>
                        $"{scrollBar.Orientation}: width={scrollBar.Width}, min={scrollBar.MinWidth}, bounds={scrollBar.Bounds.Width}")));

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
                await windowClosed.Task.WaitAsync(TimeSpan.FromSeconds(2));
            }
            finally
            {
                // Avalonia owns a dispatcher thread; do not block the test synchronization context on teardown.
                await Task.Run(session.Dispose);
            }
        }
    }

    private static UiServices CreateServices()
    {
        var root = Path.Combine(Path.GetTempPath(), "transcriptor-ui-geometry", Guid.NewGuid().ToString("N"));
        var paths = new TestPaths(root);
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
        public override void Initialize() => Styles.Add(new FluentTheme());
    }

    private sealed record TestPaths(string Root) : IDefaultPaths
    {
        public string SettingsPath => Path.Combine(Root, "settings.json");
        public string RecordingsRoot => Path.Combine(Root, "recordings");
        public string WhisperExecutablePath => Path.Combine(Root, "whisper");
        public string WhisperModelPath => Path.Combine(Root, "model.bin");
        public string? WhisperExecutablePickerPattern => null;
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
