using WpfMessageBox = System.Windows.MessageBox;
using WpfSystemColors = System.Windows.SystemColors;
using System.Globalization;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;

namespace Transcriptor.Desktop;

public partial class MiniModeWindow : Window
{
    private readonly MainWindow _main;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private bool _allowClose;
    private bool _initializing = true;
    private bool _showingTranscription;

    public MiniModeWindow(MainWindow main)
    {
        _main = main ?? throw new ArgumentNullException(nameof(main));
        InitializeComponent();
        Topmost = main.MiniModeAlwaysOnTop;
        TopmostToggle.IsChecked = Topmost;
        if (main.MiniModeWidth is double width && width >= MinWidth)
            Width = width;
        if (main.MiniModeHeight is double height && height >= MinHeight)
            Height = height;
        if (main.MiniModeLeft is double left && main.MiniModeTop is double top)
        {
            Left = left;
            Top = top;
        }
        else
        {
            Left = Math.Max(0, SystemParameters.WorkArea.Right - Width - 24);
            Top = Math.Max(0, SystemParameters.WorkArea.Bottom - Height - 24);
        }

        _initializing = false;
        _timer.Tick += (_, _) => RefreshSnapshot();
    }

    private void ApplyHighContrast()
    {
        Background = WpfSystemColors.WindowBrush;
        Foreground = WpfSystemColors.WindowTextBrush;
        RootBorder.BorderBrush = WpfSystemColors.ActiveBorderBrush;
        foreach (var control in FindVisualChildren<System.Windows.Controls.Control>(this))
        {
            control.Foreground = WpfSystemColors.ControlTextBrush;
            control.Background = WpfSystemColors.ControlBrush;
            control.BorderBrush = WpfSystemColors.ActiveBorderBrush;
        }
        foreach (var text in FindVisualChildren<System.Windows.Controls.TextBlock>(this))
            text.Foreground = WpfSystemColors.WindowTextBrush;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is T typed) yield return typed;
            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }

    private void MiniWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_initializing) return;
        ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        var ultraCompact = ActualWidth < 340 || ActualHeight < 100;
        RootBorder.Padding = ultraCompact ? new Thickness(0) : new Thickness(10);
        ElapsedText.Visibility = ultraCompact ? Visibility.Collapsed : Visibility.Visible;
        TopmostToggle.Visibility = ultraCompact ? Visibility.Collapsed : Visibility.Visible;
        ExpandButton.Visibility = ultraCompact ? Visibility.Collapsed : Visibility.Visible;
        CloseMiniButton.Visibility = ultraCompact ? Visibility.Collapsed : Visibility.Visible;
        CompactPauseButton.Visibility = ultraCompact && !_showingTranscription
            ? Visibility.Visible : Visibility.Collapsed;
        EncryptionCompactText.Visibility = ultraCompact && !_showingTranscription
            ? Visibility.Visible : Visibility.Collapsed;

        if (ultraCompact)
        {
            RecordingPanel.Visibility = Visibility.Collapsed;
            RecordingControls.Visibility = Visibility.Collapsed;
            TranscriptionPanel.Visibility = Visibility.Collapsed;
            StateText.FontSize = 12;
        }
        else
        {
            StateText.FontSize = 14;
            RecordingPanel.Visibility = _showingTranscription ? Visibility.Collapsed : Visibility.Visible;
            RecordingControls.Visibility = _showingTranscription ? Visibility.Collapsed : Visibility.Visible;
            TranscriptionPanel.Visibility = _showingTranscription ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void PresetSize_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem { Tag: string dimensions }) return;
        var parts = dimensions.Split(',');
        if (parts.Length != 2
            || !double.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var width)
            || !double.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var height))
            return;
        Width = width;
        Height = height;
        _main.UpdateMiniModePreferences(Topmost, Left, Top, Width, Height);
    }

    private void Root_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (RootBorder.ContextMenu?.Items.OfType<System.Windows.Controls.MenuItem>()
            .FirstOrDefault(item => Equals(item.Header, "Mantener siempre visible")) is { } menuItem)
            menuItem.IsChecked = Topmost;
    }

    private void ContextTopmost_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem menuItem)
            Topmost = menuItem.IsChecked;
        TopmostToggle.IsChecked = Topmost;
        _main.UpdateMiniModePreferences(Topmost, Left, Top, Width, Height);
    }

    private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase)
            return;
        if (e.ClickCount >= 2)
        {
            Height = ActualHeight < 100 ? 130 : 64;
            e.Handled = true;
            return;
        }

        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
        {
            try { DragMove(); }
            catch (InvalidOperationException) { }
        }
    }

    private void MiniWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (SystemParameters.HighContrast)
            ApplyHighContrast();
        _timer.Start();
        RefreshSnapshot();
    }

    private void RefreshSnapshot()
    {
        var snapshot = _main.GetMiniModeSnapshot();
        if (snapshot is null)
        {
            _timer.Stop();
            _main.CloseMiniModeFromWindow(this);
            return;
        }

        if (snapshot.IsTranscribing)
        {
            _showingTranscription = true;
            ApplyResponsiveLayout();
            StateText.Text = "⏳ TRANSCRIBIENDO";
            StateText.Foreground = System.Windows.Media.Brushes.Orange;
            ElapsedText.Text = string.Empty;
            TranscriptionText.Text = snapshot.Status;
            return;
        }

        _showingTranscription = false;
        ApplyResponsiveLayout();
        StateText.Text = snapshot.IsError ? "⚠ ERROR" : snapshot.IsPaused ? "Ⅱ PAUSADO" : "● REC";
        StateText.Foreground = SystemParameters.HighContrast
            ? WpfSystemColors.WindowTextBrush
            : snapshot.IsError
                ? System.Windows.Media.Brushes.OrangeRed
                : snapshot.IsPaused
                    ? System.Windows.Media.Brushes.Orange
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 17, 35));
        var totalHours = (long)snapshot.Elapsed.TotalHours;
        ElapsedText.Text = $"{totalHours:D2}:{snapshot.Elapsed.Minutes:D2}:{snapshot.Elapsed.Seconds:D2}";
        MicLevel.Value = snapshot.MicrophoneLevel * 100;
        SystemLevel.Value = snapshot.SystemLevel * 100;
        MicLevelText.Text = snapshot.MicrophoneLevel.ToString("P0");
        SystemLevelText.Text = snapshot.SystemLevel.ToString("P0");
        PauseButton.Content = snapshot.IsPaused ? "▶ Reanudar" : "Ⅱ Pausar";
        PauseButton.IsEnabled = !snapshot.IsError;
        SessionText.Text = snapshot.IsError ? snapshot.Status : snapshot.SessionTitle;
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (_main.GetMiniModeSnapshot()?.IsPaused == true)
            _main.ResumeRecordingFromMiniMode();
        else
            _main.PauseRecordingFromMiniMode();
        RefreshSnapshot();
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        var answer = WpfMessageBox.Show(
            "Se detendrán de forma coordinada las pistas Yo y Cliente. ¿Deseas finalizar la grabación?",
            "Detener grabación", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        PauseButton.IsEnabled = false;
        StopButton.IsEnabled = false;
        await _main.StopRecordingFromMiniModeAsync();
    }

    private void Topmost_Changed(object sender, RoutedEventArgs e)
    {
        Topmost = TopmostToggle.IsChecked == true;
        if (_initializing) return;
        _main.UpdateMiniModePreferences(Topmost, Left, Top, Width, Height);
    }

    private void CancelTranscription_Click(object sender, RoutedEventArgs e)
    {
        CancelTranscriptionButton.IsEnabled = false;
        TranscriptionText.Text = "Cancelando transcripción…";
        _main.CancelTranscriptionFromMiniMode();
    }

    private void Expand_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        _main.Show();
        _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    private void CloseMini_Click(object sender, RoutedEventArgs e) => Close();

    private void MiniWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        _timer.Stop();
        _main.UpdateMiniModePreferences(Topmost, Left, Top, Width, Height);
        Hide();
        _main.Show();
        _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    public void CloseFromMain()
    {
        _allowClose = true;
        _timer.Stop();
        _main.UpdateMiniModePreferences(Topmost, Left, Top, Width, Height);
        Close();
    }
}
