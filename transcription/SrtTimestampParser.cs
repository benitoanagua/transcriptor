using System.Globalization;
using System.Text.RegularExpressions;

namespace Transcriptor.Transcription;

/// <summary>
/// Centralized SRT timestamp parsing logic shared by SrtParser and SrtMerger.
/// </summary>
internal static partial class SrtTimestampParser
{
    [GeneratedRegex(@"^(?<hours>\d{2,}):(?<minutes>\d{2}):(?<seconds>\d{2})[,.](?<milliseconds>\d{1,3})$", RegexOptions.CultureInvariant)]
    private static partial Regex TimestampRegex();

    public static TimeSpan Parse(string value)
    {
        var match = TimestampRegex().Match(value);
        if (!match.Success)
            throw new InvalidDataException($"La marca de tiempo '{value}' no es válida.");

        int hours, minutes, seconds, milliseconds;
        try
        {
            hours = int.Parse(match.Groups["hours"].Value, NumberStyles.None, CultureInfo.InvariantCulture);
            minutes = int.Parse(match.Groups["minutes"].Value, NumberStyles.None, CultureInfo.InvariantCulture);
            seconds = int.Parse(match.Groups["seconds"].Value, NumberStyles.None, CultureInfo.InvariantCulture);
            milliseconds = int.Parse(match.Groups["milliseconds"].Value.PadRight(3, '0'), NumberStyles.None, CultureInfo.InvariantCulture);
        }
        catch (OverflowException ex)
        {
            throw new InvalidDataException($"La marca de tiempo '{value}' excede el rango admitido.", ex);
        }

        if (minutes > 59 || seconds > 59)
            throw new InvalidDataException($"La marca de tiempo '{value}' no es válida.");

        try
        {
            return TimeSpan.FromTicks(
                hours * TimeSpan.TicksPerHour +
                minutes * TimeSpan.TicksPerMinute +
                seconds * TimeSpan.TicksPerSecond +
                milliseconds * TimeSpan.TicksPerMillisecond);
        }
        catch (OverflowException ex)
        {
            throw new InvalidDataException($"La marca de tiempo '{value}' excede el rango admitido.", ex);
        }
    }
}
