using System.Windows;
using System.Windows.Input;
using Transcriptor.Transcription;

namespace Transcriptor.Desktop;

/// <summary>Comportamiento de la ventana sin marco: arrastre, minimizar, cerrar y modo compacto.</summary>
public partial class MainWindow
{
    private const double ExpandedMinHeight = 560;
    private const double DefaultExpandedHeight = 700;

    private double _expandedHeight;
    private bool _compact;

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ModeText.Text = PortableMode.IsPortable ? "PORTABLE" : string.Empty;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        try { DragMove(); }
        catch (InvalidOperationException) { /* El botón se soltó antes de iniciar el arrastre. */ }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    // El cierre pasa por OnClosing, que espera a que termine la captura en curso.
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Compact_Changed(object sender, RoutedEventArgs e)
    {
        var compact = MiniToggle.IsChecked == true;
        if (compact == _compact) return;
        _compact = compact;

        var visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        SessionPanel.Visibility = visibility;
        TabArea.Visibility = visibility;
        FooterText.Visibility = visibility;

        if (compact)
        {
            _expandedHeight = ActualHeight;
            MinHeight = 0;
            SizeToContent = SizeToContent.Height;
        }
        else
        {
            SizeToContent = SizeToContent.Manual;
            MinHeight = ExpandedMinHeight;
            Height = _expandedHeight > 0 ? _expandedHeight : DefaultExpandedHeight;
        }
    }
}
