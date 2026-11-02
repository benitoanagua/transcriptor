using System.IO;

namespace Transcriptor.Desktop;

/// <summary>Operaciones explícitas de ciclo de vida para sesiones locales.</summary>
public static class RecordingPrivacy
{
    public static void DeleteSession(string recordingsRoot, string sessionDirectory)
    {
        var root = Path.GetFullPath(recordingsRoot);
        var target = Path.GetFullPath(sessionDirectory);
        var relative = Path.GetRelativePath(root, target);
        if (relative == "." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
            throw new InvalidOperationException("La sesión debe estar dentro de la carpeta de grabaciones configurada.");
        if (!Directory.Exists(target))
            throw new DirectoryNotFoundException("La carpeta de la sesión ya no existe.");
        if (!File.Exists(Path.Combine(target, "yo.wav")) && !File.Exists(Path.Combine(target, "cliente.wav")))
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
        foreach (var dateDirectory in Directory.EnumerateDirectories(root))
        {
            if ((File.GetAttributes(dateDirectory) & FileAttributes.ReparsePoint) != 0) continue;
            foreach (var sessionDirectory in Directory.EnumerateDirectories(dateDirectory))
            {
                if ((File.GetAttributes(sessionDirectory) & FileAttributes.ReparsePoint) != 0) continue;
                var microphone = Path.Combine(sessionDirectory, "yo.wav");
                var client = Path.Combine(sessionDirectory, "cliente.wav");
                if (!File.Exists(microphone) && !File.Exists(client)) continue;
                var lastWrite = new[] { microphone, client }
                    .Where(File.Exists).Select(File.GetLastWriteTimeUtc).DefaultIfEmpty(Directory.GetLastWriteTimeUtc(sessionDirectory)).Max();
                if (lastWrite >= cutoff) continue;
                DeleteSession(root, sessionDirectory);
                deleted.Add(sessionDirectory);
            }
        }
        return deleted;
    }
}
