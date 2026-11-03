using WpfMessageBox = System.Windows.MessageBox;
using WpfSystemColors = System.Windows.SystemColors;
using System.Globalization;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using FormsScreen = System.Windows.Forms.Screen;

namespace Transcriptor.Desktop;

public partial class MiniModeWindow : Window
{
    private readonly MainWindow _main;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly DispatcherTimer _boundsSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private bool _allowClose;
    private bool _initializing = true;
    private bool _showingTranscription;
    private bool _showingIdle;

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

        // A saved position may belong to a monitor that is no longer connected.
        RestoreBoundsToAvailableMonitor();

        _initializing = false;
        _timer.Tick += (_, _) => RefreshSnapshot();
        _boundsSaveTimer.Tick += (_, _) =>
        {
            _boundsSaveTimer.Stop();
            PersistBounds();
        };
    }

    private const int MonitorDefaultToNearest = 2;
    private const int EffectiveDpi = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, int flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    private static double GetMonitorScale(FormsScreen screen)
    {
        var bounds = screen.Bounds;
        var center = new NativePoint { X = bounds.Left + bounds.Width / 2, Y = bounds.Top + bounds.Height / 2 };
        var monitor = MonitorFromPoint(center, MonitorDefaultToNearest);
        if (monitor != IntPtr.Zero
            && GetDpiForMonitor(monitor, EffectiveDpi, out var dpiX, out _) == 0
            && dpiX >= 48 && dpiX <= 768)
            return dpiX / 96d;
        return 1d;
    }

    private void RestoreBoundsToAvailableMonitor()
    {
        var screens = FormsScreen.AllScreens;
        if (screens.Length == 0) return;

        FormsScreen? selected = null;
        var selectedScale = 1d;
        var bestOverlap = -1d;
        var bestDistance = double.MaxValue;

        foreach (var screen in screens)
        {
            var scale = GetMonitorScale(screen);
            var area = screen.WorkingArea;
            var candidateLeft = Left * scale;
            var candidateTop = Top * scale;
            var candidateWidth = Width * scale;
            var candidateHeight = Height * scale;
            var overlapWidth = Math.Max(0, Math.Min(candidateLeft + candidateWidth, area.Right) - Math.Max(candidateLeft, area.Left));
            var overlapHeight = Math.Max(0, Math.Min(candidateTop + candidateHeight, area.Bottom) - Math.Max(candidateTop, area.Top));
            var overlap = overlapWidth * overlapHeight;
            var centerX = candidateLeft + candidateWidth / 2;
            var centerY = candidateTop + candidateHeight / 2;
            var dx = centerX < area.Left ? area.Left - centerX : centerX > area.Right ? centerX - area.Right : 0;
            var dy = centerY < area.Top ? area.Top - centerY : centerY > area.Bottom ? centerY - area.Bottom : 0;
            var distance = dx * dx + dy * dy;

            if (overlap > bestOverlap || (Math.Abs(overlap - bestOverlap) < 0.5 && distance < bestDistance))
            {
                selected = screen;
                selectedScale = scale;
                bestOverlap = overlap;
                bestDistance = distance;
            }
        }

        if (selected is null) return;
        var work = selected.WorkingArea;
        var workLeft = work.Left / selectedScale;
        var workTop = work.Top / selectedScale;
        var workRight = work.Right / selectedScale;
        var workBottom = work.Bottom / selectedScale;
        var availableWidth = Math.Max(MinWidth, workRight - workLeft);
        var availableHeight = Math.Max(MinHeight, workBottom - workTop);

        Width = Math.Min(Width, availableWidth);
        Height = Math.Min(Height, availableHeight);
        Left = Math.Clamp(Left, workLeft, Math.Max(workLeft, workRight - Width));
        Top = Math.Clamp(Top, workTop, Math.Max(workTop, workBottom - Height));
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

    private void MiniWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible && IsLoaded)
        {
            _timer.Start();
            RefreshSnapshot();
        }
        else
        {
            _timer.Stop();
        }
    }

    private void MiniWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_initializing) return;
        ApplyResponsiveLayout();

        // Resizing emits many SizeChanged events. Persist only after the user pauses,
        // avoiding a settings-file write for every intermediate resize frame.
        _boundsSaveTimer.Stop();
        _boundsSaveTimer.Start();
    }

    private void ApplyResponsiveLayout()
    {
        var ultraCompact = ActualWidth < 340 || ActualHeight < 100;
        RootBorder.Padding = ultraCompact ? new Thickness(0) : new Thickness(10);
        ElapsedText.Visibility = ultraCompact ? Visibility.Collapsed : Visibility.Visible;
        TopmostToggle.Visibility = ultraCompact ? Visibility.Collapsed : Visibility.Visible;
        ExpandButton.Visibility = ultraCompact ? Visibility.Collapsed : Visibility.Visible;
        CloseMiniButton.Visibility = ultraCompact ? Visibility.Collapsed : Visibility.Visible;
        CompactPauseButton.Visibility = ultraCompact && !_showingTranscription && !_showingIdle
            ? Visibility.Visible : Visibility.Collapsed;
        EncryptionCompactText.Visibility = ultraCompact && !_showingTranscription && !_showingIdle
            ? Visibility.Visible : Visibility.Collapsed;

        if (ultraCompact)
        {
            RecordingPanel.Visibility = Visibility.Collapsed;
            RecordingControls.Visibility = Visibility.Collapsed;
            TranscriptionPanel.Visibility = Visibility.Collapsed;
            IdlePanel.Visibility = Visibility.Collapsed;
            StateText.FontSize = 12;
        }
        else
        {
            StateText.FontSize = 14;
            RecordingPanel.Visibility = _showingTranscription || _showingIdle ? Visibility.Collapsed : Visibility.Visible;
            RecordingControls.Visibility = _showingTranscription || _showingIdle ? Visibility.Collapsed : Visibility.Visible;
            TranscriptionPanel.Visibility = _showingTranscription ? Visibility.Visible : Visibility.Collapsed;
            IdlePanel.Visibility = _showingIdle ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void MiniWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var modifiers = System.Windows.Input.Keyboard.Modifiers;
        if (modifiers == System.Windows.Input.ModifierKeys.None
            && e.Key == System.Windows.Input.Key.Space
            && e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase)
        {
            if (_main.GetMiniModeSnapshot()?.IsRecording == true)
            {
                Pause_Click(sender, e);
                e.Handled = true;
            }
            return;
        }

        if (modifiers == System.Windows.Input.ModifierKeys.Control && e.Key == System.Windows.Input.Key.M)
        {
            Expand_Click(sender, e);
            e.Handled = true;
            return;
        }

        if (modifiers == (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift)
            && e.Key == System.Windows.Input.Key.S
            && _main.GetMiniModeSnapshot()?.IsRecording == true)
        {
            Stop_Click(sender, e);
            e.Handled = true;
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
        PersistBounds();
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
            PersistBounds();
            e.Handled = true;
            return;
        }

        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
                PersistBounds();
            }
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
        MiniModeSnapshot? snapshot;
        try
        {
            snapshot = _main.GetMiniModeSnapshot();
        }
        catch (Exception exception)
        {
            // A transient snapshot failure must not crash the WPF dispatcher.
            StateText.Text = "⚠ ERROR";
            StateText.Foreground = SystemParameters.HighContrast
                ? WpfSystemColors.WindowTextBrush
                : System.Windows.Media.Brushes.OrangeRed;
            SessionText.Text = "No se pudo actualizar el estado. Reintentando…";
            System.Diagnostics.Debug.WriteLine($"Mini-Mode snapshot error: {exception}");
            return;
        }
        if (snapshot is null)
        {
            _timer.Stop();
            _main.CloseMiniModeFromWindow(this);
            return;
        }

        if (RootBorder.ContextMenu?.Items.OfType<System.Windows.Controls.MenuItem>()
            .FirstOrDefault(item => Equals(item.Header, "Detener grabación")) is { } stopMenuItem)
            stopMenuItem.IsEnabled = snapshot.IsRecording;
        if (RootBorder.ContextMenu?.Items.OfType<System.Windows.Controls.MenuItem>()
            .FirstOrDefault(item => Equals(item.Header, "Cancelar transcripción")) is { } cancelMenuItem)
            cancelMenuItem.IsEnabled = snapshot.IsTranscribing && !_main.IsMiniModeTranscriptionCancellationRequested;

        if (snapshot.IsTranscribing)
        {
            CancelTranscriptionButton.IsEnabled = !_main.IsMiniModeTranscriptionCancellationRequested;
            _showingTranscription = true;
            _showingIdle = false;
            ApplyResponsiveLayout();
            StateText.Text = "⏳ TRANSCRIBIENDO";
            StateText.Foreground = System.Windows.Media.Brushes.Orange;
            ElapsedText.Text = string.Empty;
            TranscriptionText.Text = snapshot.Status;
            return;
        }

        _showingTranscription = false;
        _showingIdle = snapshot.IsIdle;
        ApplyResponsiveLayout();

        if (snapshot.IsIdle)
        {
            StateText.Text = "● LISTO";
            StateText.Foreground = SystemParameters.HighContrast
                ? WpfSystemColors.WindowTextBrush
                : System.Windows.Media.Brushes.LightGreen;
            ElapsedText.Text = string.Empty;
            IdleSessionText.Text = snapshot.SessionTitle;
            PlayMicrophoneMiniButton.IsEnabled = snapshot.HasMicrophone;
            PlayClientMiniButton.IsEnabled = snapshot.HasClient;
            StopPlaybackMiniButton.IsEnabled = snapshot.IsPlaying;
            MiniPlaybackSeekSlider.Maximum = Math.Max(1, snapshot.PlaybackDuration.TotalSeconds);
            if (!MiniPlaybackSeekSlider.IsMouseCaptureWithin && !MiniPlaybackSeekSlider.IsKeyboardFocusWithin)
                MiniPlaybackSeekSlider.Value = Math.Clamp(snapshot.PlaybackPosition.TotalSeconds, 0, MiniPlaybackSeekSlider.Maximum);
            MiniPlaybackSeekSlider.IsEnabled = snapshot.HasPlayback && snapshot.PlaybackDuration > TimeSpan.Zero;
            MiniPlaybackCurrentText.Text = FormatMiniPlaybackTime(snapshot.PlaybackPosition);
            MiniPlaybackDurationText.Text = FormatMiniPlaybackTime(snapshot.PlaybackDuration);
            MiniPlaybackTrackText.Text = snapshot.HasPlayback
                ? $"Reproduciendo: {snapshot.PlaybackTrack}"
                : "Sin reproducción";
            return;
        }

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

    private static string FormatMiniPlaybackTime(TimeSpan time)
    {
        var totalMinutes = (long)Math.Max(0, time.TotalMinutes);
        return $"{totalMinutes:D2}:{Math.Max(0, time.Seconds):D2}";
    }

    private void MiniPlaybackSeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MiniPlaybackSeekSlider.IsMouseCaptureWithin || MiniPlaybackSeekSlider.IsKeyboardFocusWithin)
            _main.SeekPlaybackFromMiniMode(TimeSpan.FromSeconds(MiniPlaybackSeekSlider.Value));
    }

    private void PlayMicrophoneMini_Click(object sender, RoutedEventArgs e)
    {
        _main.PlayMicrophoneFromMiniMode();
        RefreshSnapshot();
    }

    private void PlayClientMini_Click(object sender, RoutedEventArgs e)
    {
        _main.PlayClientFromMiniMode();
        RefreshSnapshot();
    }

    private void StopPlaybackMini_Click(object sender, RoutedEventArgs e)
    {
        _main.StopPlaybackFromMiniMode();
        RefreshSnapshot();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_main.GetMiniModeSnapshot()?.IsPaused == true)
                _main.ResumeRecordingFromMiniMode();
            else
                _main.PauseRecordingFromMiniMode();
        }
        catch (Exception exception)
        {
            WpfMessageBox.Show(
                $"No se pudo cambiar el estado de la grabación. La sesión permanece abierta.\n\n{exception.Message}",
                "No se pudo pausar o reanudar", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            RefreshSnapshot();
        }
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        var answer = WpfMessageBox.Show(
            "Se detendrán de forma coordinada las pistas Yo y Cliente. ¿Deseas finalizar la grabación?",
            "Detener grabación", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        PauseButton.IsEnabled = false;
        CompactPauseButton.IsEnabled = false;
        StopButton.IsEnabled = false;
        try
        {
            await _main.StopRecordingFromMiniModeAsync();
        }
        catch (Exception exception)
        {
            WpfMessageBox.Show(
                $"No se pudo finalizar la grabación. No cierres la aplicación; puedes volver a intentarlo.\n\n{exception.Message}",
                "Error al detener la grabación", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RefreshSnapshot();
            var recordingStillActive = false;
            try { recordingStillActive = _main.GetMiniModeSnapshot()?.IsRecording == true; }
            catch (Exception exception) { System.Diagnostics.Debug.WriteLine($"Mini-Mode post-stop state error: {exception}"); }
            PauseButton.IsEnabled = recordingStillActive;
            CompactPauseButton.IsEnabled = recordingStillActive;
            StopButton.IsEnabled = recordingStillActive;
        }
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
        _boundsSaveTimer.Stop();
        PersistBounds();
        Hide();
        _main.Show();
        _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    public void CloseFromMain()
    {
        _allowClose = true;
        _timer.Stop();
        _boundsSaveTimer.Stop();
        PersistBounds();
        Close();
    }

    private void PersistBounds()
    {
        if (_initializing || !IsLoaded) return;
        if (!double.IsFinite(Left) || !double.IsFinite(Top)
            || !double.IsFinite(Width) || !double.IsFinite(Height))
            return;

        _main.UpdateMiniModePreferences(Topmost, Left, Top, Width, Height);
    }
}
