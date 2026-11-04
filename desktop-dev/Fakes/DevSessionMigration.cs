using Transcriptor.Transcription;
namespace Transcriptor.Dev;

// ⚠️ SOLO DESARROLLO ⚠️
// Migración simulada NO destructiva: no cifra ni elimina nada; solo cuenta los
// pares ya cifrados para dar una respuesta honesta a la interfaz. El banner de
// modo desarrollo permanece visible y la pista real sigue siendo la cabeza Windows.
public sealed class DevSessionMigration : ISessionMigrationService
{
    public RecordingMigrationResult MigrateSession(string sessionDirectory)
    {
        if (!Directory.Exists(sessionDirectory))
            throw new DirectoryNotFoundException("La carpeta de la sesión no existe.");
        var encrypted = Directory.GetFiles(sessionDirectory, "*.trn", SearchOption.TopDirectoryOnly);
        return new RecordingMigrationResult(0, 0, encrypted.Length);
    }
}
