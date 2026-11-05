using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Tests;

public sealed class SrtParserTests
{
    [Fact]
    public void Parse_HandlesBomCrLfCommaDecimalsAndMultilineText()
    {
        const string srt = "\uFEFF1\r\n00:00:01,25 --> 00:00:02.500\r\nYo: Hola\r\nsegunda línea\r\n\r\n2\r\n00:00:03,000 --> 00:00:04,250\r\nCliente: Respuesta\r\n";

        var segments = SrtParser.Parse(srt);

        Assert.Equal(2, segments.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(1250), segments[0].Start);
        Assert.Equal(TimeSpan.FromMilliseconds(2500), segments[0].End);
        Assert.Equal("Yo", segments[0].Speaker);
        Assert.Equal("Hola" + Environment.NewLine + "segunda línea", segments[0].Text);
        Assert.Equal("Cliente", segments[1].Speaker);
    }

    [Fact]
    public void Parse_UsesDefaultSpeakerForUnlabeledSegments()
    {
        var segments = SrtParser.Parse("00:00:00,000 --> 00:00:01,000\nTexto", "Micrófono");

        Assert.Single(segments);
        Assert.Equal("Micrófono", segments[0].Speaker);
        Assert.Equal("Texto", segments[0].Text);
    }

    [Fact]
    public void Parse_ReturnsEmptyForEmptyOrWhitespaceInput()
    {
        Assert.Empty(SrtParser.Parse(string.Empty));
        Assert.Empty(SrtParser.Parse(" \r\n\t"));
    }

    [Fact]
    public void Parse_RejectsAnEndBeforeTheStart()
    {
        Assert.Throws<InvalidDataException>(() =>
            SrtParser.Parse("00:00:02,000 --> 00:00:01,000\nTexto"));
    }
}
