namespace Transcriptor.Transcription;

/// <summary>Marca sesiones cuya captura no terminó íntegramente.</summary>
public static class RecordingIntegrity
{
    public const string IncompleteMarkerFileName = ".recording-incomplete";

    public static bool IsIncomplete(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        return File.Exists(Path.Combine(directory, IncompleteMarkerFileName));
    }

    public static void MarkIncomplete(string directory, Exception error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(error);
        var marker = Path.Combine(directory, IncompleteMarkerFileName);
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
