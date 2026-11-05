using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Tests;

public sealed class TranscriptionEngineTests : IDisposable
{
    private static readonly string[] Artifacts =
    [
        "yo.srt",
        "cliente.srt",
        "transcripcion.srt",
        "transcripcion.txt"
    ];

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "TranscriptorEngineTests", Guid.NewGuid().ToString("N"));

    public TranscriptionEngineTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void IsTranscriptComplete_ReturnsTrueWhenAllArtifactsAreEncryptedAndAuthenticated()
    {
        CreateEncryptedArtifacts();

        Assert.True(TranscriptionEngine.IsTranscriptComplete(_directory));
    }

    [Fact]
    public void IsTranscriptComplete_ReturnsFalseWhenAnArtifactIsMissing()
    {
        CreateEncryptedArtifacts();
        File.Delete(Path.Combine(_directory, "cliente.srt.trn"));

        Assert.False(TranscriptionEngine.IsTranscriptComplete(_directory));
    }

    [Fact]
    public void IsTranscriptComplete_ReturnsFalseWhenPlaintextArtifactRemains()
    {
        CreateEncryptedArtifacts();
        File.WriteAllText(Path.Combine(_directory, "transcripcion.txt"), "resultado residual");

        Assert.False(TranscriptionEngine.IsTranscriptComplete(_directory));
    }

    [Fact]
    public void IsTranscriptComplete_ReturnsFalseWhenAnEncryptedArtifactIsDamaged()
    {
        CreateEncryptedArtifacts();
        var damaged = Path.Combine(_directory, "yo.srt.trn");
        var bytes = File.ReadAllBytes(damaged);
        bytes[^20] ^= 0x40;
        File.WriteAllBytes(damaged, bytes);

        Assert.False(TranscriptionEngine.IsTranscriptComplete(_directory));
    }

    private void CreateEncryptedArtifacts()
    {
        foreach (var artifact in Artifacts)
        {
            var plaintext = Path.Combine(_directory, artifact);
            File.WriteAllText(plaintext, $"Contenido de prueba: {artifact}");
            EncryptedFileOperations.EncryptAndVerifyFile(plaintext, plaintext + ".trn", deletePlaintext: true);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
