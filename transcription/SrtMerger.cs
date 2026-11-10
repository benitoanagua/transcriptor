using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Transcriptor.Transcription;

public sealed record TranscriptSegment(TimeSpan Start, TimeSpan End, string Speaker, string Text);

public static partial class SrtMerger
{
    private const long MaximumSrtBytes = 64L * 1024 * 1024;
    private const int MaximumSegments = 100_000;
    private const int MaximumSegmentTextCharacters = 32_768;
    [GeneratedRegex(@"^(?<start>\d{2,}:\d{2}:\d{2}[,.]\d{1,3})\s*-->\s*(?<end>\d{2,}:\d{2}:\d{2}[,.]\d{1,3})", RegexOptions.CultureInvariant)]
    private static partial Regex TimestampRegex();

    public static IReadOnlyList<TranscriptSegment> Parse(string path, string speaker)
    {
        if (!File.Exists(path))
            return [];

        var info = new FileInfo(path);
        if (info.Length > MaximumSrtBytes)
            throw new InvalidDataException($"El SRT '{path}' supera el límite de {MaximumSrtBytes} bytes.");

        var segments = new List<TranscriptSegment>();
        var content = File.ReadAllText(path, Encoding.UTF8);
        foreach (var block in Regex.Split(content.Trim(), @"\r?\n\s*\r?\n"))
        {
            var lines = block.Split(["\r\n", "\n"], StringSplitOptions.None);
            for (var index = 0; index < lines.Length; index++)
            {
                var match = TimestampRegex().Match(lines[index].Trim());
                if (!match.Success)
                    continue;

                var text = string.Join(" ", lines.Skip(index + 1).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()));
                if (text.Length == 0)
                    break;
                if (text.Length > MaximumSegmentTextCharacters)
                    throw new InvalidDataException($"Un segmento SRT en '{path}' supera el límite de texto permitido.");
                if (segments.Count >= MaximumSegments)
                    throw new InvalidDataException($"El SRT '{path}' supera el límite de {MaximumSegments} segmentos.");

                var start = SrtTimestampParser.Parse(match.Groups["start"].Value);
                var end = SrtTimestampParser.Parse(match.Groups["end"].Value);
                if (end < start)
                    throw new InvalidDataException($"El segmento SRT en '{path}' termina antes de comenzar.");

                segments.Add(new TranscriptSegment(start, end, speaker, text));
                break;
            }
        }

        return segments;
    }

    public static IReadOnlyList<TranscriptSegment> Merge(string directory)
    {
        var segments = Parse(Path.Combine(directory, "yo.srt"), "Yo")
            .Concat(Parse(Path.Combine(directory, "cliente.srt"), "Cliente"))
            .OrderBy(segment => segment.Start)
            .ThenBy(segment => segment.Speaker, StringComparer.Ordinal)
            .ThenBy(segment => segment.End)
            .ToArray();

        if (segments.Length == 0)
            throw new InvalidDataException($"No hay segmentos de transcripción válidos en '{directory}'.");

        return segments;
    }

    public static void Write(string directory, IReadOnlyList<TranscriptSegment> segments)
    {
        if (segments.Count == 0)
            throw new ArgumentException("No se puede escribir una transcripción vacía.", nameof(segments));

        var textPath = Path.Combine(directory, "transcripcion.txt");
        var srtPath = Path.Combine(directory, "transcripcion.srt");
        var textTemp = textPath + ".tmp";
        var srtTemp = srtPath + ".tmp";

        try
        {
            File.WriteAllLines(textTemp, segments.Select(segment =>
                $"[{Format(segment.Start, includeMilliseconds: false)}] {segment.Speaker}: {segment.Text}"), new UTF8Encoding(false));

            var srt = new StringBuilder();
            for (var index = 0; index < segments.Count; index++)
            {
                var segment = segments[index];
                srt.AppendLine((index + 1).ToString(CultureInfo.InvariantCulture));
                srt.Append(Format(segment.Start, includeMilliseconds: true));
                srt.Append(" --> ");
                srt.AppendLine(Format(segment.End, includeMilliseconds: true));
                srt.AppendLine($"{segment.Speaker}: {segment.Text}");
                srt.AppendLine();
            }
            File.WriteAllText(srtTemp, srt.ToString(), new UTF8Encoding(false));

            File.Move(srtTemp, srtPath, overwrite: true);
            // El TXT es el marcador de finalización y siempre se publica al final.
            File.Move(textTemp, textPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(textTemp)) File.Delete(textTemp);
            if (File.Exists(srtTemp)) File.Delete(srtTemp);
        }
    }

    private static string Format(TimeSpan value, bool includeMilliseconds)
    {
        var prefix = $"{(long)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
        return includeMilliseconds ? $"{prefix},{value.Milliseconds:000}" : prefix;
    }
}
