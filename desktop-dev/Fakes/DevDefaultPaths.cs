using System.IO;
using Transcriptor.Transcription;
using Transcriptor.UI;

namespace Transcriptor.Dev;
// ⚠️ SOLO DESARROLLO ⚠️
// Rutas del entorno de desarrollo: todo vive en una carpeta temporal dedicada
// (nunca en las rutas reales del usuario) y la biblioteca de muestra se genera
// en su primera ejecución.
public sealed class DevDefaultPaths : PlatformDefaultPaths
{
    private static readonly string DefaultDevRoot =
        Path.Combine(Path.GetTempPath(), "TranscriptorDev");
    private readonly string _devRoot;

    public DevDefaultPaths(string? devRoot = null) =>
        _devRoot = devRoot ?? DefaultDevRoot;

    public override string SettingsPath => Path.Combine(_devRoot, "config", "settings.json");

    public override string RecordingsRoot => Path.Combine(_devRoot, "Grabaciones");

    // El runner y el modelo son fakes: la ruta depende solo del compositor,
    // no del archivo físico.
    public override string WhisperExecutablePath => "whisper-fake";

    public override string WhisperModelPath => "modelo-fake.bin";
}
