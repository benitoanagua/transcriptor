using System.Security.Cryptography;

namespace Transcriptor.Transcription;

/// <summary>
/// Publica contenedores completos mediante un archivo temporal en el mismo
/// directorio. La salida de descifrado permanece en un temporal privado hasta
/// que todos los bloques y el terminador hayan sido autenticados.
/// </summary>
public static class EncryptedFileOperations
{
    public static void EncryptFileAtomically(string plaintextPath, string encryptedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedPath);
        EnsureWindows();

        var source = Path.GetFullPath(plaintextPath);
        var destination = Path.GetFullPath(encryptedPath);
        var directory = Path.GetDirectoryName(destination)
            ?? throw new ArgumentException("El destino debe tener una carpeta.", nameof(encryptedPath));
        Directory.CreateDirectory(directory);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            throw new IOException("El origen y el destino no pueden ser el mismo archivo.");

        var temporary = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       64 * 1024, FileOptions.WriteThrough))
            {
                EncryptedFileContainer.Encrypt(input, output);
                output.Flush(flushToDisk: true);
            }

            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    /// <summary>
    /// Descifra a un archivo temporal junto al contenedor. El llamador debe
    /// eliminarlo en finally después de consumirlo. Nunca devuelve un archivo
    /// parcialmente autenticado.
    /// </summary>
    public static string DecryptToTemporaryFile(string encryptedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedPath);
        EnsureWindows();

        var source = Path.GetFullPath(encryptedPath);
        var directory = Path.GetDirectoryName(source)
            ?? throw new ArgumentException("El origen debe tener una carpeta.", nameof(encryptedPath));
        var temporary = Path.Combine(directory, $".{Path.GetFileName(source)}.{Guid.NewGuid():N}.plaintext.tmp");
        try
        {
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       64 * 1024, FileOptions.WriteThrough))
            {
                EncryptedFileContainer.Decrypt(input, output);
                output.Flush(flushToDisk: true);
            }

            return temporary;
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Las operaciones de archivo cifrado requieren Windows DPAPI.");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // El borrado normal es best effort; no se presenta como borrado seguro.
        }
    }
}
