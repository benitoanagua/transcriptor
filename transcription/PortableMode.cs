namespace Transcriptor.Transcription;

/// <summary>
/// Modo portable: si junto a los ejecutables existe <c>portable.flag</c>, la configuración, el motor
/// Whisper, el modelo y las grabaciones por defecto viven en la carpeta de la aplicación y no se
/// escribe nada en %LOCALAPPDATA% ni en las variables de entorno.
/// </summary>
public static class PortableMode
{
    public const string MarkerFileName = "portable.flag";

    public static string BaseDirectory => AppContext.BaseDirectory;

    public static bool IsPortable => IsPortableAt(BaseDirectory);

    public static bool IsPortableAt(string directory) =>
        !string.IsNullOrWhiteSpace(directory) && File.Exists(Path.Combine(directory, MarkerFileName));

    public static string DataDirectory(string root) => Path.Combine(root, "data");
    public static string SettingsPath(string root) => Path.Combine(DataDirectory(root), "settings.json");
    public static string RecordingsDirectory(string root) => Path.Combine(DataDirectory(root), "Grabaciones");
    public static string WhisperExecutable(string root) => Path.Combine(root, "whisper", "whisper-cli.exe");
    public static string ModelPath(string root) => Path.Combine(root, "models", "ggml-small.bin");
}
