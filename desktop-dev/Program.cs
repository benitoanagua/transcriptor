using Avalonia;

namespace Transcriptor.Dev;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // El smoke de CI corre en headless: no requiere display ni WSLg, y evita
        // la indefinición del apagado X11 observada en entornos sin compositor.
        var smoke = args.Any(arg => string.Equals(arg, "--smoke-test", StringComparison.OrdinalIgnoreCase));
        var captureUiReference = args.Any(arg =>
            string.Equals(arg, "--capture-ui-reference", StringComparison.OrdinalIgnoreCase));
        var captureGallery = args.Any(arg =>
            string.Equals(arg, "--capture-spectrum-gallery", StringComparison.OrdinalIgnoreCase));
        var builder = BuildAvaloniaApp();
        if (smoke || captureUiReference || captureGallery)
            builder = Avalonia.Headless.AvaloniaHeadlessPlatformExtensions.UseHeadless(
                builder, new Avalonia.Headless.AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = !captureUiReference && !captureGallery
                });
        return builder.StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
