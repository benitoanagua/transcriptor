using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace Transcriptor.Desktop;

public partial class MiniModeWindow : Window
{
    private readonly MainWindow _main;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private bool _allowClose;

    public MiniModeWindow(MainWindow main)
    {
        _main = main ?? throw new ArgumentNullException(nameof(main));
        InitializeComponent();
        Topmost = main.MiniModeAlwaysOnTop;
        TopmostToggle.IsChecked = Topmost;
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

        _timer.Tick += (_, _) => RefreshSnapshot();
    }

    private void MiniWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _timer.Start();
        RefreshSnapshot();
    }

    private void RefreshSnapshot()
    {
        var snapshot = _main.GetMiniModeSnapshot();
        if (snapshot is null)
        {
            _timer.Stop();
            CloseFromMain();
            return;
        }

        StateText.Text = snapshot.IsPaused ? "Ⅱ PAUSADO" : "● REC";
        StateText.Foreground = snapshot.IsPaused
            ? System.Windows.Media.Brushes.Orange
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 17, 35));
        ElapsedText.Text = snapshot.Elapsed.ToString(@"hh\:mm\:ss");
        MicLevel.Value = snapshot.MicrophoneLevel * 100;
        SystemLevel.Value = snapshot.SystemLevel * 100;
        MicLevelText.Text = snapshot.MicrophoneLevel.ToString("P0");
        SystemLevelText.Text = snapshot.SystemLevel.ToString("P0");
        PauseButton.Content = snapshot.IsPaused ? "▶ Reanudar" : "Ⅱ Pausar";
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
        PauseButton.IsEnabled = false;
        await _main.StopRecordingFromMiniModeAsync();
    }

    private void Topmost_Changed(object sender, RoutedEventArgs e)
    {
        Topmost = TopmostToggle.IsChecked == true;
        _main.UpdateMiniModePreferences(Topmost, Left, Top, Width, Height);
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
