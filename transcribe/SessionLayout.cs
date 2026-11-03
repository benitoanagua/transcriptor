namespace Transcriptor.Transcription;

/// <summary>
/// Canonical names and path construction for a recording session.
/// This class is deliberately filesystem-agnostic: callers retain responsibility
/// for validation, leases, ACLs, encryption and atomic publication.
/// </summary>
public sealed class SessionLayout
{
    public const string MicrophoneTrackName = "yo";
    public const string SystemTrackName = "cliente";
    public const string IncompleteMarkerFileName = ".recording-incomplete";

    private static readonly string[] TranscriptNames =
    [
        "yo.srt",
        "cliente.srt",
        "transcripcion.srt",
        "transcripcion.txt"
    ];

    public SessionLayout(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory = Path.GetFullPath(directory);
    }

    public string Directory { get; }

    public string AudioTrack(string trackName, bool encrypted = true)
    {
        ValidateTrackName(trackName);
        return Path.Combine(Directory, trackName + (encrypted ? ".trn" : ".wav"));
    }

    public string? ExistingAudioTrack(string trackName)
    {
        var encrypted = AudioTrack(trackName);
        if (File.Exists(encrypted)) return encrypted;
        var legacy = AudioTrack(trackName, encrypted: false);
        return File.Exists(legacy) ? legacy : null;
    }

    public string Transcript(string fileName, bool encrypted = true)
    {
        if (!TranscriptNames.Contains(fileName, StringComparer.Ordinal))
            throw new ArgumentException("Nombre de transcripción no reconocido.", nameof(fileName));
        return Path.Combine(Directory, fileName + (encrypted ? ".trn" : string.Empty));
    }

    public string IncompleteMarker => Path.Combine(Directory, IncompleteMarkerFileName);

    public string Temporary(string purpose, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        if (purpose.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || extension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("El propósito y la extensión deben ser nombres de archivo válidos.");
        var suffix = extension.StartsWith('.') ? extension : "." + extension;
        return Path.Combine(Directory, $".{Guid.NewGuid():N}.{purpose}{suffix}");
    }

    public static IReadOnlyList<string> TranscriptFileNames => Array.AsReadOnly(TranscriptNames);

    private static void ValidateTrackName(string trackName)
    {
        if (trackName is not (MicrophoneTrackName or SystemTrackName))
            throw new ArgumentException("Pista de audio no reconocida.", nameof(trackName));
    }
}
