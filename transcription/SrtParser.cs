using System.Globalization;
using System.Text.RegularExpressions;

namespace Transcriptor.Transcription;

public static partial class SrtParser
{
    private const int MaximumContentCharacters = 64 * 1024 * 1024;
    private const int MaximumSegments = 100_000;
    private const int MaximumTextCharacters = 32_768;

    [GeneratedRegex(@"^(?<start>\d{2,}:\d{2}:\d{2}[,.]\d{1,3})\s*-->\s*(?<end>\d{2,}:\d{2}:\d{2}[,.]\d{1,3})(?:\s+.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(@"^(?<speaker>Yo|Cliente):\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SpeakerRegex();

    public static IReadOnlyList<TranscriptSegment> Parse(string content, string defaultSpeaker = "")
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(defaultSpeaker);
        if (content.Length > MaximumContentCharacters)
            throw new InvalidDataException("La transcripción supera el tamaño máximo admitido.");
        if (content.Length == 0) return [];

        content = content.TrimStart('\uFEFF');
        var segments = new List<TranscriptSegment>();
        foreach (var block in Regex.Split(content.Trim(), @"\r?\n\s*\r?\n"))
        {
            if (string.IsNullOrWhiteSpace(block)) continue;
            var lines = block.Split(["\r\n", "\n"], StringSplitOptions.None);
            var timestampIndex = Array.FindIndex(lines, line => TimestampRegex().IsMatch(line.Trim()));
            if (timestampIndex < 0) continue;

            var textLines = lines.Skip(timestampIndex + 1)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim())
                .ToArray();
            if (textLines.Length == 0) continue;

            var firstLine = textLines[0];
            var speakerMatch = SpeakerRegex().Match(firstLine);
            var speaker = speakerMatch.Success ? speakerMatch.Groups["speaker"].Value : defaultSpeaker;
            if (speakerMatch.Success) textLines[0] = firstLine[speakerMatch.Length..];
            var text = string.Join(Environment.NewLine, textLines);
            if (text.Length == 0) continue;
            if (text.Length > MaximumTextCharacters)
                throw new InvalidDataException("Un segmento de la transcripción supera el tamaño máximo admitido.");
            if (segments.Count >= MaximumSegments)
                throw new InvalidDataException("La transcripción supera el número máximo de segmentos.");

            var match = TimestampRegex().Match(lines[timestampIndex].Trim());
            var start = ParseTimestamp(match.Groups["start"].Value);
            var end = ParseTimestamp(match.Groups["end"].Value);
            if (end < start) throw new InvalidDataException("Un segmento de la transcripción termina antes de comenzar.");
            segments.Add(new TranscriptSegment(start, end, speaker, text));
        }
        return segments;
    }

    private static TimeSpan ParseTimestamp(string value)
    {
        var parts = value.Replace(',', '.').Split(':');
        if (parts.Length != 3) throw new InvalidDataException($"La marca de tiempo '{value}' no es válida.");
        var seconds = parts[2].Split('.');
        if (seconds.Length != 2 || seconds[1].Length is < 1 or > 3
            || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || !int.TryParse(seconds[0], NumberStyles.None, CultureInfo.InvariantCulture, out var wholeSeconds)
            || !int.TryParse(seconds[1].PadRight(3, '0'), NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds)
            || minutes > 59 || wholeSeconds > 59)
            throw new InvalidDataException($"La marca de tiempo '{value}' no es válida.");

        try
        {
            return TimeSpan.FromTicks(checked(
                hours * TimeSpan.TicksPerHour
                + minutes * TimeSpan.TicksPerMinute
                + wholeSeconds * TimeSpan.TicksPerSecond
                + milliseconds * TimeSpan.TicksPerMillisecond));
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException($"La marca de tiempo '{value}' excede el rango admitido.", exception);
        }
    }
}
