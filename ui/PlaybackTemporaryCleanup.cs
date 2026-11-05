using Transcriptor.Transcription;
using Transcriptor.UI.ViewModels;

namespace Transcriptor.UI;

/// <summary>Removes only abandoned decrypted playback WAVs from unlocked sessions.</summary>
public static class PlaybackTemporaryCleanup
{
    private const string PlaybackSuffix = ".playback.wav";
    private const string RawAudioSuffix = ".audio.tmp";

    public static int Sweep(ISessionStore sessionStore, string recordingsRoot, ILogSink log)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(log);
        if (string.IsNullOrWhiteSpace(recordingsRoot) || !Directory.Exists(recordingsRoot)) return 0;

        IReadOnlyList<string> sessions;
        try
        {
            sessions = sessionStore.GetSessions(recordingsRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or System.Security.SecurityException)
        {
            log.Log("No se pudieron revisar los temporales de reproducción: " + ex.Message);
            return 0;
        }

        var removed = 0;
        foreach (var session in sessions)
        {
            try
            {
                using var lease = TranscriptionSessionLease.TryAcquire(session);
                if (lease is null) continue;

                foreach (var path in Directory.EnumerateFiles(session, "*", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(path);
                    if (!IsOwnedTemporaryName(name, PlaybackSuffix)
                        && !IsOwnedTemporaryName(name, RawAudioSuffix))
                        continue;
                    var attributes = File.GetAttributes(path);
                    if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) continue;
                    File.Delete(path);
                    removed++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or InvalidOperationException or System.Security.SecurityException)
            {
                log.Log("No se pudo limpiar un temporal de reproducción en " + session + ": " + ex.Message);
            }
        }

        return removed;
    }

    private static bool IsOwnedTemporaryName(string name, string suffix)
    {
        if (name.Length != 1 + 32 + suffix.Length
            || name[0] != '.'
            || !name.EndsWith(suffix, StringComparison.Ordinal))
            return false;

        for (var index = 1; index < 33; index++)
        {
            var value = name[index];
            if (value is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
                return false;
        }

        return true;
    }
}
