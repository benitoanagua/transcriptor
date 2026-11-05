using System.IO;

namespace Transcriptor.UI;

/// <summary>Creates canonical recording directories using an injectable local clock.</summary>
public static class RecordingDirectoryFactory
{
    /// <summary>
    /// Unport of the invalid-name characters as a superset: Windows forbids
    /// &lt; &gt; : " / \ | ? and control chars, Unix only 0x00 and '/'. Using the union
    /// makes the sanitized session name identical on every platform instead of
    /// allowing Windows-only characters to survive on Unix paths.
    /// </summary>
    private static readonly char[] InvalidNameChars =
    [
        .. Path.GetInvalidFileNameChars(),
        '<', '>', ':', '"', '|', '?'
    ];

    public static string Create(string root, string reference, IClock clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(clock);

        var now = clock.LocalNow;
        var invalid = InvalidNameChars.Append(' ').ToArray();
        var safeReference = string.Join("-", (string.IsNullOrWhiteSpace(reference) ? "Llamada" : reference.Trim())
            .Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeReference))
            safeReference = "Llamada";

        var directory = Path.Combine(root, now.ToString("yyyy-MM-dd"), now.ToString("HHmmss") + "_" + safeReference);
        Directory.CreateDirectory(directory);
        return directory;
    }
}
