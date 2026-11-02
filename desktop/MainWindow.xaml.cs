using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Transcriptor.Desktop;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private Process? _recorder;
    private DateTime _recordingStarted;
    private bool _stopping;

    public MainWindow()
    {
        InitializeComponent();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        RecordingsPathBox.Text = Environment.GetEnvironmentVariable("TRANSCRIPTOR_DIR")
            ?? Path.Combine(home, "Transcriptor", "Grabaciones");
        var baseDirectory = AppContext.BaseDirectory;
        RecorderDllBox.Text = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "rec", "bin", "Release", "net10.0-windows", "rec.dll"));
        TranscriberDllBox.Text = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "transcribe", "bin", "Release", "net10.0-windows", "transcriptor-transcribe.dll"));
        _timer.Tick += (_, _) => UpdateElapsed();
        Log("Aplicación iniciada.");
    }

    private void BrowseRecordings_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = RecordingsPathBox.Text };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            RecordingsPathBox.Text = dialog.SelectedPath;
    }

    private void BrowseRecorder_Click(object sender, RoutedEventArgs e)
    {
        var path = PickDll("Selecciona rec.dll");
        if (path is not null) RecorderDllBox.Text = path;
    }

    private void BrowseTranscriber_Click(object sender, RoutedEventArgs e)
    {
        var path = PickDll("Selecciona transcriptor-transcribe.dll");
        if (path is not null) TranscriberDllBox.Text = path;
    }

    private static string? PickDll(string title)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = "Biblioteca .NET (*.dll)|*.dll|Todos los archivos (*.*)|*.*" };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private async void StartRecording_Click(object sender, RoutedEventArgs e)
    {
        if (_recorder is not null) return;
        if (!File.Exists(RecorderDllBox.Text))
        {
            MessageBox.Show("No se encuentra rec.dll. Compila el proyecto de grabación o selecciona su ruta.", "Transcriptor", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Directory.CreateDirectory(RecordingsPathBox.Text);
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            start.ArgumentList.Add(RecorderDllBox.Text);
            start.ArgumentList.Add("--out");
            start.ArgumentList.Add(RecordingsPathBox.Text);
            start.ArgumentList.Add("--ref");
            start.ArgumentList.Add(string.IsNullOrWhiteSpace(ReferenceBox.Text) ? "Llamada" : ReferenceBox.Text);

            var process = new Process { StartInfo = start, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, args) => Dispatcher.Invoke(() => { if (args.Data is not null) Log(args.Data); });
            process.ErrorDataReceived += (_, args) => Dispatcher.Invoke(() => { if (args.Data is not null) Log("ERROR: " + args.Data); });
            process.Exited += (_, _) => Dispatcher.Invoke(() => RecorderExited(process));
            if (!process.Start()) throw new InvalidOperationException("No se pudo iniciar el proceso de grabación.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _recorder = process;
            _recordingStarted = DateTime.Now;
            _stopping = false;
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            TranscribeButton.IsEnabled = false;
            StatusText.Text = "GRABANDO";
            StatusText.Foreground = System.Windows.Media.Brushes.DarkRed;
            _timer.Start();
            Log("Grabación iniciada. " + DateTime.Now.ToString("G"));
            await Task.CompletedTask;
        }
        catch (Exception exception)
        {
            Log("No se pudo iniciar la grabación: " + exception.Message);
            MessageBox.Show(exception.Message, "Error de grabación", MessageBoxButton.OK, MessageBoxImage.Error);
            ResetRecordingState();
        }
    }

    private async void StopRecording_Click(object sender, RoutedEventArgs e) => await StopRecordingAsync();

    private async Task StopRecordingAsync()
    {
        var process = _recorder;
        if (process is null || _stopping) return;
        _stopping = true;
        StopButton.IsEnabled = false;
        Log("Solicitando cierre de las dos pistas...");
        try
        {
            await process.StandardInput.WriteLineAsync();
            await process.StandardInput.FlushAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            await process.WaitForExitAsync(timeout.Token);
            Log(process.ExitCode == 0 ? "Grabación finalizada correctamente." : $"El grabador terminó con código {process.ExitCode}.");
        }
        catch (OperationCanceledException)
        {
            Log("El grabador no confirmó el cierre dentro del tiempo límite. Revisa los WAV antes de usarlos.");
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception)
        {
            Log("Error al detener: " + exception.Message);
        }
        finally
        {
            ResetRecordingState();
        }
    }

    private async void Transcribe_Click(object sender, RoutedEventArgs e)
    {
        if (_recorder is not null) return;
        if (!File.Exists(TranscriberDllBox.Text))
        {
            MessageBox.Show("No se encuentra transcriptor-transcribe.dll. Compila el proyecto o selecciona su ruta.", "Transcriptor", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TranscribeButton.IsEnabled = false;
        StatusText.Text = "TRANSCRIBIENDO";
        try
        {
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            start.ArgumentList.Add(TranscriberDllBox.Text);
            start.ArgumentList.Add(RecordingsPathBox.Text);
            using var process = new Process { StartInfo = start };
            process.OutputDataReceived += (_, args) => Dispatcher.Invoke(() => { if (args.Data is not null) Log(args.Data); });
            process.ErrorDataReceived += (_, args) => Dispatcher.Invoke(() => { if (args.Data is not null) Log("ERROR: " + args.Data); });
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();
            Log(process.ExitCode == 0 ? "Proceso de transcripción terminado." : $"La transcripción terminó con código {process.ExitCode}.");
            StatusText.Text = process.ExitCode == 0 ? "LISTO" : "ERROR";
        }
        catch (Exception exception)
        {
            Log("Error de transcripción: " + exception.Message);
            StatusText.Text = "ERROR";
        }
        finally
        {
            TranscribeButton.IsEnabled = true;
        }
    }

    private void RecorderExited(Process process)
    {
        if (!ReferenceEquals(_recorder, process)) return;
        Log($"El proceso de grabación terminó con código {process.ExitCode}.");
        ResetRecordingState();
    }

    private void ResetRecordingState()
    {
        _timer.Stop();
        _recorder?.Dispose();
        _recorder = null;
        _stopping = false;
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        TranscribeButton.IsEnabled = true;
        MicLevel.Value = 0;
        SystemLevel.Value = 0;
        MicLevelText.Text = "En espera";
        SystemLevelText.Text = "En espera";
        StatusText.Text = "LISTO";
        StatusText.Foreground = System.Windows.Media.Brushes.RoyalBlue;
        ElapsedText.Text = "00:00:00";
    }

    private void UpdateElapsed()
    {
        var elapsed = DateTime.Now - _recordingStarted;
        ElapsedText.Text = elapsed.ToString(@"hh:mm:ss");
    }

    private void Log(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        ActivityLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        ActivityLog.ScrollToEnd();
    }

    protected override async void OnClosed(EventArgs e)
    {
        if (_recorder is not null)
            await StopRecordingAsync();
        base.OnClosed(e);
    }
}
