using System.Security.Cryptography;
using NAudio.Wave;
using Transcriptor.Desktop;
using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class RecordingMigrationServiceTests
{
    [Fact]
    public void MigrateSession_EncryptsAndVerifiesEveryLegacyArtifactBeforeDeletingOriginals()
    {
        var directory = CreateDirectory();
        var wave = Path.Combine(directory, "yo.wav");
        var transcript = Path.Combine(directory, "transcripcion.txt");
        var samples = RandomNumberGenerator.GetBytes(64_000);
        try
        {
            using (var writer = new WaveFileWriter(wave, new WaveFormat(16000, 16, 1)))
                writer.Write(samples, 0, samples.Length);
            File.WriteAllText(transcript, "Yo: prueba de migración");

            var result = RecordingMigrationService.MigrateSession(directory);

            Assert.Equal(1, result.AudioTracksMigrated);
            Assert.Equal(1, result.TranscriptsMigrated);
            Assert.False(File.Exists(wave));
            Assert.False(File.Exists(transcript));
            Assert.True(File.Exists(Path.Combine(directory, "yo.trn")));
            Assert.True(File.Exists(Path.Combine(directory, "transcripcion.txt.trn")));

            var restoredWave = EncryptedAudioTrack.DecryptToWaveFile(Path.Combine(directory, "yo.trn"));
            try
            {
                using var reader = new WaveFileReader(restoredWave);
                using var actual = new MemoryStream();
                var buffer = new byte[8192];
                int read;
                while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                    actual.Write(buffer, 0, read);
                Assert.Equal(samples, actual.ToArray());
            }
            finally { File.Delete(restoredWave); }

            var restoredText = EncryptedFileOperations.DecryptToTemporaryFile(Path.Combine(directory, "transcripcion.txt.trn"));
            try { Assert.Equal("Yo: prueba de migración", File.ReadAllText(restoredText)); }
            finally { File.Delete(restoredText); }

            var resumed = RecordingMigrationService.MigrateSession(directory);
            Assert.Equal(0, resumed.AudioTracksMigrated);
            Assert.Equal(0, resumed.TranscriptsMigrated);
            Assert.Equal(2, resumed.AlreadyEncrypted);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void MigrateSession_RejectsSessionOwnedByActiveTranscription()
    {
        var directory = CreateDirectory();
        try
        {
            using var lease = TranscriptionSessionLease.TryAcquire(directory);
            Assert.NotNull(lease);

            Assert.Throws<InvalidOperationException>(() => RecordingMigrationService.MigrateSession(directory));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void MigrateSession_PreservesPlaintextWhenAnExistingEncryptedCopyIsInvalid()
    {
        var directory = CreateDirectory();
        var wave = Path.Combine(directory, "yo.wav");
        var encrypted = Path.Combine(directory, "yo.trn");
        try
        {
            using (var writer = new WaveFileWriter(wave, new WaveFormat(16000, 16, 1)))
                writer.Write(RandomNumberGenerator.GetBytes(32_000), 0, 32_000);
            File.WriteAllText(encrypted, "not a valid encrypted container");

            Assert.ThrowsAny<Exception>(() => RecordingMigrationService.MigrateSession(directory));
            Assert.True(File.Exists(wave));
            Assert.True(File.Exists(encrypted));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void MigrateSession_DoesNotDeleteAnyOriginalUntilEveryArtifactIsVerified()
    {
        var directory = CreateDirectory();
        var wave = Path.Combine(directory, "yo.wav");
        var transcript = Path.Combine(directory, "yo.srt");
        try
        {
            using (var writer = new WaveFileWriter(wave, new WaveFormat(16000, 16, 1)))
                writer.Write(RandomNumberGenerator.GetBytes(32_000), 0, 32_000);
            File.WriteAllText(transcript, "1\\n00:00:00,000 --> 00:00:01,000\\nPrueba");
            File.WriteAllText(transcript + ".trn", "contenedor inválido");

            Assert.ThrowsAny<Exception>(() => RecordingMigrationService.MigrateSession(directory));

            Assert.True(File.Exists(wave));
            Assert.True(File.Exists(transcript));
            Assert.True(File.Exists(Path.Combine(directory, "yo.trn")));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string CreateDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TranscriptorMigrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
