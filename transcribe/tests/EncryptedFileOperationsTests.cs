using System.Security.Cryptography;
using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class EncryptedFileOperationsTests
{
    [Fact]
    public void EncryptFileAtomically_PublishesDecryptableContainerAndPreservesSource()
    {
        var directory = CreateDirectory();
        try
        {
            var source = Path.Combine(directory, "source.wav");
            var encrypted = Path.Combine(directory, "source.trn");
            var bytes = RandomNumberGenerator.GetBytes(180_000);
            File.WriteAllBytes(source, bytes);

            EncryptedFileOperations.EncryptFileAtomically(source, encrypted);

            Assert.True(File.Exists(source));
            Assert.True(File.Exists(encrypted));
            var temporary = EncryptedFileOperations.DecryptToTemporaryFile(encrypted);
            try { Assert.Equal(bytes, File.ReadAllBytes(temporary)); }
            finally { File.Delete(temporary); }
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void EncryptAndVerifyFile_DeletesSourceOnlyAfterByteForByteVerification()
    {
        var directory = CreateDirectory();
        try
        {
            var source = Path.Combine(directory, "transcripcion.txt");
            var encrypted = source + ".trn";
            var bytes = RandomNumberGenerator.GetBytes(210_123);
            File.WriteAllBytes(source, bytes);

            EncryptedFileOperations.EncryptAndVerifyFile(source, encrypted, deletePlaintext: true);

            Assert.False(File.Exists(source));
            Assert.True(File.Exists(encrypted));
            EncryptedFileOperations.VerifyEncryptedFile(encrypted);
            Assert.Empty(Directory.EnumerateFiles(directory, "*.plaintext.tmp"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void VerifyEncryptedFile_RejectsTamperedContainer()
    {
        var directory = CreateDirectory();
        try
        {
            var source = Path.Combine(directory, "source.txt");
            var encrypted = source + ".trn";
            File.WriteAllText(source, "contenido que debe autenticarse");
            EncryptedFileOperations.EncryptFileAtomically(source, encrypted);

            var bytes = File.ReadAllBytes(encrypted);
            bytes[^1] ^= 0x20;
            File.WriteAllBytes(encrypted, bytes);

            Assert.ThrowsAny<CryptographicException>(() => EncryptedFileOperations.VerifyEncryptedFile(encrypted));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.plaintext.tmp"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void DecryptToTemporaryFile_DoesNotLeavePlaintextWhenAuthenticationFails()
    {
        var directory = CreateDirectory();
        try
        {
            var encrypted = Path.Combine(directory, "damaged.trn");
            using (var input = new MemoryStream(RandomNumberGenerator.GetBytes(90_000)))
            using (var output = File.Create(encrypted))
                EncryptedFileContainer.Encrypt(input, output);

            var bytes = File.ReadAllBytes(encrypted);
            bytes[^1] ^= 0x20;
            File.WriteAllBytes(encrypted, bytes);

            Assert.ThrowsAny<CryptographicException>(
                () => EncryptedFileOperations.DecryptToTemporaryFile(encrypted));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.plaintext.tmp"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string CreateDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TranscriptorEncryptedTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
