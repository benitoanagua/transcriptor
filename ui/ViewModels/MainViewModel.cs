using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Transcriptor.Transcription;

namespace Transcriptor.UI.ViewModels;

/// <summary>
/// Shell delgado de la ventana principal: compone los seis sub-ViewModels,
/// posee el espacio de trabajo (paneles visibles, pestaña activa, banner de
/// desarrollo) y coordina ciclo de vida (cierre seguro, ticks del temporizador
/// que la vista ordena). Toda la lógica de dominio vive en los hijos:
/// <see cref="RecorderViewModel"/>, <see cref="LibraryViewModel"/>,
/// <see cref="SessionDetailViewModel"/>, <see cref="TranscriptViewModel"/>,
/// <see cref="SettingsViewModel"/> y <see cref="LogViewModel"/>.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    public MainViewModel(UiServices services, IUiHost host, Action<Action> uiDispatch)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _uiDispatch = uiDispatch ?? throw new ArgumentNullException(nameof(uiDispatch));

        var settingsStore = new SettingsStore(
            services.Paths.SettingsPath, new PhysicalSettingsFileSystem(), services.Clock, services.Paths);
        var load = settingsStore.Load();
        _settingsStore = settingsStore;
        _settings = load.Settings;

        Log = new LogViewModel(host);
        Recorder = new RecorderViewModel(services, State, Log, uiDispatch);
        Library = new LibraryViewModel(services, State, Log);
        Detail = new SessionDetailViewModel(services, State, Log, host, Library, RecordingsDirectory);
        Transcript = new TranscriptViewModel(services, State, Log, host, uiDispatch);
        Settings = new SettingsViewModel(
            load.Settings, services, host, Log, SaveSettingsSnapshot,
            () => (Recorder.SelectedMicrophone?.Id, Recorder.SelectedOutput?.Id),
            State, () => Recorder.IsRecordingActive);
        IsLibraryPanelVisible = load.Settings.LibraryPanelVisible;

        Library.SetRecordingsDirectorySource(RecordingsDirectory);
        Library.SelectionChanged += _ => OnPropertyChanged(nameof(CurrentSessionPath));
        Transcript.AttachLibrary(Library);
        Transcript.IsRecordingActive = () => Recorder.IsRecordingActive;
        Transcript.SeekTo = Detail.SeekTo;
        Recorder.RecordingStateChanged += Transcript.NotifyRecordingStateChanged;
        Recorder.RecordingStateChanged += Settings.NotifyRecordingStateChanged;
        Recorder.RecordingStateChanged += NotifySessionActionCommands;
        State.Changed += NotifySessionActionCommands;
        Recorder.RecordingStoppedSuccessfully += OnRecordingStoppedSuccessfullyAsync;
        Transcript.PathsSnapshot = () =>
        {
            var snapshot = Settings.Snapshot();
            return (snapshot.RecordingsDirectory, snapshot.WhisperExecutablePath, snapshot.WhisperModelPath, snapshot.Language);
        };
        Recorder.SetRecordingsDirectory(Settings.RecordingsDirectoryText);
        Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.RecordingsDirectoryText))
            {
                Recorder.SetRecordingsDirectory(Settings.RecordingsDirectoryText);
                RefreshShellStatus();
            }
        };
        Recorder.PropertyChanged += (_, _) => RefreshShellStatus();
        Transcript.PropertyChanged += (_, _) => RefreshShellStatus();
        RefreshShellStatus();

        DevelopmentBannerVisible = !services.Capabilities.SupportsEncryptionAtRest;
        if (DevelopmentBannerVisible)
            Log.Log("MODO DESARROLLO: sin cifrado real ni captura de audio. Datos de muestra locales.");
        if (!string.IsNullOrWhiteSpace(load.Warning)) Log.Log(load.Warning);
        Log.Log("Configuración cargada desde " + settingsStore.SettingsPath);
        var removedPlaybackTemporaries = PlaybackTemporaryCleanup.Sweep(
            services.CreateSessionStore(), Settings.RecordingsDirectoryText, Log);
        if (removedPlaybackTemporaries > 0)
            Log.Log($"Se limpiaron {removedPlaybackTemporaries} temporal(es) huérfano(s) de reproducción.");

        Library.RefreshLibraryCommand.Execute(null);
    }

    private readonly UiServices _services;
    private readonly IUiHost _host;
    private readonly Action<Action> _uiDispatch;
    private readonly SettingsStore _settingsStore;
    private AppSettings _settings;

    public TranscriptionUiState State { get; } = new();
    public RecorderViewModel Recorder { get; }
    public LibraryViewModel Library { get; }
    public SessionDetailViewModel Detail { get; }
    public TranscriptViewModel Transcript { get; }
    public SettingsViewModel Settings { get; }
    public LogViewModel Log { get; }

    [ObservableProperty] private bool _developmentBannerVisible;
    [ObservableProperty] private int _selectedNavigationIndex;
    [ObservableProperty] private bool _isLibraryPanelVisible = true;
    [ObservableProperty] private string _statusChipText = "LISTO";
    [ObservableProperty] private string _encryptionStatusText = string.Empty;
    [ObservableProperty] private string _diskStatusText = string.Empty;
    [ObservableProperty] private string _whisperStatusText = string.Empty;
    [ObservableProperty] private bool _isWhisperAvailable;
    [ObservableProperty] private string _operationStatusText = string.Empty;

    public string CurrentPageTitle => SelectedNavigationIndex switch
    {
        0 => "Estudio",
        1 => "Biblioteca",
        2 => "Ajustes",
        _ => "Registro"
    };
    public bool IsStudioPage => SelectedNavigationIndex == 0;
    public bool IsLibraryPage => SelectedNavigationIndex == 1;
    public bool IsSettingsPage => SelectedNavigationIndex == 2;
    public bool IsLogPage => SelectedNavigationIndex == 3;
    public bool IsIdle => !Recorder.IsRecordingActive && !Transcript.IsTranscribing;
    public bool IsEncryptionAvailable => _services.Capabilities.SupportsEncryptionAtRest;
    public string CurrentSessionPath => Recorder.IsRecordingActive
        ? Recorder.ActiveSessionDirectory ?? string.Empty
        : Library.SelectedRecording?.DirectoryPath ?? string.Empty;
    public string LibraryPanelToggleLabel => IsLibraryPanelVisible ? "Ocultar sesiones" : "Mostrar sesiones";

    [RelayCommand(CanExecute = nameof(CanPlayMicrophoneEntry))]
    private void PlayMicrophoneEntry(LibraryEntry? entry)
    {
        if (entry is null) return;
        Library.SelectedRecording = entry;
        Detail.PlayMicrophoneCommand.Execute(null);
    }

    private bool CanPlayMicrophoneEntry(LibraryEntry? entry) =>
        entry?.HasMicrophone == true && _services.Capabilities.SupportsAudioPlayback && Detail.CanUseLibraryOperations;

    [RelayCommand(CanExecute = nameof(CanPlaySystemEntry))]
    private void PlaySystemEntry(LibraryEntry? entry)
    {
        if (entry is null) return;
        Library.SelectedRecording = entry;
        Detail.PlaySystemCommand.Execute(null);
    }

    private bool CanPlaySystemEntry(LibraryEntry? entry) =>
        entry?.HasClient == true && _services.Capabilities.SupportsAudioPlayback && Detail.CanUseLibraryOperations;

    [RelayCommand(CanExecute = nameof(CanTranscribeEntry))]
    private Task TranscribeEntryAsync(LibraryEntry? entry)
    {
        if (entry is null) return Task.CompletedTask;
        Library.SelectedRecording = entry;
        return Transcript.TranscribeSelectedCommand.ExecuteAsync(null);
    }

    private bool CanTranscribeEntry(LibraryEntry? entry) =>
        entry is { IsIncomplete: false } && Transcript.CanTranscribePending;

    [RelayCommand(CanExecute = nameof(CanDeleteEntry))]
    private async Task DeleteEntryAsync(LibraryEntry? entry)
    {
        if (entry is null) return;
        Library.SelectedRecording = entry;
        await Detail.DeleteSessionCommand.ExecuteAsync(null);
    }

    private bool CanDeleteEntry(LibraryEntry? entry) =>
        entry is not null && Detail.CanUseLibraryOperations;

    private void NotifySessionActionCommands()
    {
        PlayMicrophoneEntryCommand.NotifyCanExecuteChanged();
        PlaySystemEntryCommand.NotifyCanExecuteChanged();
        TranscribeEntryCommand.NotifyCanExecuteChanged();
        DeleteEntryCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedNavigationIndexChanged(int value)
    {
        if (value != 0 && Recorder.IsTestingLevels)
            Recorder.StopLevelTestingCommand.Execute(null);
        OnPropertyChanged(nameof(CurrentPageTitle));
        OnPropertyChanged(nameof(IsStudioPage));
        OnPropertyChanged(nameof(IsLibraryPage));
        OnPropertyChanged(nameof(IsSettingsPage));
        OnPropertyChanged(nameof(IsLogPage));
    }
    partial void OnIsLibraryPanelVisibleChanged(bool value) => OnPropertyChanged(nameof(LibraryPanelToggleLabel));

    [RelayCommand]
    private void ShowRecordingPage() => SelectedNavigationIndex = 0;
    [RelayCommand]
    private void ShowLibraryPage() => SelectedNavigationIndex = 1;
    [RelayCommand]
    private void ShowSettingsPage() => SelectedNavigationIndex = 2;
    [RelayCommand]
    private void ShowLogPage() => SelectedNavigationIndex = 3;
    [RelayCommand]
    private void DismissDevelopmentBanner() => DevelopmentBannerVisible = false;
    [RelayCommand]
    private void ToggleLibraryPanel()
    {
        var visible = !IsLibraryPanelVisible;
        var settings = _settings with { LibraryPanelVisible = visible };
        try { _settingsStore.Save(settings); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Log("No se pudo guardar la visibilidad de la biblioteca: " + exception.Message);
            return;
        }

        _settings = settings;
        Settings.UpdateLibraryPanelVisibility(visible);
        IsLibraryPanelVisible = visible;
    }

    /// <summary>Raíz de grabaciones vigente; la fija el shell desde Settings.</summary>
    private string RecordingsDirectory() => Settings.RecordingsDirectoryText?.Trim() ?? string.Empty;

    private void SaveSettingsSnapshot(AppSettings settings)
    {
        _settings = settings;
        _settingsStore.Save(_settings);
        Recorder.SetRecordingsDirectory(settings.RecordingsDirectory);
        Settings.RetentionSettingsText = settings.RetentionDays.ToString();
        Library.RefreshLibraryCommand.Execute(null);
    }

    private async Task OnRecordingStoppedSuccessfullyAsync(string sessionDirectory)
    {
        await Library.RefreshLibraryCommand.ExecuteAsync(null);
        Library.SelectEntry(sessionDirectory);
        var entry = Library.SelectedRecording;
        if (entry is null)
        {
            Log.Log("La sesión guardada no apareció en la biblioteca; no se iniciará la transcripción automática.");
            return;
        }
        if (entry.IsIncomplete)
        {
            Log.Log("La sesión quedó incompleta y no se transcribirá automáticamente.");
            return;
        }
        if (!Settings.Snapshot().TranscribeAutomaticallyOnStop) return;

        Library.SelectedRecording = entry;
        await Transcript.TranscribeSelectedCommand.ExecuteAsync(null);
    }

    // ------------------------------------------------------------------ ciclo de vida
    /// <summary>Un tick del temporizador de la vista: actualiza captura y reproductor.</summary>
    public void OnTimerTick()
    {
        Recorder.Tick();
        Detail.Tick();
        Transcript.UpdatePlaybackPosition(Detail.CurrentPlaybackPosition);
        RefreshShellStatus();
    }

    private void RefreshShellStatus()
    {
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(CurrentSessionPath));
        StatusChipText = Recorder.IsRecordingActive
            ? $"{(Recorder.IsPaused ? "Ⅱ PAUSADO" : "● GRABANDO")} {Recorder.ElapsedText}"
            : Transcript.IsTranscribing ? "TRANSCRIBIENDO" : "LISTO";
        EncryptionStatusText = _services.Capabilities.SupportsEncryptionAtRest
            ? "Cifrado activo"
            : "Sin cifrar";
        var settings = Settings.Snapshot();
        DiskStatusText = GetFreeSpaceStatus(Settings.RecordingsDirectoryText, settings.MinimumFreeSpaceGb);
        IsWhisperAvailable = File.Exists(settings.WhisperExecutablePath) && File.Exists(settings.WhisperModelPath);
        WhisperStatusText = IsWhisperAvailable
            ? "Whisper: disponible"
            : "Whisper: configuración incompleta";
        OperationStatusText = Transcript.IsTranscribing
            ? Transcript.TranscriptionProgressText
            : Detail.LifecycleProgressText;
    }

    private static string GetFreeSpaceStatus(string path, double warningThresholdGb)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return "Espacio libre: configura carpeta";
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root)) return "Espacio libre: ruta no disponible";
            var drive = new DriveInfo(root);
            if (!drive.IsReady) return "Espacio libre: no disponible";
            var availableGb = drive.AvailableFreeSpace / (1024d * 1024 * 1024);
            return availableGb < warningThresholdGb
                ? $"Poco espacio: {availableGb:0.0} GB (umbral {warningThresholdGb:0.0} GB)"
                : $"Espacio libre: {availableGb:0.0} GB";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return "Espacio libre: no disponible";
        }
    }

    /// <summary>True mientras haya captura o transcripción que exigen decisión antes de cerrar.</summary>
    public bool IsCloseBlocked => Recorder.IsRecordingActive || Transcript.IsCloseBlocked;

    /// <summary>
    /// Ruta de cierre seguro apoyada en el host: pide confirmación de la
    /// grabación o la transcripción en curso, detiene la captura de forma
    /// coordinada (guardando ambas pistas) y cancela la transcripción en
    /// orden esperando su finalización. Devuelve false si el usuario eligió
    /// no continuar, dejando la ventana abierta y las pistas intactas.
    /// </summary>
    public async Task<bool> ConfirmCloseAsync()
    {
        if (Recorder.IsRecordingActive)
        {
            var stopGranted = await _host.ConfirmAsync("Grabación en curso",
                "Hay una grabación en curso. ¿Detener y guardar ambas pistas?");
            if (!stopGranted) return false;
            Log.Log("Cierre solicitado: deteniendo la grabación antes de salir.");
            if (!await Recorder.StopForCloseAsync())
            {
                Log.Log("Cierre cancelado: no se pudieron guardar y cerrar ambas pistas correctamente.");
                return false;
            }
        }
        if (Transcript.IsCloseBlocked)
        {
            var cancelGranted = await _host.ConfirmAsync("Transcripción en curso",
                "Hay una transcripción en curso. ¿Cancelarla y salir? Las pistas sin cerrar se conservan.");
            if (!cancelGranted) return false;
            Log.Log("Cierre solicitado: cancelando la transcripción antes de salir.");
            await Transcript.CancelAndWaitForCloseAsync();
        }
        return true;
    }

    public void OnWindowClosed()
    {
        if (Recorder.IsTestingLevels)
            Recorder.StopLevelTestingCommand.Execute(null);
        Detail.StopPlayback();
        Transcript.CancelTranscriptionCommand.Execute(null);
        Recorder.StopForClose();
    }
}
