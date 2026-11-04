using Transcriptor.Transcription;
using System.IO;

namespace Transcriptor.UI;

/// <summary>Operaciones explícitas de ciclo de vida para sesiones locales.</summary>
public static class RecordingPrivacy
{
    public static IReadOnlyList<string> GetSessions(string recordingsRoot, ISessionDirectorySecurity security)
    {
        var root = Path.GetFullPath(recordingsRoot);
        if (!Directory.Exists(root)) return Array.Empty<string>();
        security.ValidateDirectoryPath(root);

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

    public static void DeleteSession(string recordingsRoot, string sessionDirectory, ISessionDirectorySecurity security)
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

        using var sessionLease = Transcriptor.Transcription.TranscriptionSessionLease.TryAcquire(target)
            ?? throw new InvalidOperationException("La sesión está siendo transcrita o modificada por otra operación.");

        // La validación compartida comprueba todos los antecesores, desde la
        // sesión hasta la raíz del volumen, antes de eliminar contenido.
        security.ValidateSessionDirectory(target);
        if (!HasAudioTracks(target))
            throw new InvalidOperationException("La carpeta no contiene pistas de audio reconocidas como una sesión.");

        Directory.Delete(target, recursive: true);
    }

    public static IReadOnlyList<string> ApplyRetention(string recordingsRoot, int retentionDays, DateTime utcNow, ISessionDirectorySecurity security)
    {
        if (retentionDays < 1 || retentionDays > 3650)
            throw new ArgumentOutOfRangeException(nameof(retentionDays), "La retención debe estar entre 1 y 3650 días.");

        var root = Path.GetFullPath(recordingsRoot);
        if (!Directory.Exists(root)) return Array.Empty<string>();
        var cutoff = utcNow.ToUniversalTime().AddDays(-retentionDays);
        var deleted = new List<string>();
        foreach (var sessionDirectory in GetSessions(root, security))
        {
            var layout = new Transcriptor.Transcription.SessionLayout(sessionDirectory);
            var tracks = new[]
            {
                layout.AudioTrack(Transcriptor.Transcription.SessionLayout.MicrophoneTrackName),
                layout.AudioTrack(Transcriptor.Transcription.SessionLayout.SystemTrackName),
                layout.AudioTrack(Transcriptor.Transcription.SessionLayout.MicrophoneTrackName, encrypted: false),
                layout.AudioTrack(Transcriptor.Transcription.SessionLayout.SystemTrackName, encrypted: false)
            };
            var lastWrite = tracks.Where(File.Exists).Select(File.GetLastWriteTimeUtc)
                .DefaultIfEmpty(Directory.GetLastWriteTimeUtc(sessionDirectory)).Max();
            if (lastWrite >= cutoff) continue;
            DeleteSession(root, sessionDirectory, security);
            deleted.Add(sessionDirectory);
        }
        return deleted;
    }

    private static bool HasAudioTracks(string directory)
    {
        var layout = new Transcriptor.Transcription.SessionLayout(directory);
        return File.Exists(layout.AudioTrack(Transcriptor.Transcription.SessionLayout.MicrophoneTrackName))
            || File.Exists(layout.AudioTrack(Transcriptor.Transcription.SessionLayout.SystemTrackName))
            || File.Exists(layout.AudioTrack(Transcriptor.Transcription.SessionLayout.MicrophoneTrackName, encrypted: false))
            || File.Exists(layout.AudioTrack(Transcriptor.Transcription.SessionLayout.SystemTrackName, encrypted: false));
    }

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

}
