using System.Security.Cryptography;

namespace Transcriptor.Transcription;

/// <summary>
/// Publica contenedores completos mediante un archivo temporal en el mismo
/// directorio. El descifrado solo se expone después de autenticar el contenedor.
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
    /// Cifra, descifra para comprobar igualdad exacta y, opcionalmente, elimina
    /// el original solo después de que la verificación criptográfica coincida.
    /// </summary>
    public static void EncryptAndVerifyFile(string plaintextPath, string encryptedPath, bool deletePlaintext = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedPath);

        var source = Path.GetFullPath(plaintextPath);
        var destination = Path.GetFullPath(encryptedPath);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            throw new IOException("El origen y el destino no pueden ser el mismo archivo.");

        var directory = Path.GetDirectoryName(destination)
            ?? throw new ArgumentException("El destino debe tener una carpeta.", nameof(encryptedPath));
        Directory.CreateDirectory(directory);
        var staged = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.verified.tmp");
        try
        {
            // El contenedor nuevo no reemplaza el destino anterior hasta que
            // se autentica y coincide con el original byte a byte.
            EncryptFileAtomically(source, staged);
            VerifyFileMatches(source, staged);
            File.Move(staged, destination, overwrite: true);

            if (deletePlaintext)
                File.Delete(source);
        }
        finally
        {
            TryDelete(staged);
        }
    }

    /// <summary>Verifica la autenticidad completa sin conservar el temporal descifrado.</summary>
    public static void VerifyEncryptedFile(string encryptedPath)
    {
        var temporary = DecryptToTemporaryFile(encryptedPath);
        TryDelete(temporary);
        if (File.Exists(temporary))
            throw new IOException($"No se pudo eliminar el temporal descifrado: {temporary}");
    }

    /// <summary>Compara SHA-256 del original y del contenido descifrado sin cargar archivos completos en memoria.</summary>
    public static void VerifyFileMatches(string plaintextPath, string encryptedPath)
    {
        var source = Path.GetFullPath(plaintextPath);
        var temporary = DecryptToTemporaryFile(encryptedPath);
        try
        {
            using var original = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var restored = new FileStream(temporary, FileMode.Open, FileAccess.Read, FileShare.Read);
            var originalHash = SHA256.HashData(original);
            var restoredHash = SHA256.HashData(restored);
            try
            {
                if (!CryptographicOperations.FixedTimeEquals(originalHash, restoredHash))
                    throw new CryptographicException("El archivo cifrado no coincide byte a byte con el original.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(originalHash);
                CryptographicOperations.ZeroMemory(restoredHash);
            }
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
