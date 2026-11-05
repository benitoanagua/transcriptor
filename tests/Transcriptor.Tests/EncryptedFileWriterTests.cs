using System.Security.Cryptography;
using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Tests;

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
    [Fact]
    public void IncrementalWriter_FailsClosedWhenDestinationWriteFails()
    {
        using var destination = new FailingWriteStream();
        using var writer = EncryptedFileContainer.CreateWriter(destination);
        destination.FailWrites = true;

        Assert.Throws<IOException>(() => writer.Write(new byte[EncryptedFileContainer.BlockSize]));
        Assert.Throws<IOException>(() => writer.Write(new byte[] { 1 }));
    }

    private sealed class FailingWriteStream : MemoryStream
    {
        public bool FailWrites { get; set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (FailWrites) throw new IOException("Simulated destination failure.");
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (FailWrites) throw new IOException("Simulated destination failure.");
            base.Write(buffer);
        }
    }

}
