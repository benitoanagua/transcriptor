using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Transcriptor.UI.Spectrum;
using Transcriptor.UI.ViewModels;

namespace Transcriptor.UI;

public partial class MainWindow : Window, IUiHost
{
    private const double WindowInitialWidth = 768;
    private const double WindowInitialHeight = 780;
    private const double WindowMinimumWidth = 520;
    private const double WindowMinimumHeight = 700;

    private readonly MainViewModel _viewModel;
    private readonly bool _prefersNativeChrome;
    private readonly DispatcherTimer _uiTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(6) };
    private bool _closeConfirmed;
    private bool _closeDecisionPending;

    /// <summary>
    /// Uso interno de captura/CI: cierra la ventana sin pedir confirmación.
    /// El valor por defecto es false; la app de producción y el modo normal de
    /// desktop-dev no lo alteran.
    /// </summary>
    public bool SkipCloseConfirmation { get; set; }

    public MainWindow(UiServices services)
    {
        _viewModel = new MainViewModel(services, this, action => Dispatcher.UIThread.Post(action));
        // La barra de título propia siempre se muestra (cromo personalizado al estilo Stitch).
        // BorderOnly elimina el marco nativo del SO para que no aparezca una segunda barra de título.
        // ExtendClientAreaToDecorationsHint solo se activa en cabezas que no usan marco nativo;
        // en modo desarrollo/pruebas (marco nativo) la barra propia sigue siendo el contenido
        // superior y es arrastrable mediante TitleBar_PointerPressed.
        _prefersNativeChrome = services.Capabilities.PrefersNativeWindowChrome;
        WindowDecorations = WindowDecorations.BorderOnly;
        InitializeComponent();
        Width = WindowInitialWidth;
        MinWidth = WindowMinimumWidth;
        Height = WindowInitialHeight;
        MinHeight = WindowMinimumHeight;
        ExtendClientAreaToDecorationsHint = !_prefersNativeChrome;
        DataContext = _viewModel;
        var titleBar = this.FindControl<Border>("TitleBar")!;
        titleBar.IsVisible = true;
        if (!_prefersNativeChrome)
        {
            WindowDecorationProperties.SetElementRole(
                this.FindControl<Border>("TitleBar")!, WindowDecorationsElementRole.TitleBar);
        }
        WindowDecorationProperties.SetElementRole(
            this.FindControl<Button>("MinimizeButton")!, WindowDecorationsElementRole.MinimizeButton);
        WindowDecorationProperties.SetElementRole(
            this.FindControl<Button>("CloseButton")!, WindowDecorationsElementRole.CloseButton);
        WindowDecorationProperties.SetElementRole(
            this.FindControl<TabItem>("StudioTab")!, WindowDecorationsElementRole.User);
        WindowDecorationProperties.SetElementRole(
            this.FindControl<TabItem>("LibraryTab")!, WindowDecorationsElementRole.User);
        WindowDecorationProperties.SetElementRole(
            this.FindControl<TabItem>("SettingsTab")!, WindowDecorationsElementRole.User);
        WindowDecorationProperties.SetElementRole(
            this.FindControl<TabItem>("LogTab")!, WindowDecorationsElementRole.User);

        _uiTimer.Tick += (_, _) => _viewModel.OnTimerTick();
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            SetSavedSessionToastVisible(false);
        };
        _viewModel.SessionSaved += () =>
        {
            SetSavedSessionToastVisible(true);
            _toastTimer.Stop();
            _toastTimer.Start();
        };
        Closing += OnWindowClosing;

        // Autoscroll del registro: al cambiar el texto, el caret salta al final
        // y el ScrollViewer lo trae a la vista; el usuario sigue pegado al final.
        _viewModel.Log.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(LogViewModel.LogText)) return;
            var logBox = this.FindControl<TextBox>("LogBox");
            if (logBox is { } box) box.CaretIndex = box.Text?.Length ?? 0;
        };

        // El espacio de trabajo siempre abre en Estudio y actualiza sus datos al entrar.
        Opened += (_, _) =>
        {
            _viewModel.Recorder.RefreshDevicesCommand.Execute(null);
            _viewModel.Library.RefreshLibraryCommand.Execute(null);
            _uiTimer.Start();
        };
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

        if (e.Key == Key.F5)
        {
            _viewModel.Library.RefreshLibraryCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _viewModel.DevelopmentBannerVisible)
        {
            _viewModel.DismissDevelopmentBannerCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Space && _viewModel.IsLibraryPage && !IsSpaceOwnedByControl(e.Source))
        {
            _viewModel.Detail.TogglePlaybackCommand.Execute(null);
            e.Handled = true;
        }
    }

    private static bool IsSpaceOwnedByControl(object? source)
    {
        if (source is not Control control) return false;
        return control.FindAncestorOfType<TextBox>() is not null
            || control.FindAncestorOfType<ComboBox>() is not null
            || control.FindAncestorOfType<Button>() is not null
            || control.FindAncestorOfType<CheckBox>() is not null
            || control.FindAncestorOfType<Slider>() is not null;
    }

    private void SessionAction_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: ViewModels.LibraryEntry entry, Tag: string action })
            return;

        switch (action)
        {
            case "play":
                _viewModel.PlayMicrophoneEntryCommand.Execute(entry);
                break;
            case "transcribe":
                _viewModel.TranscribeEntryCommand.Execute(entry);
                break;
            case "delete":
                _viewModel.DeleteEntryCommand.Execute(entry);
                break;
        }
    }

    private void TranscriptSegments_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox)
            _viewModel.Transcript.SetSelectedSegments(
                listBox.SelectedItems?.Cast<object?>() ?? Enumerable.Empty<object?>());
    }

    // ------------------------------------------------------------------ cromo de la ventana
    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Los clics que nacen dentro de un botón (o de su contenido interno)
        // no deben iniciar arrastre: se recorren ancestros visuales.
        if (e.Source is Visual source
            && (source is Button
                || source.FindAncestorOfType<Button>() is not null
                || source is ComboBox
                || source.FindAncestorOfType<ComboBox>() is not null))
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.ClickCount > 1) return;
        BeginMoveDrag(e);
    }

    private void MinimizeWindow_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseWindow_Click(object? sender, RoutedEventArgs e) => Close();

    private void SetSavedSessionToastVisible(bool visible)
    {
        if (this.FindControl<SpectrumToast>("SavedSessionToast") is { } toast) toast.IsVisible = visible;
    }

    private void SavedSessionToast_VerEnBiblioteca_Click(object? sender, RoutedEventArgs e)
    {
        _toastTimer.Stop();
        SetSavedSessionToastVisible(false);
        _viewModel.ShowLibraryPageCommand.Execute(null);
    }

    // ------------------------------------------------------------------ cierre seguro
    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed || SkipCloseConfirmation) return;
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
    public async Task<bool> ConfirmAsync(string title, string message, string confirmLabel, bool destructive = false)
    {
        var popup = this.FindControl<Popup>("ConfirmationPopup")!;
        var titleBlock = this.FindControl<TextBlock>("ConfirmationTitle")!;
        var messageBlock = this.FindControl<TextBlock>("ConfirmationMessage")!;
        var confirmBtn = this.FindControl<Button>("ConfirmationConfirm")!;
        var cancelBtn = this.FindControl<Button>("ConfirmationCancel")!;

        titleBlock.Text = title;
        messageBlock.Text = message;
        confirmBtn.Content = confirmLabel;
        confirmBtn.Classes.Clear();
        if (destructive) confirmBtn.Classes.Add("negative");
        else confirmBtn.Classes.Remove("negative");

        var tcs = new TaskCompletionSource<bool>();
        void Confirm(object? s, RoutedEventArgs e) { popup.IsOpen = false; tcs.TrySetResult(true); }
        void Cancel(object? s, RoutedEventArgs e) { popup.IsOpen = false; tcs.TrySetResult(false); }
        confirmBtn.Click += Confirm;
        cancelBtn.Click += Cancel;
        try
        {
            popup.IsOpen = true;
            return await tcs.Task;
        }
        finally
        {
            confirmBtn.Click -= Confirm;
            cancelBtn.Click -= Cancel;
            popup.IsOpen = false;
        }
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
            return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = pattern is null
                ? null
                : [new FilePickerFileType(title) { Patterns = [pattern] }]
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }
}
