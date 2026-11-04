namespace Transcriptor.Transcription;

/// <summary>Marca sesiones cuya captura no terminó íntegramente.</summary>
public static class RecordingIntegrity
{
    public const string IncompleteMarkerFileName = SessionLayout.IncompleteMarkerFileName;

    public static bool IsIncomplete(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        return File.Exists(new SessionLayout(directory).IncompleteMarker);
    }

    public static void MarkIncomplete(string directory, Exception error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(error);
        var marker = new SessionLayout(directory).IncompleteMarker;
        if (File.Exists(marker)) return;

        try
        {
            using var stream = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream);
            writer.WriteLine(DateTimeOffset.UtcNow.ToString("O"));
            writer.WriteLine(error.GetType().FullName);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
        catch (IOException) when (File.Exists(marker)) { }
    }
}
