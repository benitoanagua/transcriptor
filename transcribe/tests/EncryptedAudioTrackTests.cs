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
