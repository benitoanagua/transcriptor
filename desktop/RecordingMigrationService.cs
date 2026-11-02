using System.IO;
using System.Security.Cryptography;
using NAudio.Wave;
using Transcriptor.Transcription;

namespace Transcriptor.Desktop;

public sealed record RecordingMigrationResult(int AudioTracksMigrated, int TranscriptsMigrated, int AlreadyEncrypted);

/// <summary>
/// Migra una sesión heredada en claro de forma reanudable: verifica todas las
/// copias cifradas antes de eliminar cualquier original.
/// </summary>
public static class RecordingMigrationService
{
    private static readonly (string Plain, string Encrypted, bool Audio)[] Files =
    [
        ("yo.wav", "yo.trn", true),
        ("cliente.wav", "cliente.trn", true),
        ("yo.srt", "yo.srt.trn", false),
        ("cliente.srt", "cliente.srt.trn", false),
        ("transcripcion.srt", "transcripcion.srt.trn", false),
        ("transcripcion.txt", "transcripcion.txt.trn", false)
    ];

    public static RecordingMigrationResult MigrateSession(string sessionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);
        var directory = Path.GetFullPath(sessionDirectory);
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException("La carpeta de la sesión no existe.");

        RecordingAccessControl.RestrictSessionDirectory(directory);
        var audioMigrated = 0;
        var transcriptMigrated = 0;
        var alreadyEncrypted = 0;
        var verifiedPlaintext = new List<string>();

        foreach (var file in Files)
        {
            var source = Path.Combine(directory, file.Plain);
            var destination = Path.Combine(directory, file.Encrypted);
            var sourceExists = File.Exists(source);
            var encryptedExists = File.Exists(destination);
            if (!sourceExists && !encryptedExists) continue;

            if (encryptedExists)
            {
                if (file.Audio)
                {
                    if (sourceExists) VerifyAudioPair(source, destination);
                    else ValidateEncryptedAudio(destination);
                }
                else
                {
                    if (sourceExists) VerifyFilePair(source, destination);
                    else ValidateEncryptedFile(destination);
                }
                alreadyEncrypted++;
                if (sourceExists) verifiedPlaintext.Add(source);
                continue;
            }

            var temporary = Path.Combine(directory, $".{Guid.NewGuid():N}.migration.tmp");
            try
            {
                if (file.Audio)
                {
                    EncryptAudio(source, temporary);
                    VerifyAudioPair(source, temporary);
                }
                else
                {
                    EncryptedFileOperations.EncryptFileAtomically(source, temporary);
                    VerifyFilePair(source, temporary);
                }

                File.Move(temporary, destination);
                if (file.Audio) audioMigrated++;
                else transcriptMigrated++;
                verifiedPlaintext.Add(source);
            }
            finally
            {
                TryDelete(temporary);
            }
        }

        // Ningún original se toca hasta que todos los archivos presentes tengan
        // una copia cifrada íntegra y autenticada.
        foreach (var source in verifiedPlaintext.Distinct(StringComparer.OrdinalIgnoreCase))
            File.Delete(source);

        return new RecordingMigrationResult(audioMigrated, transcriptMigrated, alreadyEncrypted);
    }

    private static void EncryptAudio(string source, string destination)
    {
        using var reader = new WaveFileReader(source);
        using var writer = EncryptedAudioTrack.CreateWriter(destination, reader.WaveFormat);
        var buffer = new byte[64 * 1024];
        try
        {
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                writer.Write(buffer, 0, read);
        }
        finally { CryptographicOperations.ZeroMemory(buffer); }
    }

    private static void VerifyAudioPair(string source, string encrypted)
    {
        var decrypted = EncryptedAudioTrack.DecryptToWaveFile(encrypted);
        try
        {
            var original = HashAudio(source);
            var restored = HashAudio(decrypted);
            try
            {
                if (!string.Equals(original.Format, restored.Format, StringComparison.Ordinal)
                    || !CryptographicOperations.FixedTimeEquals(original.Hash, restored.Hash))
                    throw new CryptographicException("La pista cifrada no coincide con el audio original.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(original.Hash);
                CryptographicOperations.ZeroMemory(restored.Hash);
            }
        }
        finally { TryDelete(decrypted); }
    }

    private static void ValidateEncryptedAudio(string encrypted)
    {
        var decrypted = EncryptedAudioTrack.DecryptToWaveFile(encrypted);
        TryDelete(decrypted);
    }

    private static (string Format, byte[] Hash) HashAudio(string path)
    {
        using var reader = new WaveFileReader(path);
        var format = reader.WaveFormat.AsStandardWaveFormat();
        var signature = $"{format.Encoding}|{format.SampleRate}|{format.Channels}|{format.BitsPerSample}|{format.BlockAlign}";
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        try
        {
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                hash.AppendData(buffer, 0, read);
            return (signature, hash.GetHashAndReset());
        }
        finally { CryptographicOperations.ZeroMemory(buffer); }
    }

    private static void VerifyFilePair(string source, string encrypted) =>
        EncryptedFileOperations.VerifyFileMatches(source, encrypted);

    private static void ValidateEncryptedFile(string encrypted) =>
        EncryptedFileOperations.VerifyEncryptedFile(encrypted);


    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* El borrado normal es best effort, no seguro. */ }
    }
}
