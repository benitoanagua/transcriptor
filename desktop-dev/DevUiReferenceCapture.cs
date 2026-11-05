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

        if (!viewModel.State.TryBegin())
            throw new InvalidOperationException("No se pudo preparar el estado de transcripción de ejemplo.");
        viewModel.Transcript.TranscriptionProgressText = "Procesando pista MIC · 42 %";
        foreach (var size in Sizes)
        {
            await SetSizeAndWaitAsync(window, size.Width, size.Height);
            Capture(window, outputDirectory, "transcribiendo", size.Suffix);
        }
        viewModel.State.Complete();
        viewModel.Transcript.TranscriptionProgressText = "Configura Whisper en AJUSTES para transcribir";

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
