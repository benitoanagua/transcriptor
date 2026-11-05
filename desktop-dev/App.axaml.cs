using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Transcriptor.Transcription;
using Transcriptor.UI;

namespace Transcriptor.Dev;

// ⚠️ INSEGURO — SOLO DESARROLLO ⚠️
// Esta cabeza existe para desarrollar y verificar la interfaz en Linux/WSLg sin
// hardware de audio ni APIs de Windows. Todo lo que cifra, protege o captura aquí
// es un fake rotulado; jamás debe empaquetarse para usuarios finales.
public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Composition root of the development head: synthetic capture, fake devices,
    /// an insecure dev secret protector and no-op session security. Sample data is
    /// generated in a temporary folder on first run.
    /// </summary>
    private static UiServices CreateServices()
    {
        // INSEGURO: sustituye a DPAPI solo dentro de esta cabeza de desarrollo.
        EncryptedFileContainer.ConfigureDefaultSecretProtector(new InsecureDevSecretProtector());
        var capabilities = new DevPlatformCapabilities();
        var security = new NoOpSessionSecurity();
        var paths = new DevDefaultPaths();
        DevSampleLibrary.EnsureCreated(paths.RecordingsRoot);
        return new UiServices(
            new FakeDeviceCatalog(),
            new FakeAudioCaptureFactory(),
            new FakeAudioPlayerFactory(),
            security,
            new DevSessionMigration(),
            capabilities,
            SystemClock.Instance,
            paths,
            (executable, model, language) => new TranscriptionEngine(
                executable, model, language, security, new CopyingDevNormalizer()),
            ApplyTheme);
    }

    private static void ApplyTheme(string? mode)
    {
        if (Application.Current is { } current)
            current.RequestedThemeVariant = mode switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = CreateServices();
            ApplyTheme(new SettingsStore(
                services.Paths.SettingsPath,
                new PhysicalSettingsFileSystem(),
                SystemClock.Instance,
                services.Paths).Load().Settings.ThemeMode);
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
                    // En X11/WSLg el Shutdown puede no drenar el bucle de mensajes;
                    // para CI la salida forzada con código 0 es el respaldo seguro.
                    Environment.Exit(0);
                };
                smokeTimer.Start();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
