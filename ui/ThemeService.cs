using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Transcriptor.UI;

/// <summary>
/// Único punto de aplicación del tema para todas las cabezas. Sustituye a las
/// copias idénticas que antes tenían desktop-avalonia y desktop-dev; la
/// decisión (Sistema/Claro/Oscuro) viaja por la misma firma
/// <c>Action&lt;string?&gt;</c> que registra <see cref="UiServices"/>.
/// </summary>
public static class ThemeService
{
    /// <summary>Aplica el modo conmutando la variante de tema de la aplicación:
    /// System/valor nulo → <see cref="ThemeVariant.Default"/> (sigue al SO),
    /// y Claro/Oscuro fijan la variante correspondiente.</summary>
    public static void Apply(string? mode)
    {
        if (Application.Current is not { } current) return;
        current.RequestedThemeVariant = mode switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
