using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Transcriptor.Transcription;

namespace Transcriptor.UI.ViewModels;

/// <summary>
/// Interacciones que por naturaleza pertenecen a la vista (diálogos modales y
/// selectores de archivos del sistema). El ViewModel las consume como puerto;
/// la ventana concreta las implementa.
/// </summary>
public interface IUiHost
{
    Task<bool> ConfirmAsync(string title, string message);
    Task<string?> PickFolderAsync(string title);
    Task<string?> PickFileAsync(string title, string? pattern);
}

public sealed record LibraryEntry : ILibraryEntry
{
    public LibraryEntry(
        string title, string directoryPath, string microphonePath, string clientPath,
        bool hasTranscript, bool isIncomplete)
    {
        Title = title;
        DirectoryPath = directoryPath;
        MicrophonePath = microphonePath;
        ClientPath = clientPath;
        HasTranscript = hasTranscript;
        IsIncomplete = isIncomplete;
    }

    public string Title { get; }
    public string DirectoryPath { get; }
    public string MicrophonePath { get; }
    public string ClientPath { get; }
    public bool HasTranscript { get; }
    public bool IsIncomplete { get; }
    public bool HasMicrophone => File.Exists(MicrophonePath);
    public bool HasClient => File.Exists(ClientPath);
    public string Summary => $"{(HasMicrophone ? "MIC ✓" : "MIC —")} · {(HasClient ? "SISTEMA ✓" : "SISTEMA —")}"
        + (HasTranscript ? " · TRANSCRIPCIÓN" : "") + (IsIncomplete ? " · INCOMPLETA" : "");
}

