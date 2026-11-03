using System.ComponentModel;
using System.Windows;

namespace Transcriptor.Desktop;

public partial class App : Application
{
    public App()
    {
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
    }

    private static void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if ((e.PropertyName is nameof(SystemParameters.HighContrast) or null) && Current is not null)
            ThemeManager.Apply(Current, ThemeManager.CurrentMode);
    }
}
