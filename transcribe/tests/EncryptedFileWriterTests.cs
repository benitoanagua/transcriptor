using System.Security.Cryptography;
using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class EncryptedFileWriterTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(65535)]
    [InlineData(65536)]
    [InlineData(65537)]
    [InlineData(196613)]
    public void IncrementalWriter_RoundTripsArbitraryWriteBoundaries(int length)
    {
        var expected = RandomNumberGenerator.GetBytes(length);
        using var encrypted = new MemoryStream();
        using (var writer = EncryptedFileContainer.CreateWriter(encrypted))
        {
            var offset = 0;
            while (offset < expected.Length)
            {
                var count = Math.Min((offset % 7919) + 1, expected.Length - offset);
                writer.Write(expected, offset, count);
                offset += count;
            }
        }

        encrypted.Position = 0;
        using var actual = new MemoryStream();
        EncryptedFileContainer.Decrypt(encrypted, actual);
        Assert.Equal(expected, actual.ToArray());
    }
}
