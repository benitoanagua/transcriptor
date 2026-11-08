using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Transcriptor.UI;
using Transcriptor.UI.Spectrum;
using Transcriptor.UI.ViewModels;

namespace Transcriptor.Dev;

internal static class DevUiReferenceCapture
{
    private static readonly (double Width, double Height, string Suffix)[] Sizes =
    [
        (1000, 680, "1000x680"),
        (1380, 820, "1380x820")
    ];

    public static async Task CaptureAsync(MainWindow window)
    {
        var outputDirectory = Path.Combine(Environment.CurrentDirectory, "docs", "ui-reference");
        Directory.CreateDirectory(outputDirectory);
        var viewModel = (MainViewModel)window.DataContext!;

        await Task.Delay(300);

        // ---- Captura de cada pantalla, navegando de verdad ----
        await CapturarPantalla(window, viewModel, outputDirectory, "estudio-reposo", async () =>
        {
            viewModel.ShowRecordingPageCommand.Execute(null);
        });

        await CapturarPantalla(window, viewModel, outputDirectory, "estudio-probando-niveles", async () =>
        {
            viewModel.ShowRecordingPageCommand.Execute(null);
            viewModel.Recorder.ConsentGranted = true;
            viewModel.Recorder.StartLevelTestingCommand.Execute(null);
            await Task.Delay(300);
        });

        await CapturarPantalla(window, viewModel, outputDirectory, "estudio-grabando", async () =>
        {
            viewModel.ShowRecordingPageCommand.Execute(null);
            viewModel.Recorder.ConsentGranted = true;
            viewModel.Recorder.StartRecordingCommand.Execute(null);
            if (!viewModel.Recorder.IsRecordingActive)
                throw new InvalidOperationException("No se inició la captura sintética.");
            await Task.Delay(300);
        });

        await CapturarPantalla(window, viewModel, outputDirectory, "estudio-pausado", async () =>
        {
            viewModel.ShowRecordingPageCommand.Execute(null);
            viewModel.Recorder.ConsentGranted = true;
            viewModel.Recorder.StartRecordingCommand.Execute(null);
            await Task.Delay(200);
            viewModel.Recorder.PauseRecordingCommand.Execute(null);
            await Task.Delay(300);
        });

        await CapturarPantalla(window, viewModel, outputDirectory, "estudio-fallo-captura", async () =>
        {
            viewModel.ShowRecordingPageCommand.Execute(null);
            viewModel.Recorder.CaptureFaultMessage =
                "Se cerraron ambas pistas; la sesión quedó marcada como incompleta. Dispositivo desconectado.";
            await Task.Delay(150);
        });

        await CapturarPantalla(window, viewModel, outputDirectory, "estudio-sin-dispositivo-sistema", async () =>
        {
            viewModel.ShowRecordingPageCommand.Execute(null);
            var savedOutput = viewModel.Recorder.SelectedOutput;
            viewModel.Recorder.SelectedOutput = null;
            await Task.Delay(150);
            viewModel.Recorder.SelectedOutput = savedOutput;
        });

        await CapturarPantalla(window, viewModel, outputDirectory, "estudio-whisper-no-configurado", async () =>
        {
            viewModel.ShowRecordingPageCommand.Execute(null);
            viewModel.IsWhisperAvailable = false;
            await Task.Delay(150);
        });

        await CapturarPantalla(window, viewModel, outputDirectory, "estudio-transcribiendo", async () =>
        {
            viewModel.ShowRecordingPageCommand.Execute(null);
            if (!viewModel.State.TryBegin())
                throw new InvalidOperationException("No se pudo preparar el estado de transcripción.");
            viewModel.Transcript.TranscriptionProgressText = "Procesando pista MIC · 42 %";
            viewModel.IsWhisperAvailable = true;
            await Task.Delay(150);
        });

        // Biblioteca con sesiones de muestra
        await CapturarPantalla(window, viewModel, outputDirectory, "biblioteca-con-sesiones", async () =>
        {
            viewModel.ShowLibraryPageCommand.Execute(null);
            await Task.Delay(200);
        });

        // Biblioteca vacía (sin resultados de búsqueda)
        await CapturarPantalla(window, viewModel, outputDirectory, "biblioteca-vacia", async () =>
        {
            viewModel.ShowLibraryPageCommand.Execute(null);
            viewModel.Library.LibrarySearchText = "sin-resultados-ui-reference";
            await Task.Delay(150);
        });

        // Ajustes normal
        await CapturarPantalla(window, viewModel, outputDirectory, "ajustes-normal", async () =>
        {
            viewModel.ShowSettingsPageCommand.Execute(null);
            await Task.Delay(150);
        });

        // Ajustes con error de validación en carpeta de grabaciones
        await CapturarPantalla(window, viewModel, outputDirectory, "ajustes-error-validacion", async () =>
        {
            viewModel.ShowSettingsPageCommand.Execute(null);
            viewModel.Settings.RecordingsDirectoryText = "/ruta/inexistente";
            await Task.Delay(150);
        });

        // Registro
        await CapturarPantalla(window, viewModel, outputDirectory, "registro", async () =>
        {
            viewModel.ShowLogPageCommand.Execute(null);
            await Task.Delay(150);
        });

        // ---- Densidad compacta en Estudio reposo ----
        await CapturarPantalla(window, viewModel, outputDirectory, "estudio-reposo-compacto", async () =>
        {
            viewModel.ShowRecordingPageCommand.Execute(null);
            viewModel.Settings.DensityCompact = true;
            await Task.Delay(150);
        });

        // ---- Toast ----
        await CapturarPantalla(window, viewModel, outputDirectory, "toast-sesion-guardada", async () =>
        {
            viewModel.ShowRecordingPageCommand.Execute(null);
            var toast = window.FindControl<Transcriptor.UI.Spectrum.SpectrumToast>("SavedSessionToast");
            if (toast is not null) toast.IsVisible = true;
            await Task.Delay(150);
        });
    }

