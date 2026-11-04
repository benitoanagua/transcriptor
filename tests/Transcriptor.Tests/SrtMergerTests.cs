using System.Text;
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


    [Theory]
    [InlineData("00:60:00,000 --> 00:60:01,000")]
    [InlineData("00:00:60,000 --> 00:01:01,000")]
    [InlineData("999999999999999999999:00:00,000 --> 999999999999999999999:00:01,000")]
    public void Merge_RejectsOutOfRangeTimestamps(string timestamp)
    {
        File.WriteAllText(Path.Combine(_directory, "yo.srt"),
            $"1{Environment.NewLine}{timestamp}{Environment.NewLine}Texto inválido.{Environment.NewLine}");
        File.WriteAllText(Path.Combine(_directory, "cliente.srt"), string.Empty);

        Assert.Throws<InvalidDataException>(() => SrtMerger.Merge(_directory));
    }

    [Fact]
    public void Parse_RejectsSrtLargerThanConfiguredLimitBeforeReadingIt()
    {
        var path = Path.Combine(_directory, "oversized.srt");
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            stream.SetLength(64L * 1024 * 1024 + 1);

        Assert.Throws<InvalidDataException>(() => SrtMerger.Parse(path, "Yo"));
    }

    [Fact]
    public void Parse_RejectsOversizedSingleSegmentText()
    {
        var path = Path.Combine(_directory, "oversized-segment.srt");
        File.WriteAllText(path,
            $"1{Environment.NewLine}00:00:00,000 --> 00:00:01,000{Environment.NewLine}{new string('x', 32_769)}");

        Assert.Throws<InvalidDataException>(() => SrtMerger.Parse(path, "Yo"));
    }

    [Fact]
    public void Merge_RejectsEmptyTranscriptions()
    {
        Assert.Throws<InvalidDataException>(() => SrtMerger.Merge(_directory));
    }

    [Fact]
    public void Parse_RejectsSegmentThatEndsBeforeItStarts()
    {
        var path = Path.Combine(_directory, "reversed.srt");
        File.WriteAllText(path, """
            1
            00:00:12,000 --> 00:00:11,999
            Este segmento tiene el tiempo invertido.
            """);

        Assert.Throws<InvalidDataException>(() => SrtMerger.Parse(path, "Atacante"));
    }

    [Fact]
    public void Merge_HandlesManySegmentsAndReturnsStableChronologicalOrder()
    {
        var microphone = new StringBuilder();
        var client = new StringBuilder();
        for (var index = 0; index < 250; index++)
        {
            microphone.AppendLine((index + 1).ToString());
            var microphoneSeconds = 249 - index;
            microphone.AppendLine($"00:{microphoneSeconds / 60:D2}:{microphoneSeconds % 60:D2},000 --> 00:{microphoneSeconds / 60:D2}:{microphoneSeconds % 60:D2},500");
            microphone.AppendLine($"Micrófono {index}");
            microphone.AppendLine();
            client.AppendLine((index + 1).ToString());
            client.AppendLine($"00:{index / 60:D2}:{index % 60:D2},000 --> 00:{index / 60:D2}:{index % 60:D2},500");
            client.AppendLine($"Sistema {index}");
            client.AppendLine();
        }

        File.WriteAllText(Path.Combine(_directory, "yo.srt"), microphone.ToString());
        File.WriteAllText(Path.Combine(_directory, "cliente.srt"), client.ToString());

        var merged = SrtMerger.Merge(_directory);

        Assert.Equal(500, merged.Count);
        Assert.Equal(merged.OrderBy(segment => segment.Start).ThenBy(segment => segment.Speaker, StringComparer.Ordinal), merged);
        Assert.Contains(merged, segment => segment.Text == "Micrófono 249");
        Assert.Contains(merged, segment => segment.Text == "Sistema 249");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
