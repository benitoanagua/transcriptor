using Transcriptor.Transcription;

namespace Transcriptor.Dev;

// ⚠️ INSEGURO — SOLO DESARROLLO ⚠️
// No valida rutas, no rechaza punto de reanálisis ni aplica ACL. El banner de
// desarrollo permanece visible mientras esta clase esté instalada.
public sealed class NoOpSessionSecurity : ISessionDirectorySecurity
{
    public void ValidateDirectoryPath(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
    }

    public void ValidateSessionDirectory(string sessionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);
        if (!Directory.Exists(sessionDirectory))
            throw new DirectoryNotFoundException("La carpeta de sesión no existe.");
    }

    public void RestrictSessionDirectory(string sessionDirectory) => ValidateSessionDirectory(sessionDirectory);
}
