using System.Xml.Linq;
using System.Globalization;
using Xunit;

namespace Transcriptor.Tests;

public sealed class ThemeContrastTests
{
    private static readonly string[] TextSurfaces =
    [
        "AppBackground", "Surface", "SurfaceStrip", "SurfaceCard",
        "SurfaceRaised", "SurfaceItem", "SurfaceLibrary", "SurfaceViewer", "SurfaceFooter"
    ];

    [Theory]
    [InlineData("Default")]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void TextAndControlColorsMeetContrastRequirements(string variant)
    {
        var colors = LoadPalette(variant);

        foreach (var surface in TextSurfaces)
        {
            AssertContrast(colors, variant, "TextPrimary", surface, 4.5);
            AssertContrast(colors, variant, "TextMuted", surface, 4.5);
            AssertContrast(colors, variant, "TextSecondary", surface, 4.5);
        }

        AssertContrast(colors, variant, "TextStrong", "TitleBarBackground", 4.5);
        AssertContrast(colors, variant, "TitleBarText", "TitleBarBackground", 4.5);
        AssertContrast(colors, variant, "TitleBarMuted", "TitleBarBackground", 4.5);
        AssertContrast(colors, variant, "AccentText", "AccentSoft", 4.5);
        AssertContrast(colors, variant, "AccentText", "SurfaceCard", 4.5);
        AssertContrast(colors, variant, "Success", "SurfaceCard", 4.5);
        AssertContrast(colors, variant, "WarningGlyph", "WarningSurface", 4.5);
        AssertContrast(colors, variant, "BadgeNeutralForeground", "BadgeNeutralBackground", 4.5);
        AssertContrast(colors, variant, "BadgeOkForeground", "BadgeOkBackground", 4.5, "SurfaceCard");
        AssertContrast(colors, variant, "BadgeWarnForeground", "BadgeWarnBackground", 4.5, "SurfaceCard");
        AssertContrast(colors, variant, "BadgeAccentForeground", "BadgeAccentBackground", 4.5, "SurfaceCard");

        AssertContrast(colors, variant, "Danger", "SurfaceCard", 3);
        AssertContrast(colors, variant, "SuccessDot", "SurfaceCard", 3);
        AssertContrast(colors, variant, "AccentGlyph", "SurfaceCard", 3);
        AssertContrast(colors, variant, "WaveformSystem", "LedScreen", 3);
        AssertContrast(colors, variant, "ControlStroke", "AppBackground", 3);
        AssertContrast(colors, variant, "ControlStroke", "SurfaceCard", 3);
    }

    private static Dictionary<string, string> LoadPalette(string variant)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Theming", "Tokens.axaml");
        var document = XDocument.Load(path);
        XNamespace avalonia = "https://github.com/avaloniaui";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var palette = document.Root!.Elements(avalonia + "SolidColorBrush")
            .ToDictionary(
                element => element.Attribute(x + "Key")?.Value
                    ?? throw new InvalidDataException("A SolidColorBrush is missing x:Key."),
                element => element.Value);
        var theme = document.Root.Element(avalonia + "ResourceDictionary.ThemeDictionaries")!
            .Elements(avalonia + "ResourceDictionary")
            .Single(element => (string?)element.Attribute(x + "Key") == variant);
        foreach (var brush in theme.Elements(avalonia + "SolidColorBrush"))
            palette[brush.Attribute(x + "Key")?.Value
                ?? throw new InvalidDataException("A theme brush is missing x:Key.")] = brush.Value;
        return palette;
    }

    private static void AssertContrast(
        IReadOnlyDictionary<string, string> colors,
        string variant,
        string foregroundKey,
        string backgroundKey,
        double minimum,
        string? canvasKey = null)
    {
        var foreground = Parse(colors[foregroundKey]);
        var background = Parse(colors[backgroundKey]);
        if (canvasKey is not null)
            background = Composite(background, Parse(colors[canvasKey]));
        var ratio = ContrastRatio(foreground, background);
        Assert.True(ratio >= minimum,
            $"{variant}: {foregroundKey} on {backgroundKey} has {ratio:F2}:1; expected at least {minimum:F1}:1.");
    }

    private static Rgb Parse(string hex)
    {
        var value = hex.AsSpan(1);
        if (value.Length == 8)
            return new Rgb(
                byte.Parse(value[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value[6..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d);
        return new Rgb(
            byte.Parse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static Rgb Composite(Rgb foreground, Rgb background) => new(
        foreground.R * foreground.Alpha + background.R * (1 - foreground.Alpha),
        foreground.G * foreground.Alpha + background.G * (1 - foreground.Alpha),
        foreground.B * foreground.Alpha + background.B * (1 - foreground.Alpha));

    private static double ContrastRatio(Rgb foreground, Rgb background)
    {
        var foregroundLuminance = Luminance(Composite(foreground, background));
        var backgroundLuminance = Luminance(background);
        var lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(Rgb color) =>
        0.2126 * Linearize(color.R / 255)
        + 0.7152 * Linearize(color.G / 255)
        + 0.0722 * Linearize(color.B / 255);

    private static double Linearize(double channel) =>
        channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

    private readonly record struct Rgb(double R, double G, double B, double Alpha = 1);
}
