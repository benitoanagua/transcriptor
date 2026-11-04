using System.IO;
using System.Text.Json;

namespace Transcriptor.Desktop;

public sealed record AppSettings
{
    public string RecordingsDirectory { get; init; } = string.Empty;
    public string WhisperExecutablePath { get; init; } = string.Empty;
    public string WhisperModelPath { get; init; } = string.Empty;
    public string Language { get; init; } = "es";
    public int RetentionDays { get; init; }
    public string? MicrophoneDeviceId { get; init; }
    public string? OutputDeviceId { get; init; }
    public string ThemeMode { get; init; } = "Dark";
    public bool MiniModeAutoShow { get; init; } = true;
    public bool MiniModeAlwaysOnTop { get; init; } = true;
    public double? MiniModeLeft { get; init; }
    public double? MiniModeTop { get; init; }
    public double? MiniModeWidth { get; init; }
    public double? MiniModeHeight { get; init; }

    public static AppSettings CreateDefault()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new AppSettings
        {
            RecordingsDirectory = Path.Combine(home, "Transcriptor", "Grabaciones"),
            WhisperExecutablePath = Environment.GetEnvironmentVariable("WHISPER_BIN")
                ?? Path.Combine(home, "Transcriptor", "whisper", "whisper-cli.exe"),
            WhisperModelPath = Environment.GetEnvironmentVariable("WHISPER_MODEL")
                ?? Path.Combine(home, "Transcriptor", "models", "ggml-small.bin"),
            Language = Environment.GetEnvironmentVariable("WHISPER_LANG") ?? "es"
        };
    }
}

public sealed record SettingsLoadResult(AppSettings Settings, string? Warning);

/// <summary>Persiste preferencias locales sin almacenar credenciales ni secretos.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly ISettingsFileSystem _fileSystem;
    private readonly IClock _clock;
    public string SettingsPath { get; }

    public SettingsStore(string? settingsPath = null)
        : this(settingsPath, new PhysicalSettingsFileSystem(), SystemClock.Instance)
    {
    }

    public SettingsStore(string? settingsPath, ISettingsFileSystem fileSystem, IClock? clock = null)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _clock = clock ?? SystemClock.Instance;
        SettingsPath = settingsPath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Transcriptor", "settings.json");
    }

    public SettingsLoadResult Load()
    {
        if (!_fileSystem.FileExists(SettingsPath))
            return new SettingsLoadResult(AppSettings.CreateDefault(), null);

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
                AppSettings.CreateDefault(),
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
        MicrophoneDeviceId = string.IsNullOrWhiteSpace(settings.MicrophoneDeviceId) ? null : settings.MicrophoneDeviceId.Trim(),
        OutputDeviceId = string.IsNullOrWhiteSpace(settings.OutputDeviceId) ? null : settings.OutputDeviceId.Trim()
    };
}
