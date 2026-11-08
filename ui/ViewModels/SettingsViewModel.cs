using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Transcriptor.UI.ViewModels;

/// <summary>
/// Panel AJUSTES: rutas de trabajo y de Whisper y retención configurada.
/// No toca Avalonia; los selectores de archivos viajan por <see cref="IUiHost"/>.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private AppSettings _settings;
    private readonly UiServices _services;
    private readonly IUiHost _host;
    private readonly ILogSink _log;
    private readonly Action<AppSettings> _save;
    private readonly Func<(string? MicrophoneDeviceId, string? OutputDeviceId)> _deviceIds;
    private readonly TranscriptionUiState _operationState;
    private readonly Func<bool> _isRecordingActive;
    private readonly ISessionStore _sessionStore;
    private string _recordingsDirectoryStatus = string.Empty;
    private string _whisperExecutableStatus = string.Empty;
    private string _whisperModelStatus = string.Empty;

    public SettingsViewModel(
        AppSettings settings, UiServices services, IUiHost host, ILogSink log,
        Action<AppSettings> save, Func<(string? MicrophoneDeviceId, string? OutputDeviceId)> deviceIds,
        TranscriptionUiState operationState, Func<bool> isRecordingActive)
    {
        _settings = settings;
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _save = save ?? throw new ArgumentNullException(nameof(save));
        _deviceIds = deviceIds ?? throw new ArgumentNullException(nameof(deviceIds));
        _operationState = operationState ?? throw new ArgumentNullException(nameof(operationState));
        _isRecordingActive = isRecordingActive ?? throw new ArgumentNullException(nameof(isRecordingActive));
        _sessionStore = services.CreateSessionStore();
        RecordingsDirectoryText = settings.RecordingsDirectory;
        WhisperExecutableText = settings.WhisperExecutablePath;
        WhisperModelText = settings.WhisperModelPath;
        WhisperLanguageText = settings.Language;
        LanguageSelectionIndex = settings.Language switch
        {
            "es" => 0,
            "en" => 1,
            "fr" => 2,
            "de" => 3,
            "pt" => 4,
            "auto" => 5,
            _ => 6
        };
        RetentionSettingsText = settings.RetentionDays.ToString();
        MinimumFreeSpaceGbText = settings.MinimumFreeSpaceGb.ToString(System.Globalization.CultureInfo.CurrentCulture);
        TranscribeAutomaticallyOnStop = settings.TranscribeAutomaticallyOnStop;
        DensityCompact = settings.DensityCompact;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(IsDirty))
                OnPropertyChanged(nameof(IsDirty));
            if (e.PropertyName == nameof(RecordingsDirectoryText))
                OnPropertyChanged(nameof(RecordingsDirectoryStatus));
            if (e.PropertyName == nameof(WhisperExecutableText))
                OnPropertyChanged(nameof(WhisperExecutableStatus));
            if (e.PropertyName == nameof(WhisperModelText))
                OnPropertyChanged(nameof(WhisperModelStatus));
        };
        _operationState.Changed += OnOperationStateChanged;
        RefreshValidation();
    }

    // ------------------------------------------------------------------ observables
    [ObservableProperty] private string _recordingsDirectoryText = string.Empty;
    [ObservableProperty] private string _whisperExecutableText = string.Empty;
    [ObservableProperty] private string _whisperModelText = string.Empty;
    [ObservableProperty] private string _whisperLanguageText = "es";
    [ObservableProperty] private int _languageSelectionIndex;
    [ObservableProperty] private string _retentionSettingsText = "0";
    [ObservableProperty] private string _minimumFreeSpaceGbText = "1";
    [ObservableProperty] private bool _transcribeAutomaticallyOnStop;
    [ObservableProperty] private bool _densityCompact;
    [ObservableProperty] private bool _isTestingWhisper;
    [ObservableProperty] private string _whisperTestStatus = string.Empty;
    [ObservableProperty] private string _retentionStatusText = string.Empty;

    public bool IsCustomLanguage => LanguageSelectionIndex == 6;
    public bool CanApplyRetention => !_operationState.IsTranscribing && !_isRecordingActive()
        && int.TryParse(RetentionSettingsText, out var days) && days is >= 1 and <= 3650;
    public bool CanTestWhisper => !IsTestingWhisper
        && WhisperExecutableStatus.StartsWith('✓')
        && WhisperModelStatus.StartsWith('✓');
    public string RecordingsDirectoryStatus
    {
        get => _recordingsDirectoryStatus;
        private set => SetProperty(ref _recordingsDirectoryStatus, value);
    }
    public string WhisperExecutableStatus
    {
        get => _whisperExecutableStatus;
        private set => SetProperty(ref _whisperExecutableStatus, value);
    }
    public string WhisperModelStatus
    {
        get => _whisperModelStatus;
        private set => SetProperty(ref _whisperModelStatus, value);
    }

    partial void OnRecordingsDirectoryTextChanged(string value) => RefreshRecordingsDirectoryStatus();
    partial void OnWhisperExecutableTextChanged(string value) => RefreshWhisperExecutableStatus();
    partial void OnWhisperModelTextChanged(string value) => RefreshWhisperModelStatus();
    partial void OnRetentionSettingsTextChanged(string value)
    {
        OnPropertyChanged(nameof(CanApplyRetention));
        ApplyRetentionCommand.NotifyCanExecuteChanged();
    }
    partial void OnIsTestingWhisperChanged(bool value)
    {
        OnPropertyChanged(nameof(CanTestWhisper));
        TestWhisperCommand.NotifyCanExecuteChanged();
    }

    partial void OnLanguageSelectionIndexChanged(int value)
    {
        if (value < 0 || value > 6) return;
        if (value < 6)
            WhisperLanguageText = new[] { "es", "en", "fr", "de", "pt", "auto" }[value];
        OnPropertyChanged(nameof(IsCustomLanguage));
    }

    private void OnOperationStateChanged()
    {
        OnPropertyChanged(nameof(CanApplyRetention));
        ApplyRetentionCommand.NotifyCanExecuteChanged();
    }

    public void NotifyRecordingStateChanged() => OnOperationStateChanged();

    /// <summary>Snapshot inmediato de la configuración sin semántica de guardar.</summary>
    public AppSettings Snapshot() => BuildSettings();
    public bool IsDirty => BuildSettings() != _settings;

    public void UpdateLibraryPanelVisibility(bool isVisible)
    {
        _settings = _settings with { LibraryPanelVisible = isVisible };
        OnPropertyChanged(nameof(IsDirty));
    }

    private AppSettings BuildSettings() => _settings with
    {
        RecordingsDirectory = (RecordingsDirectoryText ?? string.Empty).Trim(),
        WhisperExecutablePath = (WhisperExecutableText ?? string.Empty).Trim(),
        WhisperModelPath = (WhisperModelText ?? string.Empty).Trim(),
        Language = LanguageSelectionIndex == 6
            ? string.IsNullOrWhiteSpace(WhisperLanguageText) ? "es" : WhisperLanguageText.Trim()
            : new[] { "es", "en", "fr", "de", "pt", "auto" }[Math.Clamp(LanguageSelectionIndex, 0, 5)],
        RetentionDays = int.TryParse(RetentionSettingsText, out var retentionDays) ? Math.Clamp(retentionDays, 0, 3650) : 0,
        MinimumFreeSpaceGb = double.TryParse(
            MinimumFreeSpaceGbText, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.CurrentCulture, out var minimumFreeSpaceGb)
            && double.IsFinite(minimumFreeSpaceGb) ? Math.Clamp(minimumFreeSpaceGb, 0, 1000) : 1,
        TranscribeAutomaticallyOnStop = TranscribeAutomaticallyOnStop,
        DensityCompact = DensityCompact,
        MicrophoneDeviceId = _deviceIds().MicrophoneDeviceId,
        OutputDeviceId = _deviceIds().OutputDeviceId
    };

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

    [RelayCommand(CanExecute = nameof(CanTestWhisper))]
    private async Task TestWhisperAsync()
    {
        IsTestingWhisper = true;
        WhisperTestStatus = "Comprobando respuesta local de Whisper…";
        try
        {
            var settings = BuildSettings();
            var engine = _services.CreateTranscriptionEngine(
                settings.WhisperExecutablePath, settings.WhisperModelPath, settings.Language);
            await engine.TestWhisperAsync();
            WhisperTestStatus = "✓ whisper-cli respondió; el modelo existe y no se cargó durante esta comprobación.";
            _log.Log("Comprobación de Whisper correcta. Se verificó el ejecutable y la presencia del modelo; el modelo no se cargó.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException
            or ArgumentException or System.Security.SecurityException)
        {
            WhisperTestStatus = "✗ No se pudo comprobar Whisper: " + ex.Message;
            _log.Log("No se pudo comprobar Whisper: " + ex.Message);
        }
        finally
        {
            IsTestingWhisper = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyRetention))]
    private async Task ApplyRetentionAsync()
    {
        if (!CanApplyRetention || !_operationState.TryBegin()) return;
        var days = int.Parse(RetentionSettingsText, System.Globalization.CultureInfo.InvariantCulture);
        var now = _services.Clock.UtcNow.UtcDateTime;
        try
        {
            var settings = BuildSettings();
            var root = settings.RecordingsDirectory;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                RetentionStatusText = "Configura una carpeta de grabaciones existente para aplicar la retención.";
                return;
            }

            var candidateCount = await Task.Run(() => CountRetentionCandidates(root, days, now));
            if (candidateCount > 0 && !await _host.ConfirmAsync(
                "Aplicar retención",
                $"Se eliminarán {candidateCount} sesión(es) con más de {days} días. La operación no se puede deshacer. ¿Continuar?",
                "Aplicar retención", destructive: true))
            {
                RetentionStatusText = "Retención cancelada.";
                return;
            }

            _save(settings);
            _settings = settings;
            OnPropertyChanged(nameof(IsDirty));

            var deleted = await Task.Run(() => _sessionStore.ApplyRetention(root, days, now));
            RetentionStatusText = $"Retención aplicada: se eliminaron {deleted.Count} de {candidateCount} sesión(es) previstas.";
            _log.Log(RetentionStatusText);
        }
        catch (Exception ex)
        {
            RetentionStatusText = "No se pudo completar la retención.";
            _log.Log("Error al aplicar retención: " + ex.Message);
        }
        finally
        {
            _operationState.Complete();
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        try
        {
            var settings = BuildSettings();
            _save(settings);
            _settings = settings;
            OnPropertyChanged(nameof(IsDirty));
            _log.Log("Configuración guardada.");
        }
        catch (Exception ex)
        {
            _log.Log("No se pudo guardar la configuración: " + ex.Message);
        }
    }

    private void RefreshValidation()
    {
        RefreshRecordingsDirectoryStatus();
        RefreshWhisperExecutableStatus();
        RefreshWhisperModelStatus();
    }

    private void RefreshRecordingsDirectoryStatus()
    {
        var path = RecordingsDirectoryText?.Trim() ?? string.Empty;
        if (!Directory.Exists(path))
        {
            RecordingsDirectoryStatus = "✗ La carpeta no existe";
        }
        else
        {
            var probePath = Path.Combine(path, $".transcriptor-write-test-{Guid.NewGuid():N}.tmp");
            try
            {
                using var probe = new FileStream(
                    probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    bufferSize: 1, FileOptions.DeleteOnClose);
                RecordingsDirectoryStatus = "✓ Carpeta existente y escribible";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or ArgumentException or NotSupportedException or System.Security.SecurityException)
            {
                RecordingsDirectoryStatus = "✗ No se puede escribir en la carpeta";
            }
            finally
            {
                try { if (File.Exists(probePath)) File.Delete(probePath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _log.Log("No se pudo limpiar el archivo temporal de validación de la carpeta: " + ex.Message);
                }
            }
        }
        RefreshTestCommand();
    }

    private void RefreshWhisperExecutableStatus()
    {
        var path = WhisperExecutableText?.Trim() ?? string.Empty;
        WhisperExecutableStatus = !File.Exists(path)
            ? "✗ No se encontró el ejecutable"
            : OperatingSystem.IsWindows()
                && !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase)
                    ? "✗ En Windows selecciona un ejecutable .exe"
                    : "✓ Ejecutable encontrado";
        RefreshTestCommand();
    }

    private void RefreshWhisperModelStatus()
    {
        var path = WhisperModelText?.Trim() ?? string.Empty;
        WhisperModelStatus = File.Exists(path)
            && string.Equals(Path.GetExtension(path), ".bin", StringComparison.OrdinalIgnoreCase)
                ? "✓ Modelo .bin encontrado"
                : "✗ Selecciona un modelo .bin existente";
        RefreshTestCommand();
    }

    private void RefreshTestCommand()
    {
        OnPropertyChanged(nameof(CanTestWhisper));
        TestWhisperCommand.NotifyCanExecuteChanged();
    }

    private int CountRetentionCandidates(string root, int days, DateTime utcNow)
    {
        var cutoff = utcNow.ToUniversalTime().AddDays(-days);
        return _sessionStore.GetSessions(root).Count(sessionDirectory =>
        {
            var layout = new Transcriptor.Transcription.SessionLayout(sessionDirectory);
            var tracks = new[]
            {
                layout.AudioTrack(Transcriptor.Transcription.SessionLayout.MicrophoneTrackName),
                layout.AudioTrack(Transcriptor.Transcription.SessionLayout.SystemTrackName),
                layout.AudioTrack(Transcriptor.Transcription.SessionLayout.MicrophoneTrackName, encrypted: false),
                layout.AudioTrack(Transcriptor.Transcription.SessionLayout.SystemTrackName, encrypted: false)
            };
            var lastWrite = tracks.Where(File.Exists).Select(File.GetLastWriteTimeUtc)
                .DefaultIfEmpty(Directory.GetLastWriteTimeUtc(sessionDirectory)).Max();
            return lastWrite < cutoff;
        });
    }
}
