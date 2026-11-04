using System.IO;

namespace Transcriptor.UI;

/// <summary>Creates canonical recording directories using an injectable local clock.</summary>
public static class RecordingDirectoryFactory
{
    public static string Create(string root, string reference, IClock clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(clock);

        var now = clock.LocalNow;
        var invalid = Path.GetInvalidFileNameChars().Append(' ').ToArray();
        var safeReference = string.Join("-", (string.IsNullOrWhiteSpace(reference) ? "Llamada" : reference.Trim())
            .Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeReference))
            safeReference = "Llamada";

        var directory = Path.Combine(root, now.ToString("yyyy-MM-dd"), now.ToString("HHmmss") + "_" + safeReference);
        Directory.CreateDirectory(directory);
        return directory;
    }
}
