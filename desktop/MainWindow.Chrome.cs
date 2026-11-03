using System.Windows;
using System.Windows.Input;
using Transcriptor.Transcription;

namespace Transcriptor.Desktop;

/// <summary>Comportamiento de la ventana sin marco: arrastre, minimizar, cerrar y cajón de paneles.</summary>
public partial class MainWindow
{
    private const double DrawerMinHeight = 440;
    private const double DefaultDrawerHeight = 620;

    private double _drawerHeight;
    private bool _drawerOpen;

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

    // Cajón de paneles (lista, texto, registro, dispositivos y ajustes).
    // Cerrado: la ventana es una franja compacta que se ajusta a su contenido.
    // Abierto: se muestran las pestañas y la altura se puede ajustar a mano.
    private void Drawer_Changed(object sender, RoutedEventArgs e)
    {
        var open = DrawerToggle.IsChecked == true;
        if (open == _drawerOpen) return;
        _drawerOpen = open;

        var visibility = open ? Visibility.Visible : Visibility.Collapsed;
        TabArea.Visibility = visibility;
        FooterText.Visibility = visibility;

        if (open)
        {
            SizeToContent = SizeToContent.Manual;
            MinHeight = DrawerMinHeight;
            Height = _drawerHeight > 0 ? _drawerHeight : DefaultDrawerHeight;
        }
        else
        {
            _drawerHeight = ActualHeight;
            MinHeight = 0;
            SizeToContent = SizeToContent.Height;
        }
    }
}
