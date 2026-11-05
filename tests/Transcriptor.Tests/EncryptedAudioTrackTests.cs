using System.Buffers.Binary;
using System.Security.Cryptography;
using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Tests;

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
            var format = AudioTrackFormat.Pcm(16000, 1, 16);
            using (var writer = EncryptedAudioTrack.CreateWriter(encryptedPath, format))
                writer.Write(samples, 0, samples.Length);

            Assert.True(File.Exists(encryptedPath));
            Assert.False(File.Exists(Path.Combine(directory, "yo.wav")));

            var wavePath = EncryptedAudioTrack.DecryptToWaveFile(encryptedPath);
            try
            {
                var wave = File.ReadAllBytes(wavePath);
                Assert.Equal(44, wave.Length - samples.Length);
                Assert.True(wave.AsSpan().StartsWith("RIFF"u8));
                Assert.Equal(checked((uint)(36 + samples.Length)), BinaryPrimitives.ReadUInt32LittleEndian(wave.AsSpan(4)));
                Assert.True(wave.AsSpan(8).StartsWith("WAVE"u8));
                Assert.True(wave.AsSpan(12).StartsWith("fmt "u8));
                Assert.Equal(16, BinaryPrimitives.ReadInt32LittleEndian(wave.AsSpan(16)));
                Assert.Equal(AudioTrackFormat.PcmEncoding, BinaryPrimitives.ReadUInt16LittleEndian(wave.AsSpan(20)));
                Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(wave.AsSpan(22)));
                Assert.Equal(16000, BinaryPrimitives.ReadInt32LittleEndian(wave.AsSpan(24)));
                Assert.Equal(32000, BinaryPrimitives.ReadInt32LittleEndian(wave.AsSpan(28)));
                Assert.Equal(2, BinaryPrimitives.ReadUInt16LittleEndian(wave.AsSpan(32)));
                Assert.Equal(16, BinaryPrimitives.ReadUInt16LittleEndian(wave.AsSpan(34)));
                Assert.True(wave.AsSpan(36).StartsWith("data"u8));
                Assert.Equal((uint)samples.Length, BinaryPrimitives.ReadUInt32LittleEndian(wave.AsSpan(40)));
                Assert.True(wave.AsSpan(44).SequenceEqual(samples));
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
            BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(8, 2), AudioTrackFormat.PcmEncoding);
            BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(10, 2), channels);
            BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(12, 4), sampleRate);
            BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(16, 4), averageBytes);
            BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(20, 2), blockAlign);
            BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(22, 2), bits);
            using (var input = new MemoryStream(raw))
            using (var output = File.Create(encryptedPath))
                EncryptedFileContainer.Encrypt(input, output);

            Assert.Throws<InvalidDataException>(() => EncryptedAudioTrack.DecryptToWaveFile(encryptedPath));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.playback.wav"));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.audio.tmp"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData(16000, 1, 16000, 2, 16)]
    [InlineData(16000, 1, 48000, 3, 20)]
    [InlineData(16000, 1, 48000, 2, 24)]
    public void EncryptedAudioTrack_RejectsInconsistentPcmFormatBeforeCreatingFile(
        int sampleRate, int channels, int averageBytes, int blockAlign, int bits)
    {
        var directory = Path.Combine(Path.GetTempPath(), "TranscriptorAudioTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "invalid-format.trn");
        try
        {
            var format = new AudioTrackFormat(
                AudioTrackFormat.PcmEncoding, checked((ushort)channels), sampleRate, averageBytes,
                checked((ushort)blockAlign), checked((ushort)bits));

            Assert.Throws<NotSupportedException>(() => EncryptedAudioTrack.CreateWriter(path, format));
            Assert.False(File.Exists(path));
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
            // Codificación 2 (ADPCM en el espacio WAV clásico) nunca fue admitida
            // por la capa Windows; el contenedor portable la rechaza igual.
            var format = new AudioTrackFormat(2, 1, 16000, 8000, 1, 4);
            Assert.Throws<NotSupportedException>(() => EncryptedAudioTrack.CreateWriter(path, format));
            Assert.False(File.Exists(path));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
