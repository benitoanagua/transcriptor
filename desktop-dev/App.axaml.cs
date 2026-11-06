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
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    /// <summary>
    /// Composition root of the development head: synthetic capture, fake devices,
    /// an insecure dev secret protector and no-op session security. Sample data is
    /// generated in a temporary folder on first run.
    /// </summary>
    private static UiServices CreateServices(string? devRoot = null)
    {
        // INSEGURO: sustituye a DPAPI solo dentro de esta cabeza de desarrollo.
        EncryptedFileContainer.ConfigureDefaultSecretProtector(new InsecureDevSecretProtector());
        var capabilities = new DevPlatformCapabilities();
        var security = new NoOpSessionSecurity();
        var paths = new DevDefaultPaths(devRoot);
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
                executable, model, language, security, new CopyingDevNormalizer()))
        {
            LevelMonitorFactory = new FakeAudioLevelMonitorFactory()
        };
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (Environment.GetCommandLineArgs().Any(arg =>
                string.Equals(arg, "--spectrum-gallery", StringComparison.OrdinalIgnoreCase)))
            {
                var galleryWindow = new SpectrumGalleryWindow();
                desktop.MainWindow = galleryWindow;
                if (Environment.GetCommandLineArgs().Any(arg =>
                    string.Equals(arg, "--capture-spectrum-gallery", StringComparison.OrdinalIgnoreCase)))
                {
                    galleryWindow.Opened += async (_, _) =>
                    {
                        var exitCode = 0;
                        try
                        {
                            await DevSpectrumGalleryCapture.CaptureAsync(galleryWindow);
                        }
                        catch (Exception exception)
                        {
                            Console.Error.WriteLine("No se pudo capturar la galería Spectrum: " + exception);
                            exitCode = 1;
                        }
                        desktop.Shutdown(exitCode);
                    };
                }
                else if (Environment.GetCommandLineArgs().Any(arg =>
                    string.Equals(arg, "--smoke-test", StringComparison.OrdinalIgnoreCase)))
                {
                    ScheduleSmokeShutdown(desktop);
                }

                base.OnFrameworkInitializationCompleted();
                return;
            }

            var captureUiReference = Environment.GetCommandLineArgs().Any(arg =>
                string.Equals(arg, "--capture-ui-reference", StringComparison.OrdinalIgnoreCase));
            var captureDataRoot = captureUiReference
                ? Path.Combine(Path.GetTempPath(), "TranscriptorDev", "UiReference-" + Guid.NewGuid().ToString("N"))
                : null;
            var services = CreateServices(captureDataRoot);
            var mainWindow = new MainWindow(services);
            desktop.MainWindow = mainWindow;
            if (captureUiReference)
            {
                mainWindow.Opened += async (_, _) =>
                {
                    var exitCode = 0;
                    try
                    {
                        await DevUiReferenceCapture.CaptureAsync(mainWindow);
                    }
                    catch (Exception exception)
                    {
                        Console.Error.WriteLine("No se pudieron generar las capturas de referencia: " + exception);
                        exitCode = 1;
                    }
                    finally
                    {
                        try
                        {
                            if (captureDataRoot is not null && Directory.Exists(captureDataRoot))
                                Directory.Delete(captureDataRoot, recursive: true);
                        }
                        catch (Exception exception)
                        {
                            Console.Error.WriteLine("No se pudieron limpiar los datos temporales de captura: " + exception);
                            exitCode = 1;
                        }
                        desktop.Shutdown(exitCode);
                    }
                };
            }
            if (Environment.GetCommandLineArgs().Any(arg =>
                string.Equals(arg, "--smoke-test", StringComparison.OrdinalIgnoreCase)))
            {
                ScheduleSmokeShutdown(desktop);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ScheduleSmokeShutdown(IClassicDesktopStyleApplicationLifetime desktop)
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
