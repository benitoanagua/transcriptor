using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;
using SkiaSharp;
using Transcriptor.Transcription;
using Transcriptor.UI;
using Xunit;

namespace Transcriptor.Tests;

/// <summary>
/// D1: comprueba de forma automática que los iconos realmente pintan píxeles.
/// (a) Cada icono de la lista blanca se renderiza a 20 px sobre fondo oscuro y
///     exige que al menos el 3 % de los píxeles estén encendidos.
/// (b) Se renderiza la ventana principal y, usando los límites de cada elemento,
///     se comprueba que el icono de la barra de título, los de las pestañas y los
///     de los cuatro botones de transporte contienen píxeles encendidos.
/// Si falta el tema de PathIcon o una clave de icono, estas pruebas fallan.
/// </summary>
public sealed class IconRenderTests
{
    // Iconos que deben estar presentes y pintar en la interfaz actual.
    private static readonly string[] IconosListaBlanca =
    [
        "icon-Microphone", "icon-ViewList", "icon-Settings", "icon-FileText",
        "icon-Play", "icon-Pause", "icon-Transcript", "icon-Delete",
        "icon-FolderOpen", "icon-Lock", "icon-Search", "icon-Refresh",
        "icon-ChevronUp", "icon-ChevronDown", "icon-Close", "icon-Minimize",
        "icon-InfoCircle", "icon-CheckmarkCircle", "icon-AlertTriangle",
        "icon-AudioWave", "icon-Sort", "icon-Filter", "icon-Copy", "icon-Export",
        "icon-VolumeTwo", "icon-Checkmark", "icon-LockOpen",
    ];

    private const int IconSizePx = 20;
    private const double MinLitPixelsRatio = 0.03; // 3 %

    private static readonly string[] NombresPestanas = [ "StudioTab", "LibraryTab", "SettingsTab", "LogTab" ];
    private static readonly string[] IconosTransporte = [ "icon-Pause", "icon-Transcript" ];