    private static async Task CapturarPantalla(
        MainWindow window, MainViewModel viewModel, string outputDirectory,
        string estado, Action prepararPantalla)
    {
        prepararPantalla();

        foreach (var size in Sizes)
        {
            await SetSizeYContenidoCompleto(window, size.Width, size.Height);
            Captar(window, outputDirectory, estado, size.Suffix);
        }
    }

    private static async Task SetSizeYContenidoCompleto(Window window, double ancho, double alto)
    {
        window.Width = ancho;
        window.Height = alto;
        await Task.Delay(200);

        // Forzar layout y medir el contenido real para capturar sin pliegue
        window.UpdateLayout();
        await Task.Delay(100);

        // Buscar el ScrollViewer o el contenido raíz para medir el alto real
        var contenido = window.FindDescendant<ScrollViewer>();
        if (contenido is not null)
        {
            contenido.Measure(new Size(ancho, double.PositiveInfinity));
            var altoContenido = Math.Ceiling(contenido.DesiredSize.Height) + 40;
            var altoVentana = Math.Max(alto, altoContenido);
            window.Height = altoVentana;
            await Task.Delay(200);
        }

        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
    }

    private static T? FindDescendant<T>(this Visual visual) where T : Visual
    {
        if (visual is T t) return t;
        foreach (var child in visual.GetVisualChildren())
        {
            var encontrado = FindDescendant<T>(child);
            if (encontrado is not null) return encontrado;
        }
        return null;
    }

    private static void Captar(MainWindow window, string outputDirectory, string estado, string size)
    {
        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException($"Avalonia no produjo el frame de {estado} ({size}).");

        var anchoReal = frame.Size.Width;
        var altoReal = frame.Size.Height;
        var esperadoAncho = size switch
        {
            "1000x680" => 1000,
            "1380x820" => 1380,
            _ => 0
        };
        if (esperadoAncho > 0 && Math.Abs(anchoReal - esperadoAncho) > 2)
            throw new InvalidOperationException(
                $"La captura de {estado} ({size}) mide {anchoReal:F0} px de ancho, se esperaban {esperadoAncho}.");

        frame.Save(
            Path.Combine(outputDirectory, $"studio-{estado}-{size}.png"),
            PngBitmapEncoderOptions.Default);
    }
}
