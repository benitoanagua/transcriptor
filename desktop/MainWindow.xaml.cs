using WpfMessageBox = System.Windows.MessageBox;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Transcriptor.Rec;

namespace Transcriptor.Desktop;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private RecordingSession? _recording;
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
        _timer.Tick += (_, _) => UpdateRecordingStatus();
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
        var dialog = new WpfOpenFileDialog { Title = title, Filter = "Biblioteca .NET (*.dll)|*.dll|Todos los archivos (*.*)|*.*" };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private void StartRecording_Click(object sender, RoutedEventArgs e)
    {
        if (_recording is not null) return;

        try
        {
            var outputDirectory = CreateRecordingDirectory(RecordingsPathBox.Text, ReferenceBox.Text);
            var recording = new RecordingSession(outputDirectory);
            try
            {
                recording.Start();
                _recording = recording;
            }
            catch
            {
                recording.Dispose();
                throw;
            }

            _recordingStarted = DateTime.Now;
            _stopping = false;
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            TranscribeButton.IsEnabled = false;
            StatusText.Text = "GRABANDO";
            StatusText.Foreground = System.Windows.Media.Brushes.DarkRed;
            _timer.Start();
            Log($"Grabación iniciada. Micrófono: {recording.MicrophoneName}; salida: {recording.OutputName}");
            Log($"Guardando pistas en: {outputDirectory}");
        }
        catch (Exception exception)
        {
            Log("No se pudo iniciar la grabación: " + exception.Message);
            WpfMessageBox.Show(exception.Message, "Error de grabación", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            ResetRecordingState();
        }
    }

    private async void StopRecording_Click(object sender, RoutedEventArgs e) => await StopRecordingAsync();

    private async Task StopRecordingAsync()
    {
        var recording = _recording;
        if (recording is null || _stopping) return;
        _stopping = true;
        StopButton.IsEnabled = false;
        Log("Cerrando las dos pistas de audio...");
        try
        {
            await Task.Run(recording.Stop);
            Log("Grabación finalizada correctamente. Los archivos WAV están cerrados.");
        }
        catch (Exception exception)
        {
            Log("Error al finalizar la grabación: " + exception.Message);
            WpfMessageBox.Show(exception.Message, "Error al cerrar grabación", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
        finally
        {
            ResetRecordingState();
        }
    }

    private async void Transcribe_Click(object sender, RoutedEventArgs e)
    {
        if (_recording is not null) return;
        if (!File.Exists(TranscriberDllBox.Text))
        {
            WpfMessageBox.Show("No se encuentra transcriptor-transcribe.dll. Compila el proyecto o selecciona su ruta.", "Transcriptor", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
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

    private void ResetRecordingState()
    {
        _timer.Stop();
        _recording?.Dispose();
        _recording = null;
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

    private void UpdateRecordingStatus()
    {
        var recording = _recording;
        if (recording is null) return;
        ElapsedText.Text = recording.Elapsed.ToString(@"hh\:mm\:ss");
        MicLevel.Value = recording.MicrophoneLevel * 100;
        SystemLevel.Value = recording.SystemLevel * 100;
        MicLevelText.Text = $"{recording.MicrophoneLevel:P0}";
        SystemLevelText.Text = $"{recording.SystemLevel:P0}";
    }

    private static string CreateRecordingDirectory(string root, string reference)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("Selecciona una carpeta para guardar las grabaciones.");

        var now = DateTime.Now;
        var invalid = Path.GetInvalidFileNameChars().Append(' ').ToArray();
        var safeReference = string.Join("-", (string.IsNullOrWhiteSpace(reference) ? "Llamada" : reference.Trim())
            .Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        var directory = Path.Combine(root, now.ToString("yyyy-MM-dd"), now.ToString("HHmmss") + "_" + safeReference);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private void Log(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        ActivityLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        ActivityLog.ScrollToEnd();
    }

    protected override async void OnClosed(EventArgs e)
    {
        if (_recording is not null)
            await StopRecordingAsync();
        base.OnClosed(e);
    }
}
