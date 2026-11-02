using System.IO;

namespace Transcriptor.Desktop;

/// <summary>Operaciones explícitas de ciclo de vida para sesiones locales.</summary>
public static class RecordingPrivacy
{
    public static IReadOnlyList<string> GetSessions(string recordingsRoot)
    {
        var root = Path.GetFullPath(recordingsRoot);
        if (!Directory.Exists(root)) return Array.Empty<string>();
        Transcriptor.Transcription.RecordingAccessControl.ValidateDirectoryPath(root);

        var sessions = new List<string>();
        foreach (var dateDirectory in Directory.EnumerateDirectories(root))
        {
            if (IsReparsePoint(dateDirectory)) continue;
            foreach (var sessionDirectory in Directory.EnumerateDirectories(dateDirectory))
            {
                if (IsReparsePoint(sessionDirectory)) continue;
                if (HasAudioTracks(sessionDirectory)) sessions.Add(sessionDirectory);
            }
        }
        return sessions;
    }

    public static void DeleteSession(string recordingsRoot, string sessionDirectory)
    {
        var root = Path.GetFullPath(recordingsRoot);
        var target = Path.GetFullPath(sessionDirectory);
        var relative = Path.GetRelativePath(root, target);
        var segments = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        if (relative == "." || Path.IsPathRooted(relative) || segments.Length != 2
            || segments.Any(segment => segment == "." || segment == ".."))
            throw new InvalidOperationException("La sesión debe estar dentro de una carpeta de fecha de la raíz de grabaciones.");

        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("La carpeta raíz de grabaciones no existe.");
        if (!Directory.Exists(target))
            throw new DirectoryNotFoundException("La carpeta de la sesión ya no existe.");

        // La validación compartida comprueba todos los antecesores, desde la
        // sesión hasta la raíz del volumen, antes de eliminar contenido.
        Transcriptor.Transcription.RecordingAccessControl.ValidateSessionDirectory(target);
        if (!HasAudioTracks(target))
            throw new InvalidOperationException("La carpeta no contiene pistas de audio reconocidas como una sesión.");

        Directory.Delete(target, recursive: true);
    }

    public static IReadOnlyList<string> ApplyRetention(string recordingsRoot, int retentionDays, DateTime utcNow)
    {
        if (retentionDays < 1 || retentionDays > 3650)
            throw new ArgumentOutOfRangeException(nameof(retentionDays), "La retención debe estar entre 1 y 3650 días.");

        var root = Path.GetFullPath(recordingsRoot);
        if (!Directory.Exists(root)) return Array.Empty<string>();
        var cutoff = utcNow.ToUniversalTime().AddDays(-retentionDays);
        var deleted = new List<string>();
        foreach (var sessionDirectory in GetSessions(root))
        {
            var tracks = new[] { Path.Combine(sessionDirectory, "yo.trn"), Path.Combine(sessionDirectory, "cliente.trn"), Path.Combine(sessionDirectory, "yo.wav"), Path.Combine(sessionDirectory, "cliente.wav") };
            var lastWrite = tracks.Where(File.Exists).Select(File.GetLastWriteTimeUtc)
                .DefaultIfEmpty(Directory.GetLastWriteTimeUtc(sessionDirectory)).Max();
            if (lastWrite >= cutoff) continue;
            DeleteSession(root, sessionDirectory);
            deleted.Add(sessionDirectory);
        }
        return deleted;
    }

    private static bool HasAudioTracks(string directory) =>
        File.Exists(Path.Combine(directory, "yo.trn")) || File.Exists(Path.Combine(directory, "cliente.trn"))
        || File.Exists(Path.Combine(directory, "yo.wav")) || File.Exists(Path.Combine(directory, "cliente.wav"));

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

}
