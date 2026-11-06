using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Transcriptor.UI;
using Transcriptor.UI.ViewModels;

namespace Transcriptor.Dev;

internal static class DevUiReferenceCapture
{
    private static readonly (double Width, double Height, string Suffix)[] Sizes =
    [
        (1380, 820, "1380x820"),
        (1000, 680, "1000x680")
    ];

    public static async Task CaptureAsync(MainWindow window)
    {
        var outputDirectory = Path.Combine(Environment.CurrentDirectory, "docs", "ui-reference");
        Directory.CreateDirectory(outputDirectory);
        var viewModel = (MainViewModel)window.DataContext!;

        await Task.Delay(300);
        foreach (var size in Sizes)
        {
            await SetSizeAndWaitAsync(window, size.Width, size.Height);
            Capture(window, outputDirectory, "reposo", size.Suffix);
            Capture(window, outputDirectory, "whisper-no-configurado", size.Suffix);
        }

        var savedOutput = viewModel.Recorder.SelectedOutput;
        viewModel.Recorder.SelectedOutput = null;
        await Task.Delay(150);
        foreach (var size in Sizes)
        {
            await SetSizeAndWaitAsync(window, size.Width, size.Height);
            Capture(window, outputDirectory, "sin-dispositivo-sistema", size.Suffix);
        }
        viewModel.Recorder.SelectedOutput = savedOutput;

        viewModel.Recorder.CaptureFaultMessage =
            "Se cerraron ambas pistas; la sesión quedó marcada como incompleta. Dispositivo desconectado.";
        await Task.Delay(150);
        foreach (var size in Sizes)
        {
            await SetSizeAndWaitAsync(window, size.Width, size.Height);
            Capture(window, outputDirectory, "fallo-captura", size.Suffix);
        }
        viewModel.Recorder.CaptureFaultMessage = string.Empty;

        if (!viewModel.State.TryBegin())
            throw new InvalidOperationException("No se pudo preparar el estado de transcripción de ejemplo.");
        viewModel.Transcript.TranscriptionProgressText = "Procesando pista MIC · 42 %";
        // Transcribir solo es posible con Whisper operativo; el aviso queda oculto.
        viewModel.IsWhisperAvailable = true;
        foreach (var size in Sizes)
        {
            await SetSizeAndWaitAsync(window, size.Width, size.Height);
            Capture(window, outputDirectory, "transcribiendo", size.Suffix);
        }
        viewModel.State.Complete();
        viewModel.IsWhisperAvailable = false;
        viewModel.Transcript.TranscriptionProgressText = "Configura Whisper en Ajustes para transcribir";

        viewModel.Recorder.ConsentGranted = true;
        viewModel.Recorder.StartRecordingCommand.Execute(null);
        if (!viewModel.Recorder.IsRecordingActive)
            throw new InvalidOperationException("La cabeza de desarrollo no inició la captura sintética para la imagen.");
        await Task.Delay(300);
        foreach (var size in Sizes)
        {
            await SetSizeAndWaitAsync(window, size.Width, size.Height);
            Capture(window, outputDirectory, "grabando", size.Suffix);
        }

        viewModel.Recorder.PauseRecordingCommand.Execute(null);
        if (!viewModel.Recorder.IsPaused)
            throw new InvalidOperationException("La cabeza de desarrollo no pausó la captura sintética.");
        await Task.Delay(300);
        foreach (var size in Sizes)
        {
            await SetSizeAndWaitAsync(window, size.Width, size.Height);
            Capture(window, outputDirectory, "pausado", size.Suffix);
        }
        viewModel.Recorder.PauseRecordingCommand.Execute(null);

        await viewModel.Recorder.StopRecordingCommand.ExecuteAsync(null);
        if (viewModel.Recorder.IsRecordingActive)
            throw new InvalidOperationException("La cabeza de desarrollo no detuvo la captura sintética.");

        viewModel.Library.LibrarySearchText = "sin-resultados-ui-reference";
        await Task.Delay(150);
        foreach (var size in Sizes)
        {
            await SetSizeAndWaitAsync(window, size.Width, size.Height);
            Capture(window, outputDirectory, "biblioteca-vacia", size.Suffix);
        }

    }

    private static async Task SetSizeAndWaitAsync(Window window, double width, double height)
    {
        window.Width = width;
        window.Height = height;
        await Task.Delay(150);
    }

    private static void Capture(MainWindow window, string outputDirectory, string state, string size)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException($"Avalonia no produjo el frame de {state} ({size}).");
        frame.Save(
            Path.Combine(outputDirectory, $"studio-{state}-{size}.png"),
            PngBitmapEncoderOptions.Default);
    }
}
