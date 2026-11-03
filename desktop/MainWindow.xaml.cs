using WpfMessageBox = System.Windows.MessageBox;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;
using System.ComponentModel;
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
    public string MergedTranscriptPath => Path.Combine(DirectoryPath, "transcripcion.srt.trn");
    public string MicrophoneTranscriptPath => Path.Combine(DirectoryPath, "yo.srt.trn");
    public string ClientTranscriptPath => Path.Combine(DirectoryPath, "cliente.srt.trn");
    public bool IsIncomplete => Transcriptor.Transcription.RecordingIntegrity.IsIncomplete(DirectoryPath);
    public bool HasTranscript => File.Exists(MergedTranscriptPath)
        || File.Exists(MicrophoneTranscriptPath) || File.Exists(ClientTranscriptPath)
        || File.Exists(Path.Combine(DirectoryPath, "transcripcion.srt"))
        || File.Exists(Path.Combine(DirectoryPath, "yo.srt"))
        || File.Exists(Path.Combine(DirectoryPath, "cliente.srt"));
}

internal sealed record MiniModeSnapshot(bool IsRecording, bool IsTranscribing, bool IsError, TimeSpan Elapsed, float MicrophoneLevel, float SystemLevel, bool IsPaused, string Status, string SessionTitle);

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private MiniModeWindow? _miniModeWindow;
    private readonly SettingsStore _settingsStore = new();
    private readonly ISessionStore _sessionStore;
    private readonly IClock _clock;
    private AppSettings _settings = AppSettings.CreateDefault();
    private RecordingSession? _recording;
    private WaveOutEvent? _playback;
    private AudioFileReader? _playbackReader;
    private string? _playbackTemporaryPath;
    private bool _allowClose;
    private bool _closeRequested;
    private Task? _stopRecordingTask;
    private readonly TranscriptionUiState _transcriptionUiState = new();
    private CancellationTokenSource? _transcriptionCancellation;
    private Task? _transcriptionTask;
    private string _miniModeTranscriptionStatus = "Preparando transcripción…";
    private string _activeSessionTitle = string.Empty;
    private string? _miniModeCaptureError;
    private string _loadedTranscript = string.Empty;
    private int _lastTranscriptMatch = -1;

    internal bool MiniModeAlwaysOnTop => _settings.MiniModeAlwaysOnTop;
    internal double? MiniModeLeft => _settings.MiniModeLeft;
    internal double? MiniModeTop => _settings.MiniModeTop;
    internal double? MiniModeWidth => _settings.MiniModeWidth;
    internal double? MiniModeHeight => _settings.MiniModeHeight;

    public MainWindow() : this(new LocalSessionStore(), SystemClock.Instance) { }

    public MainWindow(ISessionStore sessionStore) : this(sessionStore, SystemClock.Instance) { }

    public MainWindow(ISessionStore sessionStore, IClock clock)
    {
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        InitializeComponent();
        var loaded = _settingsStore.Load();
        _settings = loaded.Settings;
        RecordingsPathBox.Text = _settings.RecordingsDirectory;
        WhisperExecutableBox.Text = _settings.WhisperExecutablePath;
        WhisperModelBox.Text = _settings.WhisperModelPath;
        WhisperLanguageBox.Text = _settings.Language;
        RetentionDaysBox.Text = _settings.RetentionDays.ToString();
        MiniModeAutoShowCheckBox.IsChecked = _settings.MiniModeAutoShow;
        _timer.Tick += (_, _) => UpdateRecordingStatus();
        RefreshAudioDevices();
        if (!string.IsNullOrWhiteSpace(loaded.Warning))
            Log(loaded.Warning);
        Log("Configuración cargada desde " + _settingsStore.SettingsPath);
        Log("Aplicación iniciada.");
    }

    private void RefreshDevices_Click(object sender, RoutedEventArgs e)
    {
        if (_transcriptionUiState.IsTranscribing) return;
        RefreshAudioDevices();
    }

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

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_transcriptionUiState.IsTranscribing) return;
        SaveSettings(showConfirmation: true);
    }

    private void SaveSettings(bool showConfirmation)
    {
        try
        {
            _settings = _settings with
            {
                RecordingsDirectory = RecordingsPathBox.Text.Trim(),
                WhisperExecutablePath = WhisperExecutableBox.Text.Trim(),
                WhisperModelPath = WhisperModelBox.Text.Trim(),
                Language = string.IsNullOrWhiteSpace(WhisperLanguageBox.Text) ? "es" : WhisperLanguageBox.Text.Trim(),
                RetentionDays = int.TryParse(RetentionDaysBox.Text, out var days) ? Math.Clamp(days, 0, 3650) : 0,
                MicrophoneDeviceId = MicrophoneDeviceBox.SelectedValue as string,
                OutputDeviceId = OutputDeviceBox.SelectedValue as string,
                MiniModeAutoShow = MiniModeAutoShowCheckBox.IsChecked == true
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
        if (_transcriptionUiState.IsTranscribing) return;
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = RecordingsPathBox.Text };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            RecordingsPathBox.Text = dialog.SelectedPath;
            RefreshRecordings();
        }
    }

    private void BrowseWhisperExecutable_Click(object sender, RoutedEventArgs e)
    {
        if (_transcriptionUiState.IsTranscribing) return;
        var dialog = new WpfOpenFileDialog { Title = "Selecciona whisper-cli.exe", Filter = "Ejecutable (*.exe)|*.exe|Todos los archivos (*.*)|*.*" };
        if (dialog.ShowDialog() == true) WhisperExecutableBox.Text = dialog.FileName;
    }

    private void BrowseWhisperModel_Click(object sender, RoutedEventArgs e)
    {
        if (_transcriptionUiState.IsTranscribing) return;
        var dialog = new WpfOpenFileDialog { Title = "Selecciona el modelo Whisper", Filter = "Modelo Whisper (*.bin)|*.bin|Todos los archivos (*.*)|*.*" };
        if (dialog.ShowDialog() == true) WhisperModelBox.Text = dialog.FileName;
    }

    private void StartRecording_Click(object sender, RoutedEventArgs e)
    {
        if (_recording is not null || _transcriptionUiState.IsTranscribing) return;
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
            var outputDirectory = RecordingDirectoryFactory.Create(RecordingsPathBox.Text, ReferenceBox.Text, _clock);
            _activeSessionTitle = Path.GetFileName(outputDirectory);
            Transcriptor.Transcription.RecordingAccessControl.RestrictSessionDirectory(outputDirectory);
            Log("Permisos NTFS aplicados: usuario actual, SYSTEM y administradores locales.");
            var recording = new RecordingSession(outputDirectory, microphoneId, outputId);
            _miniModeCaptureError = null;
            recording.Faulted += exception => Dispatcher.InvokeAsync(() =>
            {
                _miniModeCaptureError = exception.Message;
                Log("Fallo de captura: se detuvieron ambas pistas y la sesión quedó marcada como incompleta. " + exception.Message);
                StatusText.Text = "ERROR DE GRABACIÓN";
                StatusText.Foreground = (System.Windows.Media.Brush)FindResource("RecBrush");
            });
            try { recording.Start(); _recording = recording; }
            catch
            {
                try { recording.Dispose(); }
                catch { /* Se conserva el error original del inicio. */ }
                throw;
            }

                StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            PauseRecordingButton.IsEnabled = true;
            PauseRecordingButton.Content = "Ⅱ PAUSAR";
            TranscribeButton.IsEnabled = false;
            MicrophoneDeviceBox.IsEnabled = false;
            OutputDeviceBox.IsEnabled = false;
            StatusText.Text = "GRABANDO";
            StatusText.Foreground = (System.Windows.Media.Brush)FindResource("RecBrush");
            _timer.Start();
            ShowMiniMode();
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

    private Task StopRecordingAsync()
    {
        if (_stopRecordingTask is not null)
            return _stopRecordingTask;

        var recording = _recording;
        if (recording is null)
            return Task.CompletedTask;

        StopButton.IsEnabled = false;
        PauseRecordingButton.IsEnabled = false;
        Log("Cerrando las dos pistas de audio...");

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _stopRecordingTask = completion.Task;
        _ = StopRecordingCoreAsync(recording, completion);
        return _stopRecordingTask;
    }

    private async Task StopRecordingCoreAsync(RecordingSession recording, TaskCompletionSource completion)
    {
        try
        {
            await Task.Run(recording.Stop);
            Log("Grabación finalizada correctamente. Las pistas cifradas están cerradas.");
        }
        catch (Exception exception)
        {
            Log("Error al finalizar la grabación: " + exception.Message);
            WpfMessageBox.Show(exception.Message, "Error al cerrar grabación", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            try { ResetRecordingState(); RefreshRecordings(); }
            finally
            {
                _stopRecordingTask = null;
                completion.TrySetResult();
            }
        }
    }

    private async void Transcribe_Click(object sender, RoutedEventArgs e)
    {
        if (_recording is not null || !_transcriptionUiState.TryBegin()) return;
        if (!File.Exists(WhisperExecutableBox.Text))
        {
            _transcriptionUiState.Complete();
            WpfMessageBox.Show("No se encuentra whisper-cli.exe. Configura el ejecutable oficial de Whisper.cpp.", "Motor Whisper no configurado", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!File.Exists(WhisperModelBox.Text))
        {
            _transcriptionUiState.Complete();
            WpfMessageBox.Show("No se encuentra el modelo Whisper. Selecciona un archivo .bin válido.", "Modelo no configurado", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveSettings(showConfirmation: false);
        _transcriptionCancellation = new CancellationTokenSource();
        ApplyTranscriptionUiState();
        _miniModeTranscriptionStatus = "Preparando transcripción…";
        ShowMiniMode(force: true);
        var task = RunTranscriptionAsync(_transcriptionCancellation.Token);
        _transcriptionTask = task;
        try
        {
            await task;
        }
        finally
        {
            _transcriptionTask = null;
            _transcriptionCancellation?.Dispose();
            _transcriptionCancellation = null;
            _transcriptionUiState.Complete();
            ApplyTranscriptionUiState();
        }
    }

    private async Task RunTranscriptionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var engine = new Transcriptor.Transcription.TranscriptionEngine(
                WhisperExecutableBox.Text.Trim(), WhisperModelBox.Text.Trim(),
                string.IsNullOrWhiteSpace(WhisperLanguageBox.Text) ? "es" : WhisperLanguageBox.Text.Trim());
            var progress = new Progress<string>(message =>
            {
                _miniModeTranscriptionStatus = message;
                Log(message);
            });
            var processed = await engine.ProcessPendingCallsAsync(
                RecordingsPathBox.Text.Trim(), progress, cancellationToken);
            Log($"Proceso integrado finalizado. Sesiones transcritas: {processed}.");
            StatusText.Text = "LISTO";
            RefreshRecordings();
        }
        catch (OperationCanceledException)
        {
            Log("Transcripción cancelada. Se detuvo Whisper y se limpiaron los archivos temporales.");
            StatusText.Text = "CANCELADO";
        }
        catch (Exception exception)
        {
            Log("Error de transcripción: " + exception.Message);
            StatusText.Text = "ERROR";
            WpfMessageBox.Show(exception.Message, "Error de transcripción", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CancelTranscription_Click(object sender, RoutedEventArgs e)
    {
        if (_transcriptionCancellation is null || _transcriptionCancellation.IsCancellationRequested)
            return;

        CancelTranscriptionButton.IsEnabled = false;
        StatusText.Text = "CANCELANDO";
        Log("Solicitud de cancelación enviada a Whisper.");
        _transcriptionCancellation.Cancel();
    }

    private void ApplyTranscriptionUiState()
    {
        var busy = _transcriptionUiState.IsTranscribing;
        StartButton.IsEnabled = !busy && _recording is null;
        TranscribeButton.IsEnabled = !busy && _recording is null;
        CancelTranscriptionButton.IsEnabled = _transcriptionUiState.CanCancel
            && _transcriptionCancellation?.IsCancellationRequested != true;
        BrowseRecordingsButton.IsEnabled = _transcriptionUiState.CanRefreshLibrary;
        RecordingList.IsEnabled = _transcriptionUiState.CanRefreshLibrary;
        MigrateButton.IsEnabled = _transcriptionUiState.CanMigrateSession;
        DeleteSessionButton.IsEnabled = _transcriptionUiState.CanDeleteSession;
        RetentionButton.IsEnabled = _transcriptionUiState.CanDeleteSession;
        RefreshRecordingsButton.IsEnabled = _transcriptionUiState.CanRefreshLibrary;
        RefreshDevicesButton.IsEnabled = !busy && _recording is null;
        SaveSettingsButton.IsEnabled = !busy;
        MicrophoneDeviceBox.IsEnabled = !busy && _recording is null;
        OutputDeviceBox.IsEnabled = !busy && _recording is null;
        ReferenceBox.IsEnabled = !busy && _recording is null;
        ConsentCheckBox.IsEnabled = !busy && _recording is null;
        WhisperExecutableBox.IsEnabled = !busy;
        WhisperModelBox.IsEnabled = !busy;
        WhisperLanguageBox.IsEnabled = !busy;
        RetentionDaysBox.IsEnabled = !busy;
        BrowseWhisperExecutableButton.IsEnabled = !busy;
        BrowseWhisperModelButton.IsEnabled = !busy;
        PlayMicrophoneButton.IsEnabled = !busy;
        PlayClientButton.IsEnabled = !busy;
        StopPlaybackButton.IsEnabled = !busy;
        OpenRecordingFolderButton.IsEnabled = !busy;
    }

    private async void MigrateRecording_Click(object sender, RoutedEventArgs e)
    {
        if (RecordingList.SelectedItem is not RecordingEntry entry) return;
        if (_recording is not null || _transcriptionUiState.IsTranscribing)
        {
            WpfMessageBox.Show("Detén la grabación o espera a que termine la transcripción antes de migrar.", "Sesión ocupada", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var answer = WpfMessageBox.Show(
            "Se crearán y verificarán copias cifradas de los audios y transcripciones. Los originales en claro solo se eliminarán después de verificar todos los archivos. El borrado no es forense. ¿Deseas continuar?",
            "Migrar sesión a cifrado", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        StopPlayback();
        MigrateButton.IsEnabled = false;
        TranscribeButton.IsEnabled = false;
        RecordingList.IsEnabled = false;
        DeleteSessionButton.IsEnabled = false;
        RetentionButton.IsEnabled = false;
        RefreshRecordingsButton.IsEnabled = false;
        try
        {
            var result = await Task.Run(() => _sessionStore.MigrateSession(entry.DirectoryPath));
            Log($"Migración completada: {result.AudioTracksMigrated} pista(s) de audio, {result.TranscriptsMigrated} transcripción(es) cifrada(s), {result.AlreadyEncrypted} archivo(s) ya cifrado(s).");
            RefreshRecordings();
            RecordingList.SelectedItem = (RecordingList.ItemsSource as IEnumerable<RecordingEntry>)?
                .FirstOrDefault(item => string.Equals(item.DirectoryPath, entry.DirectoryPath, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception)
        {
            Log("La migración no se completó; se conservaron los originales que no se pudieron verificar: " + exception.Message);
            WpfMessageBox.Show(exception.Message, "Error de migración", MessageBoxButton.OK, MessageBoxImage.Warning);
            RefreshRecordings();
        }
        finally
        {
            MigrateButton.IsEnabled = true;
            TranscribeButton.IsEnabled = true;
            RecordingList.IsEnabled = true;
            DeleteSessionButton.IsEnabled = true;
            RetentionButton.IsEnabled = true;
            RefreshRecordingsButton.IsEnabled = true;
        }
    }

    private void DeleteRecording_Click(object sender, RoutedEventArgs e)
    {
        if (RecordingList.SelectedItem is not RecordingEntry entry || _recording is not null || _transcriptionUiState.IsTranscribing) return;
        var answer = WpfMessageBox.Show("Se eliminarán permanentemente los audios y transcripciones de esta sesión. Esta acción no se puede deshacer.", "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        try { StopPlayback(); _sessionStore.DeleteSession(RecordingsPathBox.Text, entry.DirectoryPath); Log("Sesión eliminada: " + entry.DirectoryPath); RefreshRecordings(); }
        catch (Exception exception) { Log("No se pudo eliminar la sesión: " + exception.Message); WpfMessageBox.Show(exception.Message, "Error al eliminar", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ApplyRetention_Click(object sender, RoutedEventArgs e)
    {
        if (_recording is not null || _transcriptionUiState.IsTranscribing) return;
        SaveSettings(showConfirmation: false);
        if (_settings.RetentionDays <= 0) { WpfMessageBox.Show("La retención está desactivada. Define un plazo mayor que cero.", "Retención desactivada", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var answer = WpfMessageBox.Show("Se eliminarán las sesiones antiguas según el plazo configurado. Esta acción no se puede deshacer.", "Aplicar retención", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        try { StopPlayback(); var deleted = _sessionStore.ApplyRetention(RecordingsPathBox.Text, _settings.RetentionDays, _clock.UtcNow.UtcDateTime); Log("Sesiones eliminadas por retención: " + deleted.Count); RefreshRecordings(); }
        catch (Exception exception) { Log("Error de retención: " + exception.Message); WpfMessageBox.Show(exception.Message, "Error de retención", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void RefreshRecordings_Click(object sender, RoutedEventArgs e)
    {
        if (_transcriptionUiState.IsTranscribing) return;
        RefreshRecordings();
    }

    private void RefreshRecordings()
    {
        try
        {
            var root = RecordingsPathBox.Text;
            var entries = new List<RecordingEntry>();
            if (Directory.Exists(root))
            {
                foreach (var directory in _sessionStore.GetSessions(root))
                {
                    var microphone = File.Exists(Path.Combine(directory, "yo.trn")) ? Path.Combine(directory, "yo.trn") : Path.Combine(directory, "yo.wav");
                    var client = File.Exists(Path.Combine(directory, "cliente.trn")) ? Path.Combine(directory, "cliente.trn") : Path.Combine(directory, "cliente.wav");
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
        if (_transcriptionUiState.IsTranscribing) return;
        if (RecordingList.SelectedItem is not RecordingEntry entry) return;
        RecordingDetailsText.Text = $"{entry.DirectoryPath}{Environment.NewLine}{(entry.HasMicrophone ? "micrófono disponible" : "micrófono ausente")} · {(entry.HasClient ? "audio del sistema disponible" : "audio del sistema ausente")}";
        if (entry.IsIncomplete)
        {
            RecordingDetailsText.Text += Environment.NewLine + "CAPTURA INCOMPLETA · no se transcribirá automáticamente.";
            TranscriptTextBox.Clear();
            TranscriptStatusText.Text = "Sesión incompleta: revisa las pistas antes de utilizar sus datos.";
            return;
        }
        LoadTranscript(entry);
    }

    private void LoadTranscript(RecordingEntry entry)
    {
        try
        {
            var hasEncryptedTranscript = File.Exists(entry.MergedTranscriptPath)
                || File.Exists(entry.MicrophoneTranscriptPath)
                || File.Exists(entry.ClientTranscriptPath);
            if (hasEncryptedTranscript)
                Transcriptor.Transcription.RecordingAccessControl.RestrictSessionDirectory(entry.DirectoryPath);
            else
                Transcriptor.Transcription.RecordingAccessControl.ValidateSessionDirectory(entry.DirectoryPath);

            var sections = new List<string>();
            if (File.Exists(entry.MergedTranscriptPath) || File.Exists(Path.Combine(entry.DirectoryPath, "transcripcion.srt")))
            {
                sections.Add(ReadTranscript(File.Exists(entry.MergedTranscriptPath) ? entry.MergedTranscriptPath : Path.Combine(entry.DirectoryPath, "transcripcion.srt")));
            }
            else
            {
                if (File.Exists(entry.MicrophoneTranscriptPath) || File.Exists(Path.Combine(entry.DirectoryPath, "yo.srt")))
                {
                    sections.Add("===== YO =====");
                    sections.Add(ReadTranscript(File.Exists(entry.MicrophoneTranscriptPath) ? entry.MicrophoneTranscriptPath : Path.Combine(entry.DirectoryPath, "yo.srt")));
                }
                if (File.Exists(entry.ClientTranscriptPath) || File.Exists(Path.Combine(entry.DirectoryPath, "cliente.srt")))
                {
                    sections.Add("===== CLIENTE =====");
                    sections.Add(ReadTranscript(File.Exists(entry.ClientTranscriptPath) ? entry.ClientTranscriptPath : Path.Combine(entry.DirectoryPath, "cliente.srt")));
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


    private static string ReadTranscript(string path)
    {
        if (!path.EndsWith(".trn", StringComparison.OrdinalIgnoreCase))
            return File.ReadAllText(path);
        var temporary = Transcriptor.Transcription.EncryptedFileOperations.DecryptToTemporaryFile(path);
        try { return File.ReadAllText(temporary); }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
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
        if (_transcriptionUiState.IsTranscribing) return;
        if (RecordingList.SelectedItem is RecordingEntry entry) PlayTrack(entry.MicrophonePath, "micrófono");
    }

    private void PlayClient_Click(object sender, RoutedEventArgs e)
    {
        if (_transcriptionUiState.IsTranscribing) return;
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
            var sessionDirectory = Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException("La pista no tiene carpeta de sesión.");
            var encrypted = path.EndsWith(".trn", StringComparison.OrdinalIgnoreCase);
            if (encrypted)
                Transcriptor.Transcription.RecordingAccessControl.RestrictSessionDirectory(sessionDirectory);
            else
                Transcriptor.Transcription.RecordingAccessControl.ValidateSessionDirectory(sessionDirectory);

            var playablePath = encrypted
                ? Transcriptor.Transcription.EncryptedAudioTrack.DecryptToWaveFile(path) : path;
            if (!string.Equals(playablePath, path, StringComparison.OrdinalIgnoreCase)) _playbackTemporaryPath = playablePath;
            _playbackReader = new AudioFileReader(playablePath);
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
        var playback = _playback;
        var reader = _playbackReader;
        var temporaryPath = _playbackTemporaryPath;
        _playback = null;
        _playbackReader = null;
        _playbackTemporaryPath = null;

        try
        {
            playback?.Dispose();
        }
        catch (Exception exception)
        {
            Log("No se pudo liberar el dispositivo de reproducción: " + exception.Message);
        }

        try
        {
            reader?.Dispose();
        }
        catch (Exception exception)
        {
            Log("No se pudo liberar el lector de audio: " + exception.Message);
        }

        if (temporaryPath is not null)
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch (Exception exception)
            {
                Log("No se pudo eliminar el temporal de reproducción: " + exception.Message);
            }
        }
    }

    private void OpenRecordingFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_transcriptionUiState.IsTranscribing) return;
        if (RecordingList.SelectedItem is RecordingEntry entry)
            Process.Start(new ProcessStartInfo(entry.DirectoryPath) { UseShellExecute = true });
    }

    private void ResetRecordingState()
    {
        _timer.Stop();
        if (_miniModeWindow is not null)
        {
            var mini = _miniModeWindow;
            _miniModeWindow = null;
            mini.CloseFromMain();
            if (!IsVisible)
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            }
        }
        var recording = _recording;
        _recording = null;
        try
        {
            recording?.Dispose();
        }
        catch (Exception exception)
        {
            Log("No se pudieron liberar todos los recursos de captura: " + exception.Message);
        }

        _activeSessionTitle = string.Empty;
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        PauseRecordingButton.IsEnabled = false;
        PauseRecordingButton.Content = "Ⅱ PAUSAR";
        TranscribeButton.IsEnabled = true;
        MicrophoneDeviceBox.IsEnabled = true;
        OutputDeviceBox.IsEnabled = true;
        MicLevel.Value = 0;
        SystemLevel.Value = 0;
        MicLevelText.Text = "En espera";
        SystemLevelText.Text = "En espera";
        StatusText.Text = "LISTO";
        StatusText.Foreground = (System.Windows.Media.Brush)FindResource("LcdBrush");
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

    private void Log(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        ActivityLog.AppendText($"[{_clock.LocalNow:HH:mm:ss}] {message}{Environment.NewLine}");
        ActivityLog.ScrollToEnd();
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_allowClose)
        {
            base.OnClosing(e);
            return;
        }

        if ((_recording is not null || _transcriptionUiState.IsTranscribing) && _miniModeWindow is not null)
        {
            e.Cancel = true;
            SaveSettings(showConfirmation: false);
            if (!_miniModeWindow.IsVisible)
                _miniModeWindow.Show();
            _miniModeWindow.Activate();
            Hide();
            return;
        }

        e.Cancel = true;
        if (_closeRequested)
            return;

        _closeRequested = true;
        try { StopPlayback(); }
        catch (Exception exception) { Log("No se pudo detener la reproducción al cerrar: " + exception.Message); }
        SaveSettings(showConfirmation: false);

        if (_transcriptionTask is not null)
        {
            _transcriptionCancellation?.Cancel();
            try { await _transcriptionTask; }
            catch (Exception exception) { Log("No se pudo cancelar limpiamente la transcripción al cerrar: " + exception.Message); }
        }

        try { await StopRecordingAsync(); }
        catch (Exception exception) { Log("No se pudo cerrar limpiamente la grabación: " + exception.Message); }

        _allowClose = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        StopPlayback();
        if (_miniModeWindow is not null)
        {
            var mini = _miniModeWindow;
            _miniModeWindow = null;
            mini.CloseFromMain();
        }
        base.OnClosed(e);
    }

    private void ShowMiniMode(bool force = false)
    {
        if (_miniModeWindow is null)
            _miniModeWindow = new MiniModeWindow(this);
        if (!_settings.MiniModeAutoShow && !force) return;
        _miniModeWindow.Show();
        _miniModeWindow.WindowState = WindowState.Normal;
        _miniModeWindow.Activate();
    }

    internal MiniModeSnapshot? GetMiniModeSnapshot()
    {
        var recording = _recording;
        if (recording is not null)
            return new MiniModeSnapshot(true, false, !string.IsNullOrWhiteSpace(_miniModeCaptureError),
                recording.Elapsed, recording.MicrophoneLevel, recording.SystemLevel, recording.IsPaused,
                _miniModeCaptureError ?? (recording.IsPaused ? "PAUSADO" : "GRABANDO"), _activeSessionTitle);

        return _transcriptionUiState.IsTranscribing
            ? new MiniModeSnapshot(false, true, false, TimeSpan.Zero, 0, 0, false, _miniModeTranscriptionStatus, string.Empty)
            : null;
    }

    internal void CancelTranscriptionFromMiniMode()
    {
        if (_transcriptionCancellation is { IsCancellationRequested: false })
            _transcriptionCancellation.Cancel();
    }

    internal void CloseMiniModeFromWindow(MiniModeWindow window)
    {
        if (!ReferenceEquals(_miniModeWindow, window)) return;
        _miniModeWindow = null;
        window.CloseFromMain();
        if (!IsVisible)
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }
    }

    private void PauseRecording_Click(object sender, RoutedEventArgs e)
    {
        if (_recording?.IsPaused == true)
            ResumeRecordingFromMiniMode();
        else
            PauseRecordingFromMiniMode();
    }

    internal void PauseRecordingFromMiniMode()
    {
        if (_stopRecordingTask is not null) return;
        _recording?.Pause();
        if (_recording is not null)
        {
            StatusText.Text = "PAUSADO";
            PauseRecordingButton.Content = "▶ REANUDAR";
        }
    }

    internal void ResumeRecordingFromMiniMode()
    {
        if (_stopRecordingTask is not null) return;
        _recording?.Resume();
        if (_recording is not null)
        {
            StatusText.Text = "GRABANDO";
            PauseRecordingButton.Content = "Ⅱ PAUSAR";
        }
    }

    internal Task StopRecordingFromMiniModeAsync() => StopRecordingAsync();

    internal void UpdateMiniModePreferences(bool alwaysOnTop, double left, double top, double width, double height)
    {
        _settings = _settings with
        {
            MiniModeAlwaysOnTop = alwaysOnTop,
            MiniModeLeft = left,
            MiniModeTop = top,
            MiniModeWidth = width,
            MiniModeHeight = height
        };
        try { _settingsStore.Save(_settings); }
        catch (Exception exception) { Log("No se pudieron guardar las preferencias de Mini-Mode: " + exception.Message); }
    }
}
