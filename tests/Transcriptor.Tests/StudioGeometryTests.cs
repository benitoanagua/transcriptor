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
    private static readonly int[] StandardCaptureSpans = [4, 5, 3];
    private static readonly int[] CompactCaptureSpans = [5, 7, 12];

    [Fact]
    public async Task StudioLayoutMatchesSpecifiedGeometryAtReferenceAndMinimumSizes()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(StudioHeadlessApp));
        MainWindow? window = null;
        var windowClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await session.Dispatch(() =>
        {
            window = new MainWindow(CreateServices())
            {
                Width = 1380,
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
                AssertGeometry(currentWindow, 1380, expectCompact: false);
                Assert.Equal(40, currentWindow.FindControl<Border>("TitleBar")!.Bounds.Height);
                Assert.Equal(28, currentWindow.FindControl<Border>("StatusBar")!.Bounds.Height);
                Assert.True(currentWindow.UseLayoutRounding);

                var cards = new[]
                {
                    currentWindow.FindControl<Border>("CaptureCardA")!,
                    currentWindow.FindControl<Border>("CaptureCardB")!,
                    currentWindow.FindControl<Border>("CaptureCardC")!
                };
                Assert.Equal(StandardCaptureSpans, cards.Select(Grid.GetColumnSpan));

                var captureGrid = currentWindow.FindControl<Grid>("CaptureGrid")!;
                var columnWidth = (captureGrid.Bounds.Width - captureGrid.ColumnSpacing * 11) / 12;
                for (var index = 0; index < cards.Length; index++)
                {
                    var span = StandardCaptureSpans[index];
                    var expectedWidth = span * columnWidth + (span - 1) * captureGrid.ColumnSpacing;
                    Assert.Equal(expectedWidth, cards[index].Bounds.Width, precision: 1);
                }

                var libraryPanel = currentWindow.FindControl<Grid>("LibraryWorkspacePanel")!;
                var transcriptPanel = currentWindow.FindControl<Border>("TranscriptWorkspacePanel")!;
                Assert.Equal(2, transcriptPanel.Bounds.Width / libraryPanel.Bounds.Width, precision: 1);

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

                currentWindow.Width = 1000;
                currentWindow.Height = 680;
            }, CancellationToken.None);
            await Task.Delay(100);

            await session.Dispatch(() =>
            {
                var currentWindow = window!;
                AssertGeometry(currentWindow, 1000, expectCompact: true);
                var cards = new[]
                {
                    currentWindow.FindControl<Border>("CaptureCardA")!,
                    currentWindow.FindControl<Border>("CaptureCardB")!,
                    currentWindow.FindControl<Border>("CaptureCardC")!
                };
                Assert.Equal(CompactCaptureSpans, cards.Select(Grid.GetColumnSpan));
                Assert.Equal(1, Grid.GetRow(currentWindow.FindControl<Border>("CaptureCardC")!));
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

    private static void AssertGeometry(MainWindow window, double width, bool expectCompact)
    {
        var captureGrid = window.FindControl<Grid>("CaptureGrid")!;
        Assert.Equal(width, window.Bounds.Width, precision: 0);
        Assert.Equal(expectCompact, captureGrid.Bounds.Width < 1100);
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
            (_, _, _) => throw new NotSupportedException(),
            _ => { });
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
