namespace Transcriptor.Desktop;

/// <summary>
/// Application port for listing, migrating, retaining and deleting local recording sessions.
/// Implementations must preserve session leases, path validation, ACLs and verified encryption.
/// </summary>
public interface ISessionStore
{
    IReadOnlyList<string> GetSessions(string recordingsRoot);
    RecordingMigrationResult MigrateSession(string sessionDirectory);
    void DeleteSession(string recordingsRoot, string sessionDirectory);
    IReadOnlyList<string> ApplyRetention(string recordingsRoot, int retentionDays, DateTime utcNow);
}

/// <summary>Filesystem-backed session store using the existing hardened lifecycle operations.</summary>
public sealed class LocalSessionStore : ISessionStore
{
    public IReadOnlyList<string> GetSessions(string recordingsRoot) =>
        RecordingPrivacy.GetSessions(recordingsRoot);

    public RecordingMigrationResult MigrateSession(string sessionDirectory) =>
        RecordingMigrationService.MigrateSession(sessionDirectory);

    public void DeleteSession(string recordingsRoot, string sessionDirectory) =>
        RecordingPrivacy.DeleteSession(recordingsRoot, sessionDirectory);

    public IReadOnlyList<string> ApplyRetention(string recordingsRoot, int retentionDays, DateTime utcNow) =>
        RecordingPrivacy.ApplyRetention(recordingsRoot, retentionDays, utcNow);
}
