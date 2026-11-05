using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Transcriptor.UI.ViewModels;

namespace Transcriptor.UI;

public partial class MainWindow : Window, IUiHost
{
    private readonly MainViewModel _viewModel;

    public MainWindow(UiServices services)
    {
        _viewModel = new MainViewModel(services, this);
        InitializeComponent();
        DataContext = _viewModel;

        // El espacio de trabajo abre siempre con una pestaña visible: si el
        // índice persistido quedó oculto, se selecciona la primera visible.
        Opened += (_, _) =>
        {
            _viewModel.RefreshDevicesCommand.Execute(null);
            _viewModel.RefreshLibraryCommand.Execute(null);
        };
    }

    private void InitializeComponent()
    {
        NameScope.SetNameScope(this, new NameScope());
        AvaloniaXamlLoader.Load(this);
    }

    // ------------------------------------------------------------------ cromo de la ventana
    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Button) return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    private void MinimizeWindow_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeWindow_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseWindow_Click(object? sender, RoutedEventArgs e) => Close();

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
