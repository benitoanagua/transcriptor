namespace Transcriptor.Transcription;

/// <summary>
/// Port for the platform-specific session directory protections (NTFS ACLs and
/// reparse-point rejection on Windows). Every operation that reads, writes or
/// deletes session content must funnel through these methods so traversal,
/// reparse-point and permission defenses remain in a single platform-owned place.
/// </summary>
public interface ISessionDirectorySecurity
{
    /// <summary>Validates that the whole path is real and free of reparse points.</summary>
    void ValidateDirectoryPath(string directory);

    /// <summary>Validates an existing session directory, including its contents.</summary>
    void ValidateSessionDirectory(string sessionDirectory);

    /// <summary>Restricts an existing session directory to the current user and administrators.</summary>
    void RestrictSessionDirectory(string sessionDirectory);
}
