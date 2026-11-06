using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;

namespace Transcriptor.Dev;

// Genera docs/ui-reference/spectrum-gallery.png para revisar la biblioteca de
// componentes sin compositor: el mismo camino headless que las capturas de referencia.
internal static class DevSpectrumGalleryCapture
{
    private const double Width = 1380;

    public static async Task CaptureAsync(Window window)
    {
        var outputDirectory = Path.Combine(Environment.CurrentDirectory, "docs", "ui-reference");
        Directory.CreateDirectory(outputDirectory);

        window.Width = Width;
        window.Height = 820;
        await Task.Delay(300);

        if (window.Content is not ScrollViewer scrollViewer)
            throw new InvalidOperationException("La galería debe raíz en un ScrollViewer.");
        scrollViewer.Measure(new Size(Width, double.PositiveInfinity));
        window.Height = Math.Max(820, Math.Ceiling(scrollViewer.DesiredSize.Height) + 1);
        await Task.Delay(300);

        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("Avalonia no produjo el frame de la galería.");
        frame.Save(Path.Combine(outputDirectory, "spectrum-gallery.png"), PngBitmapEncoderOptions.Default);
        Console.WriteLine(
            $"Galería Spectrum capturada en {outputDirectory}{Path.DirectorySeparatorChar}spectrum-gallery.png " +
            $"({Width:F0}x{window.Height:F0}).");
    }
}
