namespace Transcriptor.Transcription;

/// <summary>
/// Cross-process exclusive lease for one recording session. DeleteOnClose releases
/// the operating-system handle after normal completion or process termination.
/// </summary>
internal sealed class TranscriptionSessionLease : IDisposable
{
    private readonly FileStream stream;

    private TranscriptionSessionLease(FileStream stream) => this.stream = stream;

    public static TranscriptionSessionLease? TryAcquire(string sessionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);
        var fullPath = Path.GetFullPath(sessionDirectory);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException(fullPath);

        var lockPath = Path.Combine(fullPath, ".transcription.lock");
        try
        {
            var stream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);
            return new TranscriptionSessionLease(stream);
        }
        catch (IOException)
        {
            // Another process owns the exclusive handle.
            return null;
        }
    }

    public void Dispose() => stream.Dispose();
}
