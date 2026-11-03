using System.Windows;
using System.Windows.Media;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;

namespace Transcriptor.Desktop;

/// <summary>Paleta compartida y actualizable para la ventana principal y Mini-Mode.</summary>
internal static class ThemeManager
{
    private static readonly IReadOnlyDictionary<string, Color> Dark = new Dictionary<string, Color>
    {
        ["BgBrush"] = Color.FromRgb(0x14, 0x16, 0x1A), ["PanelBrush"] = Color.FromRgb(0x1C, 0x1F, 0x25),
        ["InputBrush"] = Color.FromRgb(0x0F, 0x11, 0x14), ["ControlBrush"] = Color.FromRgb(0x26, 0x2A, 0x32),
        ["ControlHoverBrush"] = Color.FromRgb(0x32, 0x38, 0x44), ["ControlPressedBrush"] = Color.FromRgb(0x3D, 0x44, 0x52),
        ["LineBrush"] = Color.FromRgb(0x35, 0x3A, 0x44), ["TextBrush"] = Color.FromRgb(0xD7, 0xDB, 0xE2),
        ["DimBrush"] = Color.FromRgb(0x8A, 0x92, 0xA0), ["LcdBgBrush"] = Color.FromRgb(0x07, 0x0B, 0x08),
        ["LcdBrush"] = Color.FromRgb(0x6D, 0xF2, 0xA0), ["LcdDimBrush"] = Color.FromRgb(0x2E, 0x7D, 0x4F),
        ["RecBrush"] = Color.FromRgb(0xFF, 0x4D, 0x5E), ["AccentBrush"] = Color.FromRgb(0xFF, 0xB0, 0x20),
        ["SelectionBrush"] = Color.FromRgb(0x1E, 0x4D, 0x33)
    };

    private static readonly IReadOnlyDictionary<string, Color> Light = new Dictionary<string, Color>
    {
        ["BgBrush"] = Color.FromRgb(0xF4, 0xF6, 0xF8), ["PanelBrush"] = Colors.White,
        ["InputBrush"] = Colors.White, ["ControlBrush"] = Color.FromRgb(0xE8, 0xED, 0xF2),
        ["ControlHoverBrush"] = Color.FromRgb(0xDC, 0xE4, 0xEC), ["ControlPressedBrush"] = Color.FromRgb(0xCB, 0xD5, 0xE1),
        ["LineBrush"] = Color.FromRgb(0xBC, 0xC6, 0xD1), ["TextBrush"] = Color.FromRgb(0x17, 0x21, 0x2B),
        ["DimBrush"] = Color.FromRgb(0x53, 0x61, 0x70), ["LcdBgBrush"] = Color.FromRgb(0xED, 0xF4, 0xEF),
        ["LcdBrush"] = Color.FromRgb(0x17, 0x6B, 0x3A), ["LcdDimBrush"] = Color.FromRgb(0x3F, 0x80, 0x57),
        ["RecBrush"] = Color.FromRgb(0xB4, 0x23, 0x32), ["AccentBrush"] = Color.FromRgb(0x8A, 0x4B, 0x00),
        ["SelectionBrush"] = Color.FromRgb(0xD7, 0xEB, 0xDD)
    };

    internal static string CurrentMode { get; private set; } = "Dark";

    internal static void Apply(Application application, string? mode)
    {
        ArgumentNullException.ThrowIfNull(application);
        CurrentMode = mode is "Dark" or "Light" or "HighContrast" or "System" ? mode : "Dark";
        var palette = SystemParameters.HighContrast || CurrentMode == "HighContrast"
            ? HighContrast : CurrentMode == "Light" ? Light : Dark;
        foreach (var item in palette)
        {
            if (application.Resources[item.Key] is SolidColorBrush brush && !brush.IsFrozen)
                brush.Color = item.Value;
            else
                application.Resources[item.Key] = new SolidColorBrush(item.Value);
        }
    }

    private static readonly IReadOnlyDictionary<string, Color> HighContrast = new Dictionary<string, Color>
    {
        ["BgBrush"] = SystemColors.WindowColor, ["PanelBrush"] = SystemColors.WindowColor,
        ["InputBrush"] = SystemColors.WindowColor, ["ControlBrush"] = SystemColors.ControlColor,
        ["ControlHoverBrush"] = SystemColors.ControlColor, ["ControlPressedBrush"] = SystemColors.ControlColor,
        ["LineBrush"] = SystemColors.WindowTextColor, ["TextBrush"] = SystemColors.WindowTextColor,
        ["DimBrush"] = SystemColors.GrayTextColor, ["LcdBgBrush"] = SystemColors.WindowColor,
        ["LcdBrush"] = SystemColors.WindowTextColor, ["LcdDimBrush"] = SystemColors.WindowTextColor,
        ["RecBrush"] = SystemColors.HighlightColor, ["AccentBrush"] = SystemColors.HighlightColor,
        ["SelectionBrush"] = SystemColors.ControlColor
    };
}
