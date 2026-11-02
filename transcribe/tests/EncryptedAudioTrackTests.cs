using System.Security.Cryptography;
using NAudio.Wave;
using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class EncryptedAudioTrackTests
{
    [Fact]
    public void EncryptedAudioTrack_RoundTripsPcmSamplesAndWaveFormat()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TranscriptorAudioTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var encryptedPath = Path.Combine(directory, "yo.trn");
        var samples = RandomNumberGenerator.GetBytes(96_000);
        try
        {
            var format = new WaveFormat(16000, 16, 1);
            using (var writer = EncryptedAudioTrack.CreateWriter(encryptedPath, format))
                writer.Write(samples, 0, samples.Length);

            Assert.True(File.Exists(encryptedPath));
            Assert.False(File.Exists(Path.Combine(directory, "yo.wav")));

            var wavePath = EncryptedAudioTrack.DecryptToWaveFile(encryptedPath);
            try
            {
                using var reader = new WaveFileReader(wavePath);
                Assert.Equal(16000, reader.WaveFormat.SampleRate);
                Assert.Equal(16, reader.WaveFormat.BitsPerSample);
                Assert.Equal(1, reader.WaveFormat.Channels);
                using var actual = new MemoryStream();
                var buffer = new byte[8192];
                int read;
                while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                    actual.Write(buffer, 0, read);
                Assert.Equal(samples, actual.ToArray());
            }
            finally { File.Delete(wavePath); }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData(1, 16000, 16000, 2, 16)]
    [InlineData(1, 16000, 32001, 2, 16)]
    [InlineData(1, 16000, 48000, 2, 24)]
    [InlineData(1, 16000, 128000, 8, 64)]
    public void DecryptToWaveFile_RejectsInconsistentAudioHeaders(
        ushort channels, int sampleRate, int averageBytes, ushort blockAlign, ushort bits)
    {
        var directory = Path.Combine(Path.GetTempPath(), "TranscriptorAudioTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var encryptedPath = Path.Combine(directory, "invalid.trn");
        try
        {
            var raw = new byte[24 + blockAlign];
            "TRNAUD01"u8.CopyTo(raw);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(8, 2), (ushort)WaveFormatEncoding.Pcm);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(10, 2), channels);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(12, 4), sampleRate);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(16, 4), averageBytes);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(20, 2), blockAlign);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(22, 2), bits);
            using (var input = new MemoryStream(raw))
            using (var output = File.Create(encryptedPath))
                EncryptedFileContainer.Encrypt(input, output);

            Assert.Throws<InvalidDataException>(() => EncryptedAudioTrack.DecryptToWaveFile(encryptedPath));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.playback.wav"));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.audio.tmp"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void EncryptedAudioTrack_RejectsUnsupportedEncodingBeforeCreatingFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TranscriptorAudioTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "unsupported.trn");
        try
        {
            var format = WaveFormat.CreateCustomFormat(WaveFormatEncoding.Adpcm, 16000, 1, 8000, 1, 4);
            Assert.Throws<NotSupportedException>(() => EncryptedAudioTrack.CreateWriter(path, format));
            Assert.False(File.Exists(path));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
