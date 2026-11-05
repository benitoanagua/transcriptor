using System.IO;
using System.Text.Json;

namespace Transcriptor.UI;

public sealed record AppSettings
{
    public string RecordingsDirectory { get; init; } = string.Empty;
    public string WhisperExecutablePath { get; init; } = string.Empty;
    public string WhisperModelPath { get; init; } = string.Empty;
    public string Language { get; init; } = "es";
    public int RetentionDays { get; init; }
    public double MinimumFreeSpaceGb { get; init; } = 1;
    public bool TranscribeAutomaticallyOnStop { get; init; }
    public string? MicrophoneDeviceId { get; init; }
    public string? OutputDeviceId { get; init; }
    public string ThemeMode { get; init; } = "Dark";
    public bool LibraryPanelVisible { get; init; } = true;

    /// <summary>Default settings built from platform-provided default paths.</summary>
    public static AppSettings CreateDefault(IDefaultPaths paths) => new()
    {
        RecordingsDirectory = paths.RecordingsRoot,
        WhisperExecutablePath = paths.WhisperExecutablePath,
        WhisperModelPath = paths.WhisperModelPath
    };

    public static AppSettings CreateDefault() => CreateDefault(new PlatformDefaultPaths());
}

/// <summary>
/// Platform-owned default locations (settings file, recordings root, Whisper
/// executable and model) and the executable file-picker filter pattern. Portable
/// code must take these from this service instead of hard-coding platform paths
/// or patterns.
/// </summary>
public interface IDefaultPaths
{
    string SettingsPath { get; }
    string RecordingsRoot { get; }
    string WhisperExecutablePath { get; }
    string WhisperModelPath { get; }
    /// <summary>Picker pattern for the Whisper executable; null when the platform has none.</summary>
    string? WhisperExecutablePickerPattern { get; }
}

/// <summary>
/// Single centralized portable implementation of default paths. Windows keeps the
/// exact same locations as before; non-Windows hosts use XDG_DATA_HOME/USERPROFILE
/// equivalents and no .exe filter. Development heads can further override the
/// recordings root to point at a sample library.
/// </summary>
public class PlatformDefaultPaths : IDefaultPaths
{
    // On Windows: %LOCALAPPDATA%\Transcriptor\settings.json. On Linux, LocalApplicationData
    // maps to $XDG_DATA_HOME (fallback ~/.local/share), so the same expression covers both.
    public virtual string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Transcriptor", "settings.json");

    public virtual string RecordingsRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Transcriptor", "Grabaciones");

    public virtual string WhisperExecutablePath
    {
        get
        {
            var fromEnvironment = Environment.GetEnvironmentVariable("WHISPER_BIN");
            if (!string.IsNullOrWhiteSpace(fromEnvironment)) return fromEnvironment;
            var executableName = OperatingSystem.IsWindows() ? "whisper-cli.exe" : "whisper-cli";
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Transcriptor", "whisper", executableName);
        }
    }

    public virtual string WhisperModelPath
    {
        get
        {
            var fromEnvironment = Environment.GetEnvironmentVariable("WHISPER_MODEL");
            if (!string.IsNullOrWhiteSpace(fromEnvironment)) return fromEnvironment;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Transcriptor", "models", "ggml-small.bin");
        }
    }

    public virtual string? WhisperExecutablePickerPattern => OperatingSystem.IsWindows() ? "*.exe" : null;
}

public sealed record SettingsLoadResult(AppSettings Settings, string? Warning);

/// <summary>Persiste preferencias locales sin almacenar credenciales ni secretos.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly ISettingsFileSystem _fileSystem;
    private readonly IClock _clock;
    private readonly IDefaultPaths _defaultPaths;
    public string SettingsPath { get; }

    public SettingsStore(string settingsPath, ISettingsFileSystem fileSystem, IClock clock, IDefaultPaths defaultPaths)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _defaultPaths = defaultPaths ?? throw new ArgumentNullException(nameof(defaultPaths));
        SettingsPath = settingsPath ?? throw new ArgumentNullException(nameof(settingsPath));
    }

    public SettingsLoadResult Load()
    {
        if (!_fileSystem.FileExists(SettingsPath))
            return new SettingsLoadResult(AppSettings.CreateDefault(_defaultPaths), null);

        try
        {
            var json = _fileSystem.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings is null)
                throw new JsonException("El archivo no contiene una configuración válida.");
            return new SettingsLoadResult(Normalize(settings), null);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            var backupPath = SettingsPath + ".corrupt-" + _clock.UtcNow.ToString("yyyyMMddHHmmssfff");
            try { _fileSystem.Move(SettingsPath, backupPath, overwrite: true); }
            catch (Exception backupException) when (backupException is IOException or UnauthorizedAccessException) { }

            return new SettingsLoadResult(
                AppSettings.CreateDefault(_defaultPaths),
                $"La configuración estaba dañada o no se pudo leer. Se usarán valores predeterminados. Detalle: {exception.Message}");
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = Normalize(settings);
        var directory = Path.GetDirectoryName(SettingsPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("La ruta de configuración no tiene una carpeta contenedora.");

        _fileSystem.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        _fileSystem.WriteAllTextAtomically(SettingsPath, json);
    }

    private static AppSettings Normalize(AppSettings settings) => settings with
    {
        RecordingsDirectory = settings.RecordingsDirectory?.Trim() ?? string.Empty,
        WhisperExecutablePath = settings.WhisperExecutablePath?.Trim() ?? string.Empty,
        WhisperModelPath = settings.WhisperModelPath?.Trim() ?? string.Empty,
        Language = string.IsNullOrWhiteSpace(settings.Language) ? "es" : settings.Language.Trim().ToLowerInvariant(),
        ThemeMode = settings.ThemeMode is "Dark" or "Light" or "HighContrast" or "System" ? settings.ThemeMode : "Dark",
        RetentionDays = Math.Clamp(settings.RetentionDays, 0, 3650),
        MinimumFreeSpaceGb = double.IsFinite(settings.MinimumFreeSpaceGb)
            ? Math.Clamp(settings.MinimumFreeSpaceGb, 0, 1000)
            : 1,
        MicrophoneDeviceId = string.IsNullOrWhiteSpace(settings.MicrophoneDeviceId) ? null : settings.MicrophoneDeviceId.Trim(),
        OutputDeviceId = string.IsNullOrWhiteSpace(settings.OutputDeviceId) ? null : settings.OutputDeviceId.Trim()
    };
}
