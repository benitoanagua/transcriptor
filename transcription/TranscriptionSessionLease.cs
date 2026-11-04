using System.Security.Cryptography;
using System.Text;

namespace Transcriptor.Transcription;

/// <summary>
/// Cross-process exclusive lease for one recording session. The lock lives beside
/// the session so a guarded deletion can remove the session directory safely.
/// DeleteOnClose releases the OS handle after normal completion or process exit.
/// </summary>
public sealed class TranscriptionSessionLease : IDisposable
{
    private readonly FileStream stream;

    private TranscriptionSessionLease(FileStream stream) => this.stream = stream;

    public static TranscriptionSessionLease? TryAcquire(string sessionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);
        var fullPath = Path.GetFullPath(sessionDirectory);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException(fullPath);

        var parent = Directory.GetParent(fullPath)?.FullName
            ?? throw new InvalidOperationException("La sesión no tiene carpeta padre.");
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            OperatingSystem.IsWindows() ? fullPath.ToUpperInvariant() : fullPath)))[..24];
        var lockPath = Path.Combine(parent, $".transcription-{identity}.lock");

        try
        {
            var handle = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);
            return new TranscriptionSessionLease(handle);
        }
        catch (IOException exception) when (IsSharingViolation(exception))
        {
            return null;
        }
    }

    private static bool IsSharingViolation(IOException exception)
    {
        var nativeCode = exception.HResult & 0xFFFF;
        return nativeCode is 32 or 33; // ERROR_SHARING_VIOLATION / ERROR_LOCK_VIOLATION
    }

    public void Dispose() => stream.Dispose();
}
