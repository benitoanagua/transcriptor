using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Transcriptor.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ApplyTheme(new SettingsStore().Load().Settings.ThemeMode);
    }

    internal void ApplyTheme(string? mode)
    {
        RequestedThemeVariant = mode switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            // CI invokes this hook to verify Avalonia startup; the process exits successfully after two seconds.
            if (Environment.GetCommandLineArgs().Any(arg =>
                string.Equals(arg, "--smoke-test", StringComparison.OrdinalIgnoreCase)))
            {
                var smokeTimer = new Avalonia.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                smokeTimer.Tick += (_, _) =>
                {
                    smokeTimer.Stop();
                    desktop.Shutdown(0);
                };
                smokeTimer.Start();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
