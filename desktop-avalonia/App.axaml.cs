using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Transcriptor.Platform.Windows;
using Transcriptor.Transcription;
using Transcriptor.UI;
namespace Transcriptor.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    /// <summary>
    /// Composition root of the Windows release head: production capture, playback,
    /// DPAPI-backed protection, NTFS session security and the real Whisper runner.
    /// </summary>
    private static UiServices CreateServices()
    {
        EncryptedFileContainer.ConfigureDefaultSecretProtector(DpapiSecretProtector.Instance);
        var security = new WindowsSessionDirectorySecurity();
        return new UiServices(
            new WasapiDeviceCatalog(),
            new WasapiCaptureFactory(),
            new WasapiAudioPlayerFactory(),
            security,
            new RecordingMigrationService(),
            new WindowsPlatformCapabilities(),
            SystemClock.Instance,
            new PlatformDefaultPaths(),
            (executable, model, language) => new TranscriptionEngine(
                executable, model, language, security, new WaveAudioNormalizer()))
        {
            LevelMonitorFactory = new WasapiAudioLevelMonitorFactory()
        };
    }

    /// <summary>The --smoke-test startup hook is invoked by CI and exits with code 0 after two seconds.</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = CreateServices();
            desktop.MainWindow = new MainWindow(services);
            if (Environment.GetCommandLineArgs().Any(arg =>
                string.Equals(arg, "--smoke-test", StringComparison.OrdinalIgnoreCase)))
            {
                var smokeTimer = new Avalonia.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                smokeTimer.Tick += (_, _) =>
                {
                    smokeTimer.Stop();
                    desktop.Shutdown(0);
                };
                smokeTimer.Start();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
