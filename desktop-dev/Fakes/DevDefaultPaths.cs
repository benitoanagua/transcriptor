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
    private static readonly string DevRoot =
        Path.Combine(Path.GetTempPath(), "TranscriptorDev");

    public override string SettingsPath => Path.Combine(DevRoot, "config", "settings.json");

    public override string RecordingsRoot => Path.Combine(DevRoot, "Grabaciones");

    // El runner y el modelo son fakes: la ruta depende solo del compositor,
    // no del archivo físico.
    public override string WhisperExecutablePath => "whisper-fake";

    public override string WhisperModelPath => "modelo-fake.bin";
}
