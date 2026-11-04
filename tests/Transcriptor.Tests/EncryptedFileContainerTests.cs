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
    public void EncryptDecrypt_UsesInjectedSecretProtectorWithoutChangingContainerFormat()
    {
        var plaintext = RandomNumberGenerator.GetBytes(4097);
        var protector = new ReversibleTestSecretProtector();
        using var input = new MemoryStream(plaintext);
        using var encrypted = new MemoryStream();

        EncryptedFileContainer.Encrypt(input, encrypted, protector);

        encrypted.Position = 0;
        using var restored = new MemoryStream();
        EncryptedFileContainer.Decrypt(encrypted, restored, protector);

        Assert.Equal(plaintext, restored.ToArray());
        Assert.Equal(1, protector.ProtectCalls);
        Assert.Equal(1, protector.UnprotectCalls);
        Assert.Equal(EncryptedFileContainer.CurrentVersion,
            System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(encrypted.ToArray().AsSpan(8, 4)));
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
        var headerLength = 28 + System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(encrypted.AsSpan(24, 4)) + EncryptedFileContainer.TagSize;
        encrypted[headerLength + 4] ^= 0x40;
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

    [Fact]
    public void Encrypt_WritesVersionTwoWithAuthenticatedFooter()
    {
        var encrypted = Encrypt(new byte[] { 1, 2, 3 });
        Assert.Equal(2, System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(encrypted.AsSpan(8, 4)));
        Assert.Equal("TRNEND02", System.Text.Encoding.ASCII.GetString(encrypted, encrypted.Length - 36, 8));
    }

    [Fact]
    public void Decrypt_RejectsRemovalOfCompleteTrailingBlock()
    {
        var encrypted = Encrypt(RandomNumberGenerator.GetBytes(EncryptedFileContainer.BlockSize * 2));
        var headerLength = 28 + System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(encrypted.AsSpan(24, 4)) + EncryptedFileContainer.TagSize;
        var firstBlockLength = 4 + EncryptedFileContainer.BlockSize + EncryptedFileContainer.TagSize;
        var secondBlockStart = headerLength + firstBlockLength;
        var secondBlockLength = 4 + EncryptedFileContainer.BlockSize + EncryptedFileContainer.TagSize;
        var truncated = encrypted[..secondBlockStart]
            .Concat(encrypted[(secondBlockStart + secondBlockLength)..])
            .ToArray();

        Assert.Throws<InvalidDataException>(() => Decrypt(truncated));
    }

    [Fact]
    public void Decrypt_RejectsModifiedAuthenticatedFooter()
    {
        var encrypted = Encrypt(new byte[] { 1, 2, 3 });
        encrypted[^1] ^= 0x40;
        Assert.ThrowsAny<CryptographicException>(() => Decrypt(encrypted));
    }

    [Fact]
    public void Decrypt_RejectsModifiedFooterLength()
    {
        var encrypted = Encrypt(new byte[] { 1, 2, 3 });
        encrypted[encrypted.Length - 20] ^= 0x01;

        Assert.Throws<InvalidDataException>(() => Decrypt(encrypted));
    }

    [Fact]
    public void Decrypt_RemainsCompatibleWithLegacyVersionOne()
    {
        var plaintext = new byte[] { 9, 8, 7, 6 };
        var encrypted = EncryptLegacyVersionOne(plaintext);

        Assert.Equal(plaintext, Decrypt(encrypted));
    }

    private static byte[] EncryptLegacyVersionOne(byte[] plaintext)
    {
        var key = RandomNumberGenerator.GetBytes(EncryptedFileContainer.KeySize);
        var prefix = RandomNumberGenerator.GetBytes(EncryptedFileContainer.NoncePrefixSize);
        var protectedKey = EncryptedFileContainer.ProtectKey(key);
        var header = EncryptedFileContainer.BuildHeader(prefix, protectedKey, version: 1);
        var nonce = new byte[EncryptedFileContainer.NonceSize];
        var headerTag = new byte[EncryptedFileContainer.TagSize];
        using var output = new MemoryStream();
        using var aes = new AesGcm(key, EncryptedFileContainer.TagSize);
        try
        {
            EncryptedFileContainer.SetNonce(nonce, prefix, 0);
            aes.Encrypt(nonce, ReadOnlySpan<byte>.Empty, Span<byte>.Empty, headerTag, header);
            output.Write(header);
            output.Write(headerTag);
            if (plaintext.Length > 0)
            {
                Span<byte> length = stackalloc byte[4];
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(length, plaintext.Length);
                var ciphertext = new byte[plaintext.Length];
                var tag = new byte[EncryptedFileContainer.TagSize];
                var aad = EncryptedFileContainer.BuildBlockAad(header, 1, plaintext.Length);
                try
                {
                    EncryptedFileContainer.SetNonce(nonce, prefix, 1);
                    aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);
                    output.Write(length);
                    output.Write(ciphertext);
                    output.Write(tag);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(ciphertext);
                    CryptographicOperations.ZeroMemory(tag);
                    CryptographicOperations.ZeroMemory(aad);
                }
            }
            output.Write(new byte[4]);
            return output.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(prefix);
            CryptographicOperations.ZeroMemory(protectedKey);
            CryptographicOperations.ZeroMemory(header);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(headerTag);
        }
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

    private sealed class ReversibleTestSecretProtector : ISecretProtector
    {
        public int ProtectCalls { get; private set; }
        public int UnprotectCalls { get; private set; }

        public byte[] Protect(byte[] secret)
        {
            ProtectCalls++;
            return secret.Reverse().ToArray();
        }

        public byte[] Unprotect(byte[] protectedSecret)
        {
            UnprotectCalls++;
            return protectedSecret.Reverse().ToArray();
        }
    }

    private sealed class FailingReadStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new IOException("Simulated source read failure.");
    }
}