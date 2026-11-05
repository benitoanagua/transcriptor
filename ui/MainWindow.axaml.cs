using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Transcriptor.UI.ViewModels;

namespace Transcriptor.UI;

public partial class MainWindow : Window, IUiHost
{
    private readonly MainViewModel _viewModel;
    private readonly bool _prefersNativeChrome;
    private bool _closeConfirmed;

    public MainWindow(UiServices services)
    {
        _viewModel = new MainViewModel(services, this);
        // Decoración decidida por la cabeza sin #if: Windows usa chrome compacto
        // propio con borde nativo de redimensionado; Linux/macOS pide chrome nativo.
        _prefersNativeChrome = services.Capabilities.PrefersNativeWindowChrome;
        WindowDecorations = _prefersNativeChrome ? WindowDecorations.Full : WindowDecorations.BorderOnly;
        InitializeComponent();
        DataContext = _viewModel;
        this.FindControl<StackPanel>("WindowButtons")!.IsVisible = !_prefersNativeChrome;
        Closing += OnWindowClosing;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty) ApplyWindowStateChrome();
        };

        // Autoscroll del registro: al cambiar el texto, el caret salta al final
        // y el ScrollViewer lo trae a la vista; el usuario sigue pegado al final.
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(MainViewModel.LogText)) return;
            var logBox = this.FindControl<TextBox>("LogBox");
            if (logBox is { } box) box.CaretIndex = box.Text?.Length ?? 0;
        };

        // El espacio de trabajo abre siempre con una pestaña visible: si el
        // índice persistido quedó oculto, se selecciona la primera visible.
        Opened += (_, _) =>
        {
            ApplyWindowStateChrome();
            _viewModel.RefreshDevicesCommand.Execute(null);
            _viewModel.RefreshLibraryCommand.Execute(null);
        };
    }

    private void ApplyWindowStateChrome()
    {
        var maximized = WindowState is WindowState.Maximized or WindowState.FullScreen;
        this.FindControl<Grid>("RootGrid")!.Margin = maximized ? new Thickness(0) : new Thickness(12);
        this.FindControl<Button>("MaximizeButton")!.Content = maximized ? "❐" : "□";
    }

    private void InitializeComponent()
    {
        NameScope.SetNameScope(this, new NameScope());
        AvaloniaXamlLoader.Load(this);
    }

    // ------------------------------------------------------------------ cromo de la ventana
    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Con decoración nativa el sistema ya maneja la ventana; el cabezal
        // propio solo actúa en chrome compacto (BorderOnly).
        if (_prefersNativeChrome) return;
        // Los clics que nacen dentro de un botón (o de su contenido interno)
        // no deben iniciar arrastre: se recorren ancestros visuales.
        if (e.Source is Visual source && (source is Button || source.FindAncestorOfType<Button>() is not null)) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }
        if (WindowState != WindowState.Maximized) BeginMoveDrag(e);
    }

    private void MinimizeWindow_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeWindow_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseWindow_Click(object? sender, RoutedEventArgs e) => Close();

    // ------------------------------------------------------------------ cierre seguro
    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed) return;
        e.Cancel = true;
        if (!await _viewModel.ConfirmCloseAsync()) return;
        _closeConfirmed = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.OnWindowClosed();
        base.OnClosed(e);
    }

    // ------------------------------------------------------------------ IUiHost: interacciones de vista
    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 430,
            SizeToContent = SizeToContent.Height,
            MinHeight = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(18),
                Spacing = 14,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 8,
                        Children =
                        {
                            new Button { Content = "Cancelar", MinWidth = 90 },
                            new Button { Content = "Confirmar", MinWidth = 90 }
                        }
                    }
                }
            }
        };
        var actions = (StackPanel)((StackPanel)dialog.Content!).Children[1];
        ((Button)actions.Children[0]).Click += (_, _) => dialog.Close(false);
        ((Button)actions.Children[1]).Click += (_, _) => dialog.Close(true);
        return await dialog.ShowDialog<bool>(this);
    }

    public async Task<string?> PickFolderAsync(string title) => await OpenPickerPathAsync(folder: true, title);

    public async Task<string?> PickFileAsync(string title, string? pattern) =>
        await OpenPickerPathAsync(folder: false, title, pattern);

    public async Task<bool> CopyToClipboardAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return false;
        // Avalonia 12: el portapapeles recibe un IAsyncDataTransfer. Puente
        // mínimo sobre DataTransferItem.SetText.
        await clipboard.SetDataAsync(new ClipboardTextTransfer(text));
        return true;
    }

    /// <summary>
    /// Adaptador IAsyncDataTransfer que expone un único elemento de texto
    /// sincrónico al portapapeles de Avalonia 12 (SetTextAsync ya no existe).
    /// </summary>
    private sealed class ClipboardTextTransfer(string text) : Avalonia.Input.IAsyncDataTransfer, IDisposable
    {
        private readonly Avalonia.Input.DataTransferItem _item = Avalonia.Input.DataTransferItem.CreateText(text);
        public IReadOnlyList<Avalonia.Input.DataFormat> Formats => _item.Formats;
        public IReadOnlyList<Avalonia.Input.IAsyncDataTransferItem> Items => [new Item(_item)];
        public void Dispose() { }

        private sealed class Item(Avalonia.Input.DataTransferItem inner) : Avalonia.Input.IAsyncDataTransferItem
        {
            public IReadOnlyList<Avalonia.Input.DataFormat> Formats => inner.Formats;
            public Task<object?> TryGetRawAsync(Avalonia.Input.DataFormat format)
                => Task.FromResult(inner.TryGetRaw(format));
        }
    }

    private async Task<string?> OpenPickerPathAsync(bool folder, string title, string? pattern = null)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null) return null;

        if (folder)
        {
            var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });
            return folders.FirstOrDefault()?.TryGetLocalPath();
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = pattern is null
                ? null
                : [new FilePickerFileType(title) { Patterns = [pattern] }]
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }
}
