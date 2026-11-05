using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Transcriptor.UI.ViewModels;

/// <summary>
/// Panel AJUSTES: rutas de trabajo y de Whisper, tema y retención
/// configurada. No toca Avalonia; los selectores de archivos y la
/// aplicación de tema viajan por los puertos <see cref="IUiHost"/> y
/// <see cref="UiServices.ApplyTheme"/>.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private AppSettings _settings;
    private readonly UiServices _services;
    private readonly IUiHost _host;
    private readonly ILogSink _log;
    private readonly Action<AppSettings> _save;
    private readonly Func<(string? MicrophoneDeviceId, string? OutputDeviceId)> _deviceIds;

    public SettingsViewModel(
        AppSettings settings, UiServices services, IUiHost host, ILogSink log,
        Action<AppSettings> save, Func<(string? MicrophoneDeviceId, string? OutputDeviceId)> deviceIds)
    {
        _settings = settings;
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _save = save ?? throw new ArgumentNullException(nameof(save));
        _deviceIds = deviceIds ?? throw new ArgumentNullException(nameof(deviceIds));
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
        ThemeModeIndex = settings.ThemeMode switch { "Light" => 1, "Dark" => 2, _ => 0 };
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
    [ObservableProperty] private int _themeModeIndex;

    public bool IsCustomLanguage => LanguageSelectionIndex == 6;

    partial void OnLanguageSelectionIndexChanged(int value)
    {
        if (value < 0 || value > 6) return;
        if (value < 6)
            WhisperLanguageText = new[] { "es", "en", "fr", "de", "pt", "auto" }[value];
        OnPropertyChanged(nameof(IsCustomLanguage));
    }

    /// <summary>Snapshot inmediato de la configuración sin semántica de guardar.</summary>
    public AppSettings Snapshot() => BuildSettings();
    public bool IsDirty => BuildSettings() != _settings;
    public string RecordingsDirectoryStatus => Directory.Exists(RecordingsDirectoryText)
        ? "✓ Carpeta existente"
        : "✗ La carpeta no existe";
    public string WhisperExecutableStatus => File.Exists(WhisperExecutableText)
        ? "✓ Ejecutable encontrado"
        : "✗ No se encontró el ejecutable";
    public string WhisperModelStatus => File.Exists(WhisperModelText)
        && string.Equals(Path.GetExtension(WhisperModelText), ".bin", StringComparison.OrdinalIgnoreCase)
            ? "✓ Modelo encontrado"
            : "✗ Selecciona un modelo .bin existente";

    private AppSettings BuildSettings() => _settings with
    {
        RecordingsDirectory = (RecordingsDirectoryText ?? string.Empty).Trim(),
        WhisperExecutablePath = (WhisperExecutableText ?? string.Empty).Trim(),
        WhisperModelPath = (WhisperModelText ?? string.Empty).Trim(),
        Language = LanguageSelectionIndex == 6
            ? string.IsNullOrWhiteSpace(WhisperLanguageText) ? "es" : WhisperLanguageText!.Trim()
            : new[] { "es", "en", "fr", "de", "pt", "auto" }[Math.Clamp(LanguageSelectionIndex, 0, 5)],
        RetentionDays = int.TryParse(RetentionSettingsText, out var retentionDays) ? Math.Clamp(retentionDays, 0, 3650) : 0,
        MinimumFreeSpaceGb = double.TryParse(
            MinimumFreeSpaceGbText, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.CurrentCulture, out var minimumFreeSpaceGb)
            && double.IsFinite(minimumFreeSpaceGb) ? Math.Clamp(minimumFreeSpaceGb, 0, 1000) : 1,
        TranscribeAutomaticallyOnStop = TranscribeAutomaticallyOnStop,
        ThemeMode = ThemeModeIndex switch { 1 => "Light", 2 => "Dark", _ => "System" },
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

    [RelayCommand]
    private void SaveSettings()
    {
        try
        {
            var settings = BuildSettings();
            _save(settings);
            _services.ApplyTheme(settings.ThemeMode);
            _settings = settings;
            OnPropertyChanged(nameof(IsDirty));
            _log.Log("Configuración guardada.");
        }
        catch (Exception ex)
        {
            _log.Log("No se pudo guardar la configuración: " + ex.Message);
        }
    }
}
