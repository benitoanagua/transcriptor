using System.IO;

namespace Transcriptor.Desktop;

/// <summary>Operaciones explícitas de ciclo de vida para sesiones locales.</summary>
public static class RecordingPrivacy
{
    public static IReadOnlyList<string> GetSessions(string recordingsRoot)
    {
        var root = Path.GetFullPath(recordingsRoot);
        if (!Directory.Exists(root)) return Array.Empty<string>();
        EnsureNotReparsePoint(root, "La carpeta raíz de grabaciones no puede ser un enlace o punto de reanálisis.");

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
        EnsureNotReparsePoint(root, "La carpeta raíz de grabaciones no puede ser un enlace o punto de reanálisis.");

        if (!Directory.Exists(target))
            throw new DirectoryNotFoundException("La carpeta de la sesión ya no existe.");

        // Validar todos los antecesores evita que una carpeta de fecha enlazada
        // redirija la eliminación fuera de la raíz configurada.
        EnsureNoReparsePointsInPath(root, target);
        EnsureNoReparsePoints(target);
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
        EnsureNotReparsePoint(root, "La carpeta raíz de grabaciones no puede ser un enlace o punto de reanálisis.");

        var cutoff = utcNow.ToUniversalTime().AddDays(-retentionDays);
        var deleted = new List<string>();
        foreach (var sessionDirectory in GetSessions(root))
        {
            var tracks = new[] { Path.Combine(sessionDirectory, "yo.wav"), Path.Combine(sessionDirectory, "cliente.wav") };
            var lastWrite = tracks.Where(File.Exists).Select(File.GetLastWriteTimeUtc)
                .DefaultIfEmpty(Directory.GetLastWriteTimeUtc(sessionDirectory)).Max();
            if (lastWrite >= cutoff) continue;
            DeleteSession(root, sessionDirectory);
            deleted.Add(sessionDirectory);
        }
        return deleted;
    }

    private static bool HasAudioTracks(string directory) =>
        File.Exists(Path.Combine(directory, "yo.wav")) || File.Exists(Path.Combine(directory, "cliente.wav"));

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static void EnsureNotReparsePoint(string path, string message)
    {
        if (IsReparsePoint(path)) throw new InvalidOperationException(message);
    }

    private static void EnsureNoReparsePointsInPath(string root, string target)
    {
        var rootFullPath = Path.GetFullPath(root);
        for (DirectoryInfo? current = new(target); current is not null; current = current.Parent)
        {
            if (!current.Exists)
                throw new DirectoryNotFoundException("No se pudo validar toda la ruta de la sesión.");

            if ((File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException(
                    "La ruta de la sesión contiene un enlace o punto de reanálisis; se cancela la operación.");

            if (string.Equals(Path.GetFullPath(current.FullName), rootFullPath, StringComparison.OrdinalIgnoreCase))
                return;
        }

        throw new InvalidOperationException("La ruta de la sesión no desciende de la raíz de grabaciones.");
    }

    private static void EnsureNoReparsePoints(string directory)
    {
        var pending = new Stack<string>();
        pending.Push(directory);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                if (IsReparsePoint(entry))
                    throw new InvalidOperationException("La sesión contiene un enlace o punto de reanálisis; se cancela la operación para evitar salir de la carpeta autorizada.");
                if (Directory.Exists(entry)) pending.Push(entry);
            }
        }
    }
}
