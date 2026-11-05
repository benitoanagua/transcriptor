using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Diagnostics;
using Transcriptor.UI.ViewModels;

namespace Transcriptor.UI;

public partial class MainWindow : Window, IUiHost
{
    private readonly MainViewModel _viewModel;
    private readonly bool _prefersNativeChrome;
    private readonly DispatcherTimer _uiTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private bool _closeConfirmed;
    private bool _closeDecisionPending;

    public MainWindow(UiServices services)
    {
        _viewModel = new MainViewModel(services, this, action => Dispatcher.UIThread.Post(action));
        // La cabeza elige el marco nativo o el modo compacto sin #if en la vista.
        _prefersNativeChrome = services.Capabilities.PrefersNativeWindowChrome;
        WindowDecorations = _prefersNativeChrome ? WindowDecorations.Full : WindowDecorations.BorderOnly;
        InitializeComponent();
        DataContext = _viewModel;
        this.FindControl<StackPanel>("WindowButtons")!.IsVisible = !_prefersNativeChrome;
        _uiTimer.Tick += (_, _) => _viewModel.OnTimerTick();
        Closing += OnWindowClosing;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty) ApplyWindowStateChrome();
        };

        // Autoscroll del registro: al cambiar el texto, el caret salta al final
        // y el ScrollViewer lo trae a la vista; el usuario sigue pegado al final.
        _viewModel.Log.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(LogViewModel.LogText)) return;
            var logBox = this.FindControl<TextBox>("LogBox");
            if (logBox is { } box) box.CaretIndex = box.Text?.Length ?? 0;
        };

        // El espacio de trabajo abre siempre con una pestaña visible: si el
        // índice persistido quedó oculto, se selecciona la primera visible.
        Opened += (_, _) =>
        {
            ApplyWindowStateChrome();
            _viewModel.Recorder.RefreshDevicesCommand.Execute(null);
            _viewModel.Library.RefreshLibraryCommand.Execute(null);
            _uiTimer.Start();
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;

        var controlPressed = (e.KeyModifiers & KeyModifiers.Control) != 0;
        if (controlPressed)
        {
            switch (e.Key)
            {
                case Key.R:
                    _viewModel.Recorder.StartRecordingCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.P:
                    if (_viewModel.Recorder.IsRecordingActive)
                        _viewModel.Recorder.PauseRecordingCommand.Execute(null);
                    else if (_viewModel.IsLibraryPage)
                        _viewModel.Detail.TogglePlaybackCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.F:
                    _viewModel.ShowLibraryPageCommand.Execute(null);
                    this.FindControl<TextBox>("LibrarySearchBox")?.Focus();
                    e.Handled = true;
                    return;
                case Key.D1:
                case Key.NumPad1:
                    _viewModel.ShowRecordingPageCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.D2:
                case Key.NumPad2:
                    _viewModel.ShowLibraryPageCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.D3:
                case Key.NumPad3:
                    _viewModel.ShowSettingsPageCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.D4:
                case Key.NumPad4:
                    _viewModel.ShowLogPageCommand.Execute(null);
                    e.Handled = true;
                    return;
            }
        }

        if (e.Key == Key.F5)
        {
            _viewModel.Library.RefreshLibraryCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Space && _viewModel.IsLibraryPage && !IsSpaceOwnedByControl(e.Source))
        {
            _viewModel.Detail.TogglePlaybackCommand.Execute(null);
            e.Handled = true;
        }
    }

    private bool IsSpaceOwnedByControl(object? source)
    {
        if (source is not Control control) return false;
        return control.FindAncestorOfType<TextBox>() is not null
            || control.FindAncestorOfType<ComboBox>() is not null
            || control.FindAncestorOfType<Button>() is not null
            || control.FindAncestorOfType<CheckBox>() is not null
            || control.FindAncestorOfType<Slider>() is not null;
    }

    // ------------------------------------------------------------------ cromo de la ventana
    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Los clics que nacen dentro de un botón (o de su contenido interno)
        // no deben iniciar arrastre: se recorren ancestros visuales.
        if (e.Source is Visual source && (source is Button || source.FindAncestorOfType<Button>() is not null)) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.ClickCount == 2)
        {
            WindowState = WindowState is WindowState.Maximized or WindowState.FullScreen
                ? WindowState.Normal
                : WindowState.Maximized;
            return;
        }
        BeginMoveDrag(e);
    }

    private void MinimizeWindow_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeWindow_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseWindow_Click(object? sender, RoutedEventArgs e) => Close();

    // ------------------------------------------------------------------ cierre seguro
    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed) return;
        e.Cancel = true;
        if (_closeDecisionPending) return;
        _closeDecisionPending = true;
        try
        {
            if (!await _viewModel.ConfirmCloseAsync()) return;
            _closeConfirmed = true;
            Close();
        }
        finally
        {
            _closeDecisionPending = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _uiTimer.Stop();
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

    public async Task<string?> PickSaveFileAsync(string title, string pattern)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null) return null;
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            DefaultExtension = Path.GetExtension(pattern).TrimStart('.'),
            FileTypeChoices = [new FilePickerFileType(title) { Patterns = [pattern] }]
        });
        return file?.TryGetLocalPath();
    }

    public Task OpenFolderAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("No existe la carpeta de la sesión.");
        using var process = Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        if (process is null) throw new InvalidOperationException("El sistema no abrió la carpeta de la sesión.");
        return Task.CompletedTask;
    }

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
