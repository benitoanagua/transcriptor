using System.Security.Cryptography;
using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class EncryptedFileContainerTests
{
    [Fact]
    public void EncryptDecrypt_RoundTripsEmptyFile()
    {
        RoundTrip(Array.Empty<byte>());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4095)]
    [InlineData(65536)]
    [InlineData(65537)]
    [InlineData(200000)]
    public void EncryptDecrypt_RoundTripsAcrossBlockBoundaries(int length)
    {
        RoundTrip(RandomNumberGenerator.GetBytes(length));
    }

    [Fact]
    public void EncryptDecrypt_UsesDifferentCiphertextForSamePlaintext()
    {
        var plaintext = RandomNumberGenerator.GetBytes(1024);
        var first = Encrypt(plaintext);
        var second = Encrypt(plaintext);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Decrypt_RejectsModifiedHeader()
    {
        var encrypted = Encrypt(new byte[] { 1, 2, 3 });
        encrypted[12] ^= 0x01;
        Assert.ThrowsAny<Exception>(() => Decrypt(encrypted));
    }

    [Fact]
    public void Decrypt_RejectsModifiedCiphertextOrTag()
    {
        var encrypted = Encrypt(RandomNumberGenerator.GetBytes(100));
        encrypted[encrypted.Length - 20] ^= 0x40;
        Assert.ThrowsAny<CryptographicException>(() => Decrypt(encrypted));
    }

    [Fact]
    public void Decrypt_RejectsTruncatedContainer()
    {
        var encrypted = Encrypt(RandomNumberGenerator.GetBytes(100));
        Array.Resize(ref encrypted, encrypted.Length - 3);
        Assert.ThrowsAny<EndOfStreamException>(() => Decrypt(encrypted));
    }

    [Fact]
    public void Decrypt_RejectsTrailingData()
    {
        var encrypted = Encrypt(new byte[] { 7, 8, 9 }).Append((byte)0x42).ToArray();
        Assert.Throws<InvalidDataException>(() => Decrypt(encrypted));
    }

    [Fact]
    public void Encrypt_LeavesAnIncompleteContainerWhenSourceReadFails()
    {
        using var source = new FailingReadStream();
        using var encrypted = new MemoryStream();

        Assert.Throws<IOException>(() => EncryptedFileContainer.Encrypt(source, encrypted));

        encrypted.Position = 0;
        using var restored = new MemoryStream();
        Assert.Throws<EndOfStreamException>(() => EncryptedFileContainer.Decrypt(encrypted, restored));
    }

    private static void RoundTrip(byte[] plaintext)
    {
        var decrypted = Decrypt(Encrypt(plaintext));
        Assert.Equal(plaintext, decrypted);
    }

    private static byte[] Encrypt(byte[] plaintext)
    {
        using var input = new MemoryStream(plaintext);
        using var output = new MemoryStream();
        EncryptedFileContainer.Encrypt(input, output);
        return output.ToArray();
    }

    private static byte[] Decrypt(byte[] encrypted)
    {
        using var input = new MemoryStream(encrypted);
        using var output = new MemoryStream();
        EncryptedFileContainer.Decrypt(input, output);
        return output.ToArray();
    }

    private sealed class FailingReadStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new IOException("Simulated source read failure.");
    }
}