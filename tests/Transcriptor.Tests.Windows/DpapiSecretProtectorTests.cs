using System.Security.Cryptography;
using Transcriptor.Platform.Windows;
using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Transcription.Tests.Windows;

/// <summary>
/// Pruebas del protector DPAPI real sobre el contenedor portable. DPAPI no produce
/// blobs deterministas (dependen del usuario y la máquina), así que no hay vector
/// dorado: la garantía es ida y vuelta autenticada dentro de un mismo perfil.
/// </summary>
public sealed class DpapiSecretProtectorTests
{
    [Fact]
    public void ProtectThenUnprotect_RestoresTheOriginalSecret()
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        var protector = DpapiSecretProtector.Instance;

        var protectedSecret = protector.Protect(secret);
        var restored = protector.Unprotect(protectedSecret);

        Assert.NotEqual(secret, protectedSecret);
        Assert.Equal(secret, restored);
    }

    [Fact]
    public void Unprotect_RejectsATamperedBlob()
    {
        var protector = DpapiSecretProtector.Instance;
        var protectedSecret = protector.Protect(RandomNumberGenerator.GetBytes(32));

        protectedSecret[^5] ^= 0x80;

        Assert.ThrowsAny<Exception>(() => protector.Unprotect(protectedSecret));
    }

    [Fact]
    public void EncryptedFileContainer_WithDpapiProtector_RoundTripsAndAuthenticates()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TranscriptorDpapiContainerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var containerPath = Path.Combine(directory, "sesion.trn");
        var protector = DpapiSecretProtector.Instance;
        var payload = RandomNumberGenerator.GetBytes(4096);
        try
        {
            using (var input = new MemoryStream(payload))
            using (var output = File.Create(containerPath))
                EncryptedFileContainer.Encrypt(input, output, protector);

            // El contenedor es exactamente el mismo formato portable: cabecera y
            // magic no dependen del protector usado para la clave.
            Assert.True(File.ReadAllBytes(containerPath).AsSpan().StartsWith("TRNCRYPT"u8));

            using (var encrypted = File.OpenRead(containerPath))
            using (var restored = new MemoryStream())
            {
                EncryptedFileContainer.Decrypt(encrypted, restored, protector);
                Assert.Equal(payload, restored.ToArray());
            }

            var bytes = File.ReadAllBytes(containerPath);
            bytes[^16] ^= 0x40;
            using var tampered = new MemoryStream(bytes);
            using var sink = new MemoryStream();
            Assert.ThrowsAny<Exception>(() => EncryptedFileContainer.Decrypt(tampered, sink, protector));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
