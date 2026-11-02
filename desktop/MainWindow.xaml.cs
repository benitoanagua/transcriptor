using WpfMessageBox = System.Windows.MessageBox;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using NAudio.Wave;
using System.Windows.Threading;
using Transcriptor.Rec;

namespace Transcriptor.Desktop;

public sealed record RecordingEntry(string Title, string DirectoryPath, string MicrophonePath, string ClientPath)
{
    public bool HasMicrophone => File.Exists(MicrophonePath);
    public bool HasClient => File.Exists(ClientPath);
    public string MergedTranscriptPath => Path.Combine(DirectoryPath, "transcripcion.srt");
    public string MicrophoneTranscriptPath => Path.Combine(DirectoryPath, "yo.srt");
    public string ClientTranscriptPath => Path.Combine(DirectoryPath, "cliente.srt");
    public bool HasTranscript => File.Exists(MergedTranscriptPath)
        || File.Exists(MicrophoneTranscriptPath) || File.Exists(ClientTranscriptPath);
}

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly SettingsStore _settingsStore = new();
    private AppSettings _settings = AppSettings.CreateDefault();
    private RecordingSession? _recording;
    private WaveOutEvent? _playback;
    private AudioFileReader? _playbackReader;
    private bool _stopping;
    private string _loadedTranscript = string.Empty;
    private int _lastTranscriptMatch = -1;

    public MainWindow()
    {
        InitializeComponent();
        var loaded = _settingsStore.Load();
        _settings = loaded.Settings;
        RecordingsPathBox.Text = _settings.RecordingsDirectory;
        RecorderDllBox.Text = _settings.RecorderDllPath;
        WhisperExecutableBox.Text = _settings.WhisperExecutablePath;
        WhisperModelBox.Text = _settings.WhisperModelPath;
        WhisperLanguageBox.Text = _settings.Language;
        RetentionDaysBox.Text = _settings.RetentionDays.ToString();
        _timer.Tick += (_, _) => UpdateRecordingStatus();
        RefreshAudioDevices();
        if (!string.IsNullOrWhiteSpace(loaded.Warning))
            Log(loaded.Warning);
        Log("Configuración cargada desde " + _settingsStore.SettingsPath);
        Log("Aplicación iniciada.");
    }

    private void RefreshDevices_Click(object sender, RoutedEventArgs e) => RefreshAudioDevices();

    private void RefreshAudioDevices()
    {
        try
        {
            var selectedMic = MicrophoneDeviceBox.SelectedValue as string;
            var selectedOutput = OutputDeviceBox.SelectedValue as string;
            var microphones = RecordingSession.GetCaptureDevices();
            var outputs = RecordingSession.GetRenderDevices();
            MicrophoneDeviceBox.ItemsSource = microphones;
            OutputDeviceBox.ItemsSource = outputs;
            var preferredMic = selectedMic ?? _settings.MicrophoneDeviceId;
            var preferredOutput = selectedOutput ?? _settings.OutputDeviceId;
            MicrophoneDeviceBox.SelectedValue = microphones.Any(x => x.Id == preferredMic)
                ? preferredMic : RecordingSession.GetDefaultDeviceId(NAudio.CoreAudioApi.DataFlow.Capture);
            OutputDeviceBox.SelectedValue = outputs.Any(x => x.Id == preferredOutput)
                ? preferredOutput : RecordingSession.GetDefaultDeviceId(NAudio.CoreAudioApi.DataFlow.Render);

            if (MicrophoneDeviceBox.SelectedItem is null && microphones.Count > 0)
                MicrophoneDeviceBox.SelectedIndex = 0;
            if (OutputDeviceBox.SelectedItem is null && outputs.Count > 0)
                OutputDeviceBox.SelectedIndex = 0;

            Log($"Dispositivos detectados: {microphones.Count} entradas y {outputs.Count} salidas.");
        }
        catch (Exception exception)
        {
            Log("No se pudieron enumerar los dispositivos de audio: " + exception.Message);
            WpfMessageBox.Show(exception.Message, "Dispositivos de audio", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e) => SaveSettings(showConfirmation: true);

    private void SaveSettings(bool showConfirmation)
    {
        try
        {
            _settings = _settings with
            {
                RecordingsDirectory = RecordingsPathBox.Text.Trim(),
                RecorderDllPath = RecorderDllBox.Text.Trim(),
                WhisperExecutablePath = WhisperExecutableBox.Text.Trim(),
                WhisperModelPath = WhisperModelBox.Text.Trim(),
                Language = string.IsNullOrWhiteSpace(WhisperLanguageBox.Text) ? "es" : WhisperLanguageBox.Text.Trim(),
                RetentionDays = int.TryParse(RetentionDaysBox.Text, out var days) ? Math.Clamp(days, 0, 3650) : 0,
                MicrophoneDeviceId = MicrophoneDeviceBox.SelectedValue as string,
                OutputDeviceId = OutputDeviceBox.SelectedValue as string
            };
            _settingsStore.Save(_settings);
            Log("Configuración guardada.");
            if (showConfirmation)
                WpfMessageBox.Show("La configuración se guardó correctamente.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            Log("No se pudo guardar la configuración: " + exception.Message);
            WpfMessageBox.Show(exception.Message, "Error de configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BrowseRecordings_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = RecordingsPathBox.Text };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            RecordingsPathBox.Text = dialog.SelectedPath;
            RefreshRecordings();
        }
    }

    private void BrowseRecorder_Click(object sender, RoutedEventArgs e)
    {
        var path = PickDll("Selecciona rec.dll");
        if (path is not null) RecorderDllBox.Text = path;
    }

    private void BrowseWhisperExecutable_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WpfOpenFileDialog { Title = "Selecciona whisper-cli.exe", Filter = "Ejecutable (*.exe)|*.exe|Todos los archivos (*.*)|*.*" };
        if (dialog.ShowDialog() == true) WhisperExecutableBox.Text = dialog.FileName;
    }

    private void BrowseWhisperModel_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WpfOpenFileDialog { Title = "Selecciona el modelo Whisper", Filter = "Modelo Whisper (*.bin)|*.bin|Todos los archivos (*.*)|*.*" };
        if (dialog.ShowDialog() == true) WhisperModelBox.Text = dialog.FileName;
    }

    private static string? PickDll(string title)
    {
        var dialog = new WpfOpenFileDialog { Title = title, Filter = "Biblioteca .NET (*.dll)|*.dll|Todos los archivos (*.*)|*.*" };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private void StartRecording_Click(object sender, RoutedEventArgs e)
    {
        if (_recording is not null) return;
        if (ConsentCheckBox.IsChecked != true)
        {
            WpfMessageBox.Show("Antes de grabar, confirma que cuentas con autorización de todas las personas involucradas.", "Consentimiento requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        SaveSettings(showConfirmation: false);
        if (MicrophoneDeviceBox.SelectedValue is not string microphoneId || OutputDeviceBox.SelectedValue is not string outputId)
        {
            WpfMessageBox.Show("Selecciona un micrófono y una salida de audio activos.", "Dispositivos requeridos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var outputDirectory = CreateRecordingDirectory(RecordingsPathBox.Text, ReferenceBox.Text);
            var recording = new RecordingSession(outputDirectory, microphoneId, outputId);
            try { recording.Start(); _recording = recording; }
            catch { recording.Dispose(); throw; }

            _stopping = false;
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            TranscribeButton.IsEnabled = false;
            MicrophoneDeviceBox.IsEnabled = false;
            OutputDeviceBox.IsEnabled = false;
            StatusText.Text = "GRABANDO";
            StatusText.Foreground = System.Windows.Media.Brushes.DarkRed;
            _timer.Start();
            Log($"Grabación iniciada. Micrófono: {recording.MicrophoneName}; salida: {recording.OutputName}");
            Log($"Guardando pistas en: {outputDirectory}");
        }
        catch (Exception exception)
        {
            Log("No se pudo iniciar la grabación: " + exception.Message);
            WpfMessageBox.Show(exception.Message, "Error de grabación", MessageBoxButton.OK, MessageBoxImage.Error);
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
            WpfMessageBox.Show(exception.Message, "Error al cerrar grabación", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { ResetRecordingState(); RefreshRecordings(); }
    }

    private async void Transcribe_Click(object sender, RoutedEventArgs e)
    {
        if (_recording is not null) return;
        if (!File.Exists(WhisperExecutableBox.Text))
        {
            WpfMessageBox.Show("No se encuentra whisper-cli.exe. Configura el ejecutable oficial de Whisper.cpp.", "Motor Whisper no configurado", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!File.Exists(WhisperModelBox.Text))
        {
            WpfMessageBox.Show("No se encuentra el modelo Whisper. Selecciona un archivo .bin válido.", "Modelo no configurado", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveSettings(showConfirmation: false);
        TranscribeButton.IsEnabled = false;
        StatusText.Text = "TRANSCRIBIENDO";
        try
        {
            var engine = new Transcriptor.Transcription.TranscriptionEngine(
                WhisperExecutableBox.Text.Trim(), WhisperModelBox.Text.Trim(),
                string.IsNullOrWhiteSpace(WhisperLanguageBox.Text) ? "es" : WhisperLanguageBox.Text.Trim());
            var progress = new Progress<string>(message => Log(message));
            var processed = await engine.ProcessPendingCallsAsync(RecordingsPathBox.Text.Trim(), progress);
            Log($"Proceso integrado finalizado. Sesiones transcritas: {processed}.");
            StatusText.Text = "LISTO";
            RefreshRecordings();
        }
        catch (Exception exception)
        {
            Log("Error de transcripción: " + exception.Message);
            StatusText.Text = "ERROR";
            WpfMessageBox.Show(exception.Message, "Error de transcripción", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { TranscribeButton.IsEnabled = true; }
    }


    private void DeleteRecording_Click(object sender, RoutedEventArgs e)
    {
        if (RecordingList.SelectedItem is not RecordingEntry entry || _recording is not null) return;
        var answer = WpfMessageBox.Show("Se eliminarán permanentemente los audios y transcripciones de esta sesión. Esta acción no se puede deshacer.", "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        try { StopPlayback(); RecordingPrivacy.DeleteSession(RecordingsPathBox.Text, entry.DirectoryPath); Log("Sesión eliminada: " + entry.DirectoryPath); RefreshRecordings(); }
        catch (Exception exception) { Log("No se pudo eliminar la sesión: " + exception.Message); WpfMessageBox.Show(exception.Message, "Error al eliminar", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ApplyRetention_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings(showConfirmation: false);
        if (_settings.RetentionDays <= 0) { WpfMessageBox.Show("La retención está desactivada. Define un plazo mayor que cero.", "Retención desactivada", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var answer = WpfMessageBox.Show("Se eliminarán las sesiones antiguas según el plazo configurado. Esta acción no se puede deshacer.", "Aplicar retención", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        try { StopPlayback(); var deleted = RecordingPrivacy.ApplyRetention(RecordingsPathBox.Text, _settings.RetentionDays, DateTime.UtcNow); Log("Sesiones eliminadas por retención: " + deleted.Count); RefreshRecordings(); }
        catch (Exception exception) { Log("Error de retención: " + exception.Message); WpfMessageBox.Show(exception.Message, "Error de retención", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void RefreshRecordings_Click(object sender, RoutedEventArgs e) => RefreshRecordings();

    private void RefreshRecordings()
    {
        try
        {
            var root = RecordingsPathBox.Text;
            var entries = new List<RecordingEntry>();
            if (Directory.Exists(root))
            {
                foreach (var directory in RecordingPrivacy.GetSessions(root))
                {
                    var microphone = Path.Combine(directory, "yo.wav");
                    var client = Path.Combine(directory, "cliente.wav");
                    var parent = Directory.GetParent(directory)?.Name;
                    var title = string.IsNullOrWhiteSpace(parent) ? Path.GetFileName(directory) : $"{parent} · {Path.GetFileName(directory)}";
                    entries.Add(new RecordingEntry(title, directory, microphone, client));
                }
            }
            RecordingList.ItemsSource = entries.OrderByDescending(entry => entry.DirectoryPath, StringComparer.OrdinalIgnoreCase).ToList();
            RecordingCountText.Text = $"{entries.Count} sesión(es) encontrada(s)";
            RecordingDetailsText.Text = "Selecciona una sesión para consultar las pistas disponibles.";
            TranscriptTextBox.Clear();
            TranscriptStatusText.Text = "Selecciona una sesión con transcripción.";
            _loadedTranscript = string.Empty;
            _lastTranscriptMatch = -1;
        }
        catch (Exception exception) { Log("No se pudo actualizar la biblioteca: " + exception.Message); }
    }

    private void RecordingList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecordingList.SelectedItem is not RecordingEntry entry) return;
        RecordingDetailsText.Text = $"{entry.DirectoryPath}{Environment.NewLine}{(entry.HasMicrophone ? "yo.wav disponible" : "yo.wav ausente")} · {(entry.HasClient ? "cliente.wav disponible" : "cliente.wav ausente")}";
        LoadTranscript(entry);
    }

    private void LoadTranscript(RecordingEntry entry)
    {
        try
        {
            var sections = new List<string>();
            if (File.Exists(entry.MergedTranscriptPath))
            {
                sections.Add(File.ReadAllText(entry.MergedTranscriptPath));
            }
            else
            {
                if (File.Exists(entry.MicrophoneTranscriptPath))
                {
                    sections.Add("===== YO =====");
                    sections.Add(File.ReadAllText(entry.MicrophoneTranscriptPath));
                }
                if (File.Exists(entry.ClientTranscriptPath))
                {
                    sections.Add("===== CLIENTE =====");
                    sections.Add(File.ReadAllText(entry.ClientTranscriptPath));
                }
            }

            _loadedTranscript = string.Join(Environment.NewLine + Environment.NewLine, sections);
            TranscriptTextBox.Text = _loadedTranscript;
            _lastTranscriptMatch = -1;
            TranscriptStatusText.Text = sections.Count == 0
                ? "Esta sesión todavía no tiene archivos SRT."
                : $"Transcripción cargada · { _loadedTranscript.Length:N0} caracteres";
        }
        catch (Exception exception)
        {
            _loadedTranscript = string.Empty;
            TranscriptTextBox.Clear();
            TranscriptStatusText.Text = "No se pudo leer la transcripción: " + exception.Message;
            Log("Error al leer transcripción: " + exception.Message);
        }
    }

    private void SearchTranscript_Click(object sender, RoutedEventArgs e) => SearchTranscript();

    private void TranscriptSearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            SearchTranscript();
            e.Handled = true;
        }
    }

    private void SearchTranscript()
    {
        var query = TranscriptSearchBox.Text.Trim();
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(_loadedTranscript))
        {
            TranscriptStatusText.Text = string.IsNullOrEmpty(_loadedTranscript)
                ? "No hay transcripción cargada."
                : "Escribe un texto para buscar.";
            return;
        }

        var startAt = Math.Clamp(_lastTranscriptMatch + 1, 0, _loadedTranscript.Length);
        var match = _loadedTranscript.IndexOf(query, startAt, StringComparison.CurrentCultureIgnoreCase);
        if (match < 0 && startAt > 0)
            match = _loadedTranscript.IndexOf(query, 0, StringComparison.CurrentCultureIgnoreCase);

        if (match < 0)
        {
            _lastTranscriptMatch = -1;
            TranscriptStatusText.Text = $"No se encontró «{query}».";
            return;
        }

        _lastTranscriptMatch = match;
        TranscriptTextBox.Focus();
        TranscriptTextBox.Select(match, query.Length);
        TranscriptTextBox.ScrollToLine(TranscriptTextBox.GetLineIndexFromCharacterIndex(match));
        TranscriptStatusText.Text = $"Coincidencia en posición {match + 1:N0}. Pulsa buscar siguiente para continuar.";
    }

    private void PlayMicrophone_Click(object sender, RoutedEventArgs e)
    {
        if (RecordingList.SelectedItem is RecordingEntry entry) PlayTrack(entry.MicrophonePath, "micrófono");
    }

    private void PlayClient_Click(object sender, RoutedEventArgs e)
    {
        if (RecordingList.SelectedItem is RecordingEntry entry) PlayTrack(entry.ClientPath, "audio del sistema");
    }

    private void PlayTrack(string path, string trackName)
    {
        if (!File.Exists(path))
        {
            WpfMessageBox.Show($"No se encuentra el archivo: {path}", "Pista no disponible", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            StopPlayback();
            _playbackReader = new AudioFileReader(path);
            _playback = new WaveOutEvent();
            _playback.PlaybackStopped += (_, args) => Dispatcher.InvokeAsync(() =>
            {
                if (args.Exception is not null) Log("Error de reproducción: " + args.Exception.Message);
                ReleasePlayback();
            });
            _playback.Init(_playbackReader);
            _playback.Play();
            Log($"Reproduciendo pista de {trackName}: {path}");
        }
        catch (Exception exception)
        {
            ReleasePlayback();
            Log("No se pudo reproducir la pista: " + exception.Message);
            WpfMessageBox.Show(exception.Message, "Error de reproducción", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void StopPlayback_Click(object sender, RoutedEventArgs e) => StopPlayback();

    private void StopPlayback()
    {
        try { _playback?.Stop(); }
        finally { ReleasePlayback(); }
    }

    private void ReleasePlayback()
    {
        _playback?.Dispose();
        _playback = null;
        _playbackReader?.Dispose();
        _playbackReader = null;
    }

    private void OpenRecordingFolder_Click(object sender, RoutedEventArgs e)
    {
        if (RecordingList.SelectedItem is RecordingEntry entry)
            Process.Start(new ProcessStartInfo(entry.DirectoryPath) { UseShellExecute = true });
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
        MicrophoneDeviceBox.IsEnabled = true;
        OutputDeviceBox.IsEnabled = true;
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
        StopPlayback();
        SaveSettings(showConfirmation: false);
        if (_recording is not null) await StopRecordingAsync();
        base.OnClosed(e);
    }
}
