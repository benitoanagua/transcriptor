using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class SrtMergerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"transcriptor-tests-{Guid.NewGuid():N}");

    public SrtMergerTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void Merge_SortsTracksAndKeepsSpeakerLabels()
    {
        File.WriteAllText(Path.Combine(_directory, "yo.srt"), """
            1
            00:00:02,250 --> 00:00:03,000
            Hola desde el micrófono.

            """);
        File.WriteAllText(Path.Combine(_directory, "cliente.srt"), """
            1
            00:00:01.500 --> 00:00:02.000
            Hola, te escucho.

            """);

        var segments = SrtMerger.Merge(_directory);

        Assert.Equal(2, segments.Count);
        Assert.Equal("Cliente", segments[0].Speaker);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), segments[0].Start);
        Assert.Equal("Yo", segments[1].Speaker);
        Assert.Equal("Hola desde el micrófono.", segments[1].Text);
    }

    [Fact]
    public void Write_CreatesTextAndSrtOutputs()
    {
        var segments = new[]
        {
            new TranscriptSegment(TimeSpan.FromSeconds(1.25), TimeSpan.FromSeconds(2), "Yo", "Prueba.")
        };

        SrtMerger.Write(_directory, segments);

        Assert.Contains("[00:00:01] Yo: Prueba.", File.ReadAllText(Path.Combine(_directory, "transcripcion.txt")));
        Assert.Contains("00:00:01,250 --> 00:00:02,000", File.ReadAllText(Path.Combine(_directory, "transcripcion.srt")));
    }

    [Fact]
    public void Merge_RejectsEmptyTranscriptions()
    {
        Assert.Throws<InvalidDataException>(() => SrtMerger.Merge(_directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