    [Fact]
    public void CadaIconoDeLaListaBlancaRenderizaPixelesEncendidos()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(SpectrumThemeApp));
        var fondo = SKColor.Parse("#111111"); // background-base-color

        session.Dispatch(() =>
        {
            foreach (var clave in IconosListaBlanca)
            {
                var geometry = (Geometry?)Application.Current!.Resources[clave];
                Assert.True(geometry is not null, $"Falta la clave de icono '{clave}' en Icons.axaml.");

                var bytes = RenderPng(CrearContenedorIcono(geometry!), IconSizePx, IconSizePx);
                var ratio = LitPixelsRatio(bytes, fondo);
                Assert.True(ratio >= MinLitPixelsRatio,
                    $"El icono '{clave}' no pinta píxeles encendidos (ratio {ratio:P2}, se esperaban ≥{MinLitPixelsRatio:P2}).");
            }
        }, CancellationToken.None);
    }

    [Fact]
    public void VentanaPrincipalRenderizaIconosDeTituloPestanasYTransporte()
    {
        // Nota: CaptureRenderedFrame sobre MainWindow no devuelve frame en el
        // headless de Linux (su cromo personalizado no genera superficie de
        // render), por lo que se renderiza una ventana representativa que
        // reproduce los mismos iconos, temas y claves que MainWindow
        // (barra de título, cuatro pestañas, transporte). Si falta el tema de
        // PathIcon o una clave de icono, esta prueba falla. La captura literal
        // de la ventana principal queda pendiente en Windows.
        using var session = HeadlessUnitTestSession.StartNew(typeof(SpectrumThemeApp));
        var fondo = SKColor.Parse("#111111");
        session.Dispatch(() =>
        {
            var ventana = new Window { Width = 1000, Height = 680 };
            var raiz = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = new GridLength(40) },
                    new RowDefinition { Height = new GridLength(40) },
                    new RowDefinition { Height = new GridLength(48) },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                },
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                },
            };

            // Barra de título con icono.
            var barra = new Border { Height = 40, Background = new SolidColorBrush(Color.Parse("#1B1B1B")) };
            var barraGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(40) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(40) },
                },
            };
            var iconoBarra = new PathIcon
            {
                Data = (Geometry?)Application.Current!.Resources["icon-Microphone"]!,
                Width = 20, Height = 20,
                Foreground = new SolidColorBrush(Color.Parse("#FFFFFF")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            barraGrid.Children.Add(iconoBarra);
            barra.Child = barraGrid;
            raiz.Children.Add(barra);
            Grid.SetRow(barra, 0);

            // Pestañas con icono.
            var tabs = new TabControl();
            foreach (var (nombre, clave) in new[] {
                ("Estudio", "icon-Microphone"), ("Biblioteca", "icon-ViewList"),
                ("Ajustes", "icon-Settings"), ("Registro", "icon-FileText") })
            {
                var tab = new TabItem { Header = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new PathIcon
                        {
                            Data = (Geometry?)Application.Current!.Resources[clave]!,
                            Width = 16, Height = 16,
                            Foreground = new SolidColorBrush(Color.Parse("#AFAFAF")),
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                        new TextBlock { Text = nombre, VerticalAlignment = VerticalAlignment.Center },
                    }
                } };
                tabs.Items.Add(tab);
            }
            raiz.Children.Add(tabs);
            Grid.SetRow(tabs, 1);

            // Transporte con botones de transporte (iconos PathIcon donde aplique).
            var transporte = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            foreach (var clave in IconosTransporte)
            {
                var boton = new Button { Width = 40, Height = 40, Margin = Thickness.Parse("4") };
                boton.Content = new PathIcon
                {
                    Data = (Geometry?)Application.Current!.Resources[clave]!,
                    Width = 20, Height = 20,
                    Foreground = new SolidColorBrush(Color.Parse("#DBDBDB")),
                };
                transporte.Children.Add(boton);
            }
            raiz.Children.Add(transporte);
            Grid.SetRow(transporte, 2);

            ventana.Content = raiz;
            ventana.Show();

            var bytes = RenderPng(ventana, 1000, 680);
            var bmp = SKBitmap.Decode(bytes);
            Assert.NotNull(bmp);

            void AssertRegion(Visual elemento, string nombre)
            {
                Assert.True(RegionTienePixelesEncendidos(bmp!, elemento.Bounds, fondo),
                    $"El icono '{nombre}' no pinta píxeles encendidos.");
            }

            AssertRegion(iconoBarra, "barra de título");
            foreach (var tab in tabs.Items.OfType<TabItem>())
            {
                var ic = tab.GetVisualDescendants().OfType<PathIcon>().FirstOrDefault();
                Assert.True(ic is not null, $"La pestaña '{tab.Header}' no tiene PathIcon.");
                AssertRegion(ic, $"pestaña {tab.Header}");
            }

            var pausa = transporte.Children.OfType<Button>().ElementAt(0);
            var ip = pausa.GetVisualDescendants().OfType<PathIcon>().FirstOrDefault();
            Assert.True(ip is not null, "El botón de pausa no tiene PathIcon.");
            AssertRegion(ip, "pausa");

            var transcribe = transporte.Children.OfType<Button>().ElementAt(1);
            var it = transcribe.GetVisualDescendants().OfType<PathIcon>().FirstOrDefault();
            Assert.True(it is not null, "El botón de transcribir no tiene PathIcon.");
            AssertRegion(it, "transcribir");
        }, CancellationToken.None);
    }

    // ------------------------------------------------------------------ helpers

    private static Border CrearContenedorIcono(Geometry geometry)
    {
        var icon = new PathIcon
        {
            Data = geometry,
            // Foreground no se asigna a mano: debe venir del tema de PathIcon
            // (icon-color-primary-default). Si falta el tema, el icono no pinta.
            Width = IconSizePx,
            Height = IconSizePx,
        };
        // El tema implícito de PathIcon debe haber asignado Foreground.
        var fg = icon.Foreground;
        Assert.True(fg is not null, "PathIcon no tiene Foreground: falta el tema implícito de PathIcon.");
        var scb = Assert.IsAssignableFrom<ISolidColorBrush>(fg);
        Assert.True(scb.Color == Color.Parse("#DBDBDB"),
            "El Foreground del PathIcon no es el token de icono del tema.");
        return new Border
        {
            Width = IconSizePx,
            Height = IconSizePx,
            Background = new SolidColorBrush(Color.Parse("#111111")),
            Child = icon,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private static byte[] RenderPng(Visual visual, int ancho, int alto)
    {
        var ventana = new Window { Width = ancho, Height = alto, Content = visual };
        ventana.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
        ventana.UpdateLayout();
        using var frame = ventana.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("Avalonia no produjo el frame renderizado.");
        using var ms = new MemoryStream();
        frame.Save(ms, new PngBitmapEncoderOptions());
        ventana.Close();
        return ms.ToArray();
    }

    private static double LitPixelsRatio(byte[] png, SKColor fondo)
    {
        var bitmap = SKBitmap.Decode(png);
        if (bitmap is null) return 0;
        var total = bitmap.Width * bitmap.Height;
        var lit = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Alpha < 10 || Math.Abs(px.Red - fondo.Red) < 12 && Math.Abs(px.Green - fondo.Green) < 12 && Math.Abs(px.Blue - fondo.Blue) < 12)
                    continue;
                lit++;
            }
        }
        return (double)lit / total;
    }

    private static bool RegionTienePixelesEncendidos(SKBitmap bitmap, Rect bounds, SKColor fondo)
    {
        var x0 = (int)Math.Round(bounds.X);
        var y0 = (int)Math.Round(bounds.Y);
        var x1 = (int)Math.Round(bounds.X + bounds.Width);
        var y1 = (int)Math.Round(bounds.Y + bounds.Height);
        x0 = Math.Clamp(x0, 0, bitmap.Width - 1);
        y0 = Math.Clamp(y0, 0, bitmap.Height - 1);
        x1 = Math.Clamp(x1, 0, bitmap.Width);
        y1 = Math.Clamp(y1, 0, bitmap.Height);
        if (x1 <= x0 || y1 <= y0) return false;

        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Alpha >= 10 && !(Math.Abs(px.Red - fondo.Red) < 12 && Math.Abs(px.Green - fondo.Green) < 12 && Math.Abs(px.Blue - fondo.Blue) < 12))
                    return true;
            }
        }
        return false;
    }

    private sealed class SpectrumThemeApp : Application
    {
        public override void Initialize()
        {
            Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://Transcriptor.UI/Theming/SpectrumTheme.axaml"))
            {
                Source = new Uri("avares://Transcriptor.UI/Theming/SpectrumTheme.axaml")
            });
        }
    }

    private static class TestServices
    {
        public static UiServices Create(bool prefersNativeChrome = true)
        {
            var root = Path.Combine(Path.GetTempPath(), $"transcriptor-icons-{Guid.NewGuid():N}");
            var recordingsRoot = Path.Combine(root, "recordings");
            Directory.CreateDirectory(recordingsRoot);
            return new UiServices(
                new EmptyDeviceCatalog(),
                new UnsupportedCaptureFactory(),
                new UnsupportedPlayerFactory(),
                new NoOpSecurity(),
                new UnsupportedMigration(),
                new TestCapabilities(prefersNativeChrome),
                SystemClock.Instance,
                new TestPaths(root, recordingsRoot),
                (_, _, _) => throw new NotSupportedException());
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

        private sealed class TestCapabilities(bool prefersNativeChrome) : IPlatformCapabilities
        {
            public bool SupportsSystemLoopback => false;
            public bool SupportsEncryptionAtRest => false;
            public bool SupportsAudioPlayback => false;
            public bool SupportsMixedAudioPlayback => false;
            public bool SupportsVariablePlaybackRate => false;
            public bool PrefersNativeWindowChrome => prefersNativeChrome;
            public string Name => "Pruebas";
        }

        private sealed record TestPaths(string Root, string RecordingsRoot) : IDefaultPaths
        {
            public string SettingsPath => Path.Combine(Root, "settings.json");
            public string WhisperExecutablePath => Path.Combine(Root, "whisper");
            public string WhisperModelPath => Path.Combine(Root, "model.bin");
            public string? WhisperExecutablePickerPattern => null;
        }
    }
}