/// <summary>
/// Estado y orquestación de toda la ventana principal: captura, biblioteca,
/// reproducción, transcripción y ajustes. La vista sololarla plataforma
/// visual: bindings, diálogos y arrastre de ventana.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly UiServices _services;
    private readonly IUiHost _host;
    private readonly SettingsStore _settingsStore;
    private readonly ISessionStore _sessionStore;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly TranscriptionUiState _transcriptionUiState = new();
    private readonly List<LibraryEntry> _allRecordings = new();
    private readonly StringBuilder _log = new();

    private IAudioCaptureSession? _recording;
    private IAudioPlayer? _playbackPlayer;
    private string? _playbackTemporaryPath;
    private TranscriptionSessionLease? _playbackLease;
    private string? _playbackTrackName;
    private CancellationTokenSource? _transcriptionCancellation;
    private AppSettings _settings;
    private bool _suppressSeek;

    public MainViewModel(UiServices services, IUiHost host)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _settingsStore = new SettingsStore(
            services.Paths.SettingsPath, new PhysicalSettingsFileSystem(), SystemClock.Instance, services.Paths);
        _sessionStore = services.CreateSessionStore();

        var loaded = _settingsStore.Load();
        _settings = loaded.Settings;
        RecordingsDirectoryText = _settings.RecordingsDirectory;
        WhisperExecutableText = _settings.WhisperExecutablePath;
        WhisperModelText = _settings.WhisperModelPath;
        WhisperLanguageText = _settings.Language;
        RetentionSettingsText = _settings.RetentionDays.ToString();
        RetentionDaysText = _settings.RetentionDays.ToString();
        ThemeModeIndex = _settings.ThemeMode switch { "Light" => 1, "Dark" => 2, _ => 0 };
        _timer.Tick += (_, _) => OnTimerTick();
        if (!services.Capabilities.SupportsEncryptionAtRest)
        {
            DevelopmentBannerVisible = true;
            Log("MODO DESARROLLO: sin cifrado real ni captura de audio. Datos de muestra locales.");
        }
        if (!string.IsNullOrWhiteSpace(loaded.Warning)) Log(loaded.Warning!);
        Log("Configuración cargada desde " + _settingsStore.SettingsPath);
        ShowLibraryPanel = _settings.ShowLibraryPanel;
        ShowDevicesPanel = _settings.ShowDevicesPanel;
        ShowSettingsPanel = _settings.ShowSettingsPanel;
        ShowLogPanel = _settings.ShowLogPanel;
        // La pestaña inicial siempre debe apuntar a un panel visible.
        WorkspaceSelectedIndex = ShowLibraryPanel ? 0 : ShowDevicesPanel ? 1 : ShowSettingsPanel ? 2 : 3;
    }

    /// <summary>False en modo desarrollo: la aplicación debe avisar que no hay cifrado real.</summary>
    public bool SupportsEncryptionAtRest => _services.Capabilities.SupportsEncryptionAtRest;
    public bool SupportsAudioPlayback => _services.Capabilities.SupportsAudioPlayback;

    // ------------------------------------------------------------------ estado observable
    [ObservableProperty] private bool _developmentBannerVisible;
    [ObservableProperty] private string _statusText = "LISTO";
    [ObservableProperty] private string _elapsedText = "00:00:00";
    [ObservableProperty] private double _micLevel;
    [ObservableProperty] private string _micLevelText = "En espera";
    [ObservableProperty] private double _systemLevel;
    [ObservableProperty] private string _systemLevelText = "En espera";
    [ObservableProperty] private string _transcriptionProgressText = "Configura Whisper en AJUSTES para transcribir";
    [ObservableProperty] private string _lifecycleProgressText = "Migración y eliminación requieren confirmación.";
    [ObservableProperty] private string _libraryCountText = "Biblioteca sin cargar";
    [ObservableProperty] private string _libraryDetailsTitle = "Selecciona una sesión";
    [ObservableProperty] private string _libraryDetailsText = "Selecciona una sesión para ver las pistas reconocidas.";
    [ObservableProperty] private string _playbackCurrentTimeText = "00:00";
    [ObservableProperty] private string _playbackDurationText = "00:00";
    [ObservableProperty] private string _playbackTrackText = "Selecciona una pista para reproducir";
    [ObservableProperty] private double _seekValue;
    [ObservableProperty] private double _seekMaximum = 1;
    [ObservableProperty] private bool _seekEnabled;
    [ObservableProperty] private string _logText = string.Empty;

    [ObservableProperty] private string _recordingsDirectoryText = string.Empty;
    [ObservableProperty] private string _whisperExecutableText = string.Empty;
    [ObservableProperty] private string _whisperModelText = string.Empty;
    [ObservableProperty] private string _whisperLanguageText = "es";
    [ObservableProperty] private string _retentionSettingsText = "0";
    [ObservableProperty] private string _retentionDaysText = "0";
    [ObservableProperty] private string _referenceText = "Llamada";
    [ObservableProperty] private int _themeModeIndex;
    [ObservableProperty] private bool _consentGranted;

    [ObservableProperty] private IReadOnlyList<AudioDeviceOption> _microphones = Array.Empty<AudioDeviceOption>();
    [ObservableProperty] private IReadOnlyList<AudioDeviceOption> _outputs = Array.Empty<AudioDeviceOption>();
    [ObservableProperty] private AudioDeviceOption? _selectedMicrophone;
    [ObservableProperty] private AudioDeviceOption? _selectedOutput;

    [ObservableProperty] private string _librarySearchText = string.Empty;
    [ObservableProperty] private int _libraryFilterIndex;
    [ObservableProperty] private IReadOnlyList<LibraryEntry> _filteredRecordings = Array.Empty<LibraryEntry>();
    [ObservableProperty] private LibraryEntry? _selectedRecording;

    [ObservableProperty] private bool _isTranscribing;
    [ObservableProperty] private bool _isRecordingActive;
    [ObservableProperty] private string _pauseLabel = "Ⅱ PAUSAR";

    [ObservableProperty] private int _workspaceSelectedIndex;
    [ObservableProperty] private bool _showLibraryPanel = true;
    [ObservableProperty] private bool _showDevicesPanel = true;
    [ObservableProperty] private bool _showSettingsPanel = true;
    [ObservableProperty] private bool _showLogPanel = true;

    /// <summary>Sesiones habladas por operaciones de biblioteca (transcripción o bloqueo).</summary>
    public bool CanInteract => !IsTranscribing;
    /// <summary>Los botones destructivos de biblioteca valen para sesiones o retención.</summary>
    public bool CanUseLibraryOperations => CanInteract;
    public bool CanStartRecording => CanInteract && !IsRecordingActive;
    public bool CanStopRecording => IsRecordingActive;
    public bool RecordingControlsEnabled => IsRecordingActive;

    private void RaiseCanInteract()
    {
        OnPropertyChanged(nameof(CanInteract));
        OnPropertyChanged(nameof(CanUseLibraryOperations));
        OnPropertyChanged(nameof(CanStartRecording));
        OnPropertyChanged(nameof(CanStopRecording));
        OnPropertyChanged(nameof(RecordingControlsEnabled));
    }

    // ------------------------------------------------------------------ ciclo de vida
    public void OnTimerTick()
    {
        if (_recording is { } recording)
        {
            var elapsed = recording.Elapsed;
            ElapsedText = $"{(long)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            MicLevel = Math.Clamp(recording.MicrophonePeak * 100, 0, 100);
            SystemLevel = Math.Clamp(recording.SystemPeak * 100, 0, 100);
            MicLevelText = MicLevel.ToString("F0") + "%";
            SystemLevelText = SystemLevel.ToString("F0") + "%";
            if (recording.IsPaused)
                StatusText = "PAUSADO";
        }
        if (_playbackPlayer is { } player)
        {
            _suppressSeek = true;
            try
            {
                SeekValue = Math.Clamp(player.Position.TotalSeconds, 0, Math.Max(1, SeekMaximum));
            }
            finally { _suppressSeek = false; }
            PlaybackCurrentTimeText = FormatPlaybackTime(player.Position);
        }
    }

    public void OnWindowClosed()
    {
        _timer.Stop();
        _transcriptionCancellation?.Cancel();
        StopPlayback();
        if (_recording is { } recording)
        {
            try { recording.Stop(); }
            catch (Exception ex) { Log("Error al cerrar captura: " + ex.Message); }
            finally { recording.Dispose(); _recording = null; }
        }
    }

    [RelayCommand]
    private void RefreshDevices()
    {
        try
        {
            var microphones = _services.Devices.GetCaptureDevices();
            var outputs = _services.Devices.GetOutputDevices();
            Microphones = microphones;
            Outputs = outputs;
            var micId = _settings.MicrophoneDeviceId ?? _services.Devices.GetDefaultCaptureDeviceId();
            var outputId = _settings.OutputDeviceId ?? _services.Devices.GetDefaultOutputDeviceId();
            SelectedMicrophone = microphones.FirstOrDefault(x => x.Id == micId) ?? microphones.FirstOrDefault();
            SelectedOutput = outputs.FirstOrDefault(x => x.Id == outputId) ?? outputs.FirstOrDefault();
            Log($"Dispositivos detectados: {microphones.Count} entradas y {outputs.Count} salidas.");
        }
        catch (Exception ex) { Log("No se pudieron enumerar los dispositivos: " + ex.Message); }
    }

    // ------------------------------------------------------------------ paneles del espacio de trabajo
    [RelayCommand]
    private void ToggleLibraryPanel() => ToggleWorkspacePanel(0);
    [RelayCommand]
    private void ToggleDevicesPanel() => ToggleWorkspacePanel(1);
    [RelayCommand]
    private void ToggleSettingsPanel() => ToggleWorkspacePanel(2);
    [RelayCommand]
    private void ToggleLogPanel() => ToggleWorkspacePanel(3);

    private void ToggleWorkspacePanel(int index)
    {
        var visible = new[] { ShowLibraryPanel, ShowDevicesPanel, ShowSettingsPanel, ShowLogPanel };
        if (visible[index] && visible.Count(one => one) == 1) return; // Siempre permanece al menos un panel.
        var next = !visible[index];
        switch (index)
        {
            case 0: ShowLibraryPanel = next; break;
            case 1: ShowDevicesPanel = next; break;
            case 2: ShowSettingsPanel = next; break;
            default: ShowLogPanel = next; break;
        }

        visible[index] = next;
        if (next) WorkspaceSelectedIndex = index;
        else if (WorkspaceSelectedIndex == index)
        {
            for (var candidate = 0; candidate < visible.Length; candidate++)
            {
                if (!visible[candidate]) continue;
                WorkspaceSelectedIndex = candidate;
                break;
            }
        }

        try
        {
            _settings = _settings with
            {
                ShowLibraryPanel = visible[0],
                ShowDevicesPanel = visible[1],
                ShowSettingsPanel = visible[2],
                ShowLogPanel = visible[3]
            };
            _settingsStore.Save(_settings);
        }
        catch (Exception ex) { Log("No se pudo guardar la distribución de paneles: " + ex.Message); }
    }

    // ------------------------------------------------------------------ grabación
    [RelayCommand]
    private void StartRecording()
    {
        if (_recording is not null) return;
        if (!ConsentGranted)
        {
            StatusText = "CONSENTIMIENTO REQUERIDO";
            Log("Inicio bloqueado: falta confirmar autorización de todas las personas.");
            return;
        }
        if (SelectedMicrophone is null || SelectedOutput is null)
        {
            StatusText = "SELECCIONA DISPOSITIVOS";
            return;
        }
        var recordingsDirectory = (RecordingsDirectoryText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(recordingsDirectory) || !Directory.Exists(recordingsDirectory))
        {
            StatusText = "CONFIGURA CARPETA";
            Log("Inicio bloqueado: la carpeta de grabaciones no está configurada o no existe.");
            return;
        }

        try
        {
            SaveSettingsCore(includeLibraryRefresh: false);
            var directory = RecordingDirectoryFactory.Create(recordingsDirectory, ReferenceText ?? string.Empty, _services.Clock);
            _services.Security.RestrictSessionDirectory(directory);
            var session = _services.CaptureFactory.Create(directory, SelectedMicrophone.Id, SelectedOutput.Id);
            session.Faulted += ex => Dispatcher.UIThread.Post(() =>
            {
                StatusText = "ERROR DE GRABACIÓN";
                Log("Fallo de captura: " + ex.Message);
                StopRecordingCommand.NotifyCanExecuteChanged();
            });
            try { session.Start(); _recording = session; }
            catch { session.Dispose(); throw; }

            IsRecordingActive = true;
            PauseLabel = "Ⅱ PAUSAR";
            StatusText = "GRABANDO";
            _timer.Start();
            Log("Grabación iniciada. Micrófono: " + session.MicrophoneDeviceName + "; salida: " + session.SystemDeviceName);
            Log("Guardando pistas en: " + directory + (SupportsEncryptionAtRest ? " (cifradas)" : " (SIN cifrado real: modo desarrollo)"));
        }
        catch (Exception ex)
        {
            StatusText = "ERROR";
            Log("No se pudo iniciar: " + ex.Message);
            ResetRecordingState();
        }
    }

    [RelayCommand]
    private void PauseRecording()
    {
        if (_recording is null) return;
        try
        {
            if (_recording.IsPaused)
            {
                _recording.Resume();
                PauseLabel = "Ⅱ PAUSAR";
                StatusText = "GRABANDO";
            }
            else
            {
                _recording.Pause();
                PauseLabel = "▶ REANUDAR";
                StatusText = "PAUSADO";
            }
        }
        catch (Exception ex) { Log("No se pudo cambiar el estado: " + ex.Message); }
    }

    [RelayCommand]
    private async Task StopRecordingAsync()
    {
        var recording = _recording;
        if (recording is null) return;
        Log("Cerrando ambas pistas...");
        try { await Task.Run(recording.Stop); Log("Grabación finalizada. Ambas pistas quedaron cerradas."); }
        catch (Exception ex) { Log("Error al finalizar: " + ex.Message); }
        finally
        {
            recording.Dispose();
            _recording = null;
            IsRecordingActive = false;
            ResetRecordingState();
            RefreshLibrary();
        }
    }

    private void ResetRecordingState()
    {
        _timer.Stop();
        ReferenceText = "Llamada";
        ConsentGranted = false;
        ElapsedText = "00:00:00";
        MicLevel = 0;
        SystemLevel = 0;
        MicLevelText = "En espera";
        SystemLevelText = "En espera";
        StatusText = "LISTO";
        RaiseCanInteract();
    }

    // ------------------------------------------------------------------ biblioteca
    [RelayCommand]
    private void RefreshLibrary()
    {
        if (_transcriptionUiState.IsTranscribing) return;
        try
        {
            var root = (RecordingsDirectoryText ?? string.Empty).Trim();
            _allRecordings.Clear();
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            {
                foreach (var directory in _sessionStore.GetSessions(root))
                {
                    var micEncrypted = Path.Combine(directory, "yo.trn");
                    var clientEncrypted = Path.Combine(directory, "cliente.trn");
                    var micPlain = Path.Combine(directory, "yo.wav");
                    var clientPlain = Path.Combine(directory, "cliente.wav");
                    var mic = File.Exists(micEncrypted) ? micEncrypted : micPlain;
                    var client = File.Exists(clientEncrypted) ? clientEncrypted : clientPlain;
                    var parent = Directory.GetParent(directory)?.Name;
                    var title = string.IsNullOrWhiteSpace(parent) ? Path.GetFileName(directory) : $"{parent} · {Path.GetFileName(directory)}";
                    var hasTranscript = new[] { "transcripcion.srt.trn", "yo.srt.trn", "cliente.srt.trn", "transcripcion.srt", "yo.srt", "cliente.srt" }
                        .Any(name => File.Exists(Path.Combine(directory, name)));
                    _allRecordings.Add(new LibraryEntry(title, directory, mic, client, hasTranscript, RecordingIntegrity.IsIncomplete(directory)));
                }
            }

            _allRecordings.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(b.DirectoryPath, a.DirectoryPath));
            var selectedPath = SelectedRecording?.DirectoryPath;
            ApplyLibraryFilters();
            SelectedRecording = selectedPath is null
                ? SelectedRecording
                : FilteredRecordings.FirstOrDefault(entry => string.Equals(entry.DirectoryPath, selectedPath, StringComparison.OrdinalIgnoreCase));
            Log($"Biblioteca actualizada: {_allRecordings.Count} sesión(es) reconocida(s).");
        }
        catch (Exception ex)
        {
            LibraryCountText = "No se pudo cargar la biblioteca.";
            LibraryDetailsTitle = "Error de biblioteca";
            LibraryDetailsText = ex.Message;
            Log("No se pudo actualizar la biblioteca: " + ex.Message);
        }
    }

    partial void OnLibrarySearchTextChanged(string value) => ApplyLibraryFilters();
    partial void OnLibraryFilterIndexChanged(int value) => ApplyLibraryFilters();

    partial void OnSelectedRecordingChanged(LibraryEntry? value)
    {
        if (_playbackPlayer is not null) StopPlayback();
        if (value is null)
        {
            LibraryDetailsTitle = "Selecciona una sesión";
            LibraryDetailsText = "La biblioteca muestra sesiones locales reconocidas.";
            return;
        }

        LibraryDetailsTitle = value.Title;
        LibraryDetailsText = $"{value.Summary}{Environment.NewLine}{value.DirectoryPath}";
        if (value.IsIncomplete)
            LibraryDetailsText += Environment.NewLine + "Captura incompleta: no debe transcribirse automáticamente.";
    }

    private void ApplyLibraryFilters()
    {
        var selectedPath = SelectedRecording?.DirectoryPath;
        FilteredRecordings = LibraryFilter.Apply(_allRecordings, LibrarySearchText, LibraryFilterIndex);
        LibraryCountText = $"{FilteredRecordings.Count} de {_allRecordings.Count} sesión(es)";
        if (selectedPath is not null)
            SelectedRecording = FilteredRecordings.FirstOrDefault(entry =>
                string.Equals(entry.DirectoryPath, selectedPath, StringComparison.OrdinalIgnoreCase))
                ?? SelectedRecording;
    }

    // ------------------------------------------------------------------ transcripción
    [RelayCommand]
    private async Task TranscribePendingAsync()
    {
        if (_recording is not null)
        {
            Log("Transcripción bloqueada: primero finaliza la grabación activa.");
            return;
        }
        StopPlayback();
        if (!_transcriptionUiState.TryBegin()) return;
        IsTranscribing = true;
        RaiseCanInteract();
        CancelTranscriptionCommand.NotifyCanExecuteChanged();

        var root = (RecordingsDirectoryText ?? string.Empty).Trim();
        var executable = (WhisperExecutableText ?? string.Empty).Trim();
        var model = (WhisperModelText ?? string.Empty).Trim();
        var languageText = (WhisperLanguageText ?? string.Empty).Trim();
        var language = string.IsNullOrWhiteSpace(languageText) ? "es" : languageText;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)
            || string.IsNullOrWhiteSpace(executable) || string.IsNullOrWhiteSpace(model))
        {
            _transcriptionUiState.Complete();
            IsTranscribing = false;
            RaiseCanInteract();
            CancelTranscriptionCommand.NotifyCanExecuteChanged();
            TranscriptionProgressText = "Configura una carpeta existente, whisper-cli y el modelo en AJUSTES.";
            Log("Transcripción no iniciada: faltan rutas válidas de grabaciones, Whisper o modelo.");
            return;
        }

        _transcriptionCancellation = new CancellationTokenSource();
        TranscriptionProgressText = "Preparando transcripción…";
        Log("Iniciando transcripción local de sesiones pendientes.");
        var progress = new Progress<string>(message =>
        {
            TranscriptionProgressText = message;
            Log(message);
        });
        try
        {
            var engine = _services.CreateTranscriptionEngine(executable, model, language);
            var processed = await engine.ProcessPendingCallsAsync(root, progress, _transcriptionCancellation.Token);
            TranscriptionProgressText = $"Proceso finalizado. Sesiones transcritas: {processed}.";
            Log($"Transcripción finalizada. Sesiones procesadas: {processed}.");
        }
        catch (OperationCanceledException)
        {
            TranscriptionProgressText = "Transcripción cancelada. Los originales se conservaron.";
            Log("Transcripción cancelada por el usuario.");
        }
        catch (Exception ex)
        {
            TranscriptionProgressText = "La transcripción terminó con errores. Consulta el registro.";
            Log("Error de transcripción: " + ex.Message);
        }
        finally
        {
            _transcriptionCancellation.Dispose();
            _transcriptionCancellation = null;
            _transcriptionUiState.Complete();
            IsTranscribing = false;
            RaiseCanInteract();
            CancelTranscriptionCommand.NotifyCanExecuteChanged();
            RefreshLibrary();
        }
    }

    [RelayCommand]
    private void CancelTranscription() => _transcriptionCancellation?.Cancel();

    // ------------------------------------------------------------------ migración / eliminación / retención
    [RelayCommand]
    private async Task MigrateSessionAsync()
    {
        if (SelectedRecording is not { } entry || !_transcriptionUiState.TryBegin()) return;
        if (!await _host.ConfirmAsync("Migrar sesión a cifrado",
            "Se crearán y verificarán copias cifradas. Los originales en claro solo se eliminarán después de verificar todos los archivos. El borrado no es forense. ¿Continuar?"))
        {
            _transcriptionUiState.Complete();
            return;
        }
        StopPlayback();
        LifecycleProgressText = "Migrando y verificando archivos…";
        try
        {
            var result = await Task.Run(() => _sessionStore.MigrateSession(entry.DirectoryPath));
            var completed = $"Migración completada: {result.AudioTracksMigrated} pistas, {result.TranscriptsMigrated} transcripciones, {result.AlreadyEncrypted} ya cifrados.";
            LifecycleProgressText = completed;
            Log(completed);
        }
        catch (Exception ex)
        {
            LifecycleProgressText = "Migración incompleta; revisa el registro. Los originales no verificados se conservaron.";
            Log("Error de migración: " + ex.Message);
        }
        finally
        {
            _transcriptionUiState.Complete();
            RefreshLibrary();
            SelectLibraryEntry(entry.DirectoryPath);
        }
    }

    [RelayCommand]
    private async Task DeleteSessionAsync()
    {
        if (SelectedRecording is not { } entry || !_transcriptionUiState.TryBegin()) return;
        if (!await _host.ConfirmAsync("Eliminar sesión",
            "Se eliminarán permanentemente los audios y transcripciones de esta sesión. Esta acción no se puede deshacer. ¿Continuar?"))
        {
            _transcriptionUiState.Complete();
            return;
        }
        StopPlayback();
        try
        {
            var root = (RecordingsDirectoryText ?? string.Empty).Trim();
            await Task.Run(() => _sessionStore.DeleteSession(root, entry.DirectoryPath));
            LifecycleProgressText = "Sesión eliminada.";
            Log("Sesión eliminada: " + entry.Title);
        }
        catch (Exception ex)
        {
            LifecycleProgressText = "No se pudo eliminar la sesión.";
            Log("Error al eliminar sesión: " + ex.Message);
        }
        finally
        {
            _transcriptionUiState.Complete();
            RefreshLibrary();
        }
    }

    [RelayCommand]
    private async Task ApplyRetentionAsync()
    {
        if (!_transcriptionUiState.TryBegin()) return;
        var days = int.TryParse(RetentionDaysText, out var parsed) ? Math.Clamp(parsed, 0, 3650) : 0;
        if (days <= 0)
        {
            _transcriptionUiState.Complete();
            LifecycleProgressText = "Retención desactivada. Define entre 1 y 3650 días.";
            return;
        }
        if (!await _host.ConfirmAsync("Aplicar retención",
            $"Se eliminarán las sesiones anteriores a {days} días. La operación no se puede deshacer. ¿Continuar?"))
        {
            _transcriptionUiState.Complete();
            return;
        }
        StopPlayback();
        try
        {
            _settings = _settings with { RetentionDays = days };
            RetentionSettingsText = days.ToString();
            _settingsStore.Save(_settings);
            var root = (RecordingsDirectoryText ?? string.Empty).Trim();
            var deleted = await Task.Run(() => _sessionStore.ApplyRetention(root, days, _services.Clock.UtcNow.UtcDateTime));
            var retentionMessage = $"Retención aplicada: {deleted.Count} sesión(es) eliminadas.";
            LifecycleProgressText = retentionMessage;
            Log(retentionMessage);
        }
        catch (Exception ex)
        {
            LifecycleProgressText = "No se pudo completar la retención.";
            Log("Error de retención: " + ex.Message);
        }
        finally
        {
            _transcriptionUiState.Complete();
            RefreshLibrary();
        }
    }

    private void SelectLibraryEntry(string path)
    {
        SelectedRecording = FilteredRecordings.FirstOrDefault(item =>
            string.Equals(item.DirectoryPath, path, StringComparison.OrdinalIgnoreCase))
            ?? _allRecordings.FirstOrDefault(item =>
                string.Equals(item.DirectoryPath, path, StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------ reproducción
    [RelayCommand]
    private void PlayMicrophone()
    {
        if (_transcriptionUiState.IsTranscribing) return;
        if (SelectedRecording is { } entry) PlayTrack(entry.MicrophonePath, "micrófono");
    }

    [RelayCommand]
    private void PlaySystem()
    {
        if (_transcriptionUiState.IsTranscribing) return;
        if (SelectedRecording is { } entry) PlayTrack(entry.ClientPath, "audio del sistema");
    }

    private void PlayTrack(string path, string trackName)
    {
        if (!SupportsAudioPlayback)
        {
            Log("La reproducción no está disponible en esta plataforma.");
            return;
        }
        if (!File.Exists(path))
        {
            Log("Pista no disponible: " + path);
            LibraryDetailsText = "No se encuentra el archivo de audio seleccionado.";
            return;
        }

        try
        {
            StopPlayback();
            var sessionDirectory = Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException("La pista no tiene carpeta de sesión.");
            var encrypted = path.EndsWith(".trn", StringComparison.OrdinalIgnoreCase);
            if (encrypted)
                _services.Security.RestrictSessionDirectory(sessionDirectory);
            else
                _services.Security.ValidateSessionDirectory(sessionDirectory);

            _playbackLease = TranscriptionSessionLease.TryAcquire(sessionDirectory)
                ?? throw new InvalidOperationException("La sesión está siendo transcrita, migrada o modificada por otra operación.");

            var playablePath = encrypted ? EncryptedAudioTrack.DecryptToWaveFile(path) : path;
            if (!string.Equals(playablePath, path, StringComparison.OrdinalIgnoreCase))
                _playbackTemporaryPath = playablePath;

            _playbackPlayer = _services.PlayerFactory.Create(playablePath);
            var duration = _playbackPlayer.Duration;
            _playbackPlayer.Ended += () => Dispatcher.UIThread.Post(ReleasePlayback);
            _playbackPlayer.Failed += ex => Dispatcher.UIThread.Post(() =>
            {
                Log("Error de reproducción: " + ex.Message);
                ReleasePlayback();
            });
            _playbackTrackName = trackName;
            SeekMaximum = Math.Max(1, duration.TotalSeconds);
            _suppressSeek = true;
            try { SeekValue = 0; }
            finally { _suppressSeek = false; }
            SeekEnabled = duration.TotalSeconds > 0;
            PlaybackCurrentTimeText = "00:00";
            PlaybackDurationText = FormatPlaybackTime(duration);
            PlaybackTrackText = $"REPRODUCIENDO · {trackName.ToUpperInvariant()}";

            _timer.Start();
            Log($"Reproduciendo pista de {trackName}: {path}");
        }
        catch (Exception ex)
        {
            ReleasePlayback();
            Log("No se pudo reproducir la pista: " + ex.Message);
            LibraryDetailsText = "Error de reproducción: " + ex.Message;
        }
    }

    /// <summary>Mueve el slider al valor cambiado por el usuario (los cambios programáticos usan _suppressSeek).</summary>
    partial void OnSeekValueChanged(double value)
    {
        if (_suppressSeek) return;
        if (value < 0 || value > SeekMaximum) return;
        _playbackPlayer?.Seek(TimeSpan.FromSeconds(Math.Clamp(value, 0, Math.Max(1, _playbackPlayer.Duration.TotalSeconds))));
    }

    private static string FormatPlaybackTime(TimeSpan time)
    {
        var totalSeconds = Math.Max(0, (long)time.TotalSeconds);
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    [RelayCommand]
    private void StopPlayback()
    {
        try { _playbackPlayer?.Dispose(); }
        finally { ReleasePlayback(); }
    }

    private void ReleasePlayback()
    {
        var player = _playbackPlayer;
        var temporaryPath = _playbackTemporaryPath;
        var lease = _playbackLease;
        _playbackPlayer = null;
        _playbackTemporaryPath = null;
        _playbackLease = null;
        _playbackTrackName = null;
        SeekEnabled = false;
        _suppressSeek = true;
        try { SeekValue = 0; }
        finally { _suppressSeek = false; }
        PlaybackCurrentTimeText = "00:00";
        PlaybackDurationText = "00:00";
        PlaybackTrackText = "Selecciona una pista para reproducir";

        try { player?.Dispose(); }
        catch (Exception ex) { Log("No se pudo liberar el dispositivo de reproducción: " + ex.Message); }
        if (temporaryPath is not null)
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (Exception ex) { Log("No se pudo eliminar el temporal de reproducción: " + ex.Message); }
        }
        lease?.Dispose();
        if (_recording is null) _timer.Stop();
    }

    // ------------------------------------------------------------------ ajustes
    [RelayCommand]
    private async Task BrowseRecordingsFolderAsync()
    {
        var path = await _host.PickFolderAsync("Carpeta de grabaciones");
        if (path is not null) RecordingsDirectoryText = path;
    }

    [RelayCommand]
    private async Task BrowseWhisperExecutableAsync()
    {
        var path = await _host.PickFileAsync("Seleccionar Whisper CLI", _services.Paths.WhisperExecutablePickerPattern);
        if (path is not null) WhisperExecutableText = path;
    }

    [RelayCommand]
    private async Task BrowseWhisperModelAsync()
    {
        var path = await _host.PickFileAsync("Seleccionar modelo Whisper", "*.bin");
        if (path is not null) WhisperModelText = path;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        try { SaveSettingsCore(includeLibraryRefresh: true); }
        catch (Exception ex) { Log("No se pudo guardar la configuración: " + ex.Message); }
    }

    private void SaveSettingsCore(bool includeLibraryRefresh)
    {
        _settings = _settings with
        {
            RecordingsDirectory = (RecordingsDirectoryText ?? string.Empty).Trim(),
            WhisperExecutablePath = (WhisperExecutableText ?? string.Empty).Trim(),
            WhisperModelPath = (WhisperModelText ?? string.Empty).Trim(),
            Language = string.IsNullOrWhiteSpace(WhisperLanguageText) ? "es" : WhisperLanguageText.Trim(),
            RetentionDays = int.TryParse(RetentionSettingsText, out var retentionDays) ? Math.Clamp(retentionDays, 0, 3650) : 0,
            ThemeMode = ThemeModeIndex switch { 1 => "Light", 2 => "Dark", _ => "System" },
            MicrophoneDeviceId = SelectedMicrophone?.Id,
            OutputDeviceId = SelectedOutput?.Id
        };
        _settingsStore.Save(_settings);
        _services.ApplyTheme(_settings.ThemeMode);
        Log("Configuración guardada.");
        if (includeLibraryRefresh) RefreshLibrary();
    }

    // ------------------------------------------------------------------ registro
    private void Log(string message)
    {
        _log.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        LogText = _log.ToString();
    }
}
