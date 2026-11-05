using System.Globalization;
using System.Xml.Linq;
using Xunit;

namespace Transcriptor.Tests;

public sealed class ThemeContrastTests
{
    private static readonly string[] TextSurfaces =
    [
        "background-base-color",
        "background-layer-1-color",
        "background-elevated-color",
        "background-layer-2-color",
        "gray-75",
        "gray-100",
        "gray-200"
    ];

    [Fact]
    public void DarkPaletteMeetsTextAndComponentContrastRequirements()
    {
        var colors = LoadPalette();

        foreach (var surface in TextSurfaces)
        {
            AssertContrast(colors, "gray-900", surface, 4.5);
            AssertContrast(colors, "gray-700", surface, 4.5);
        }

        foreach (var surface in new[]
        {
            "positive-subtle-background-color-default",
            "notice-subtle-background-color-default",
            "negative-subtle-background-color-default",
            "accent-subtle-background-color-default",
            "neutral-subtle-background-color-default"
        })
            AssertContrast(colors, "gray-900", surface, 4.5);

        foreach (var surface in new[]
        {
            "background-base-color",
            "background-layer-1-color",
            "background-elevated-color",
            "gray-100",
            "gray-200"
        })
        {
            AssertContrast(colors, "gray-600", surface, 3);
            AssertContrast(colors, "blue-900", surface, 3);
            AssertContrast(colors, "positive-color-900", surface, 3);
            AssertContrast(colors, "negative-content-color-default", surface, 3);
        }

        AssertContrast(colors, "notice-color-1000", "notice-subtle-background-color-default", 3);
        AssertContrast(colors, "gray-1000", "accent-background-color-default", 4.5);
        AssertContrast(colors, "focus-indicator-color", "background-base-color", 3);
    }

    private static Dictionary<string, string> LoadPalette()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Theming", "Tokens.axaml");
        var document = XDocument.Load(path);
        XNamespace avalonia = "https://github.com/avaloniaui";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return document.Root!.Elements(avalonia + "SolidColorBrush")
            .ToDictionary(
                element => element.Attribute(x + "Key")?.Value
                    ?? throw new InvalidDataException("A SolidColorBrush is missing x:Key."),
                element => element.Value);
    }

    private static void AssertContrast(
        IReadOnlyDictionary<string, string> colors,
        string foregroundKey,
        string backgroundKey,
        double minimum)
    {
        var ratio = ContrastRatio(Parse(colors[foregroundKey]), Parse(colors[backgroundKey]));
        Assert.True(ratio >= minimum,
            $"{foregroundKey} on {backgroundKey} has {ratio:F2}:1; expected at least {minimum:F1}:1.");
    }

    private static Rgb Parse(string hex)
    {
        var value = hex.AsSpan(1);
        return new Rgb(
            byte.Parse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static double ContrastRatio(Rgb foreground, Rgb background)
    {
        var foregroundLuminance = Luminance(foreground);
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

    private readonly record struct Rgb(double R, double G, double B);
}
