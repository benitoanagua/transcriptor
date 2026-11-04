using System.IO;
using Transcriptor.Transcription;

namespace Transcriptor.UI;

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

/// <summary>
/// Filesystem-backed session store using the existing hardened lifecycle operations.
/// The platform security adapter and the (platform-owned) migration service are
/// injected by each composition root.
/// </summary>
public sealed class LocalSessionStore : ISessionStore
{
    private readonly ISessionDirectorySecurity _security;
    private readonly ISessionMigrationService _migration;

    public LocalSessionStore(ISessionDirectorySecurity security, ISessionMigrationService migration)
    {
        _security = security ?? throw new ArgumentNullException(nameof(security));
        _migration = migration ?? throw new ArgumentNullException(nameof(migration));
    }

    public IReadOnlyList<string> GetSessions(string recordingsRoot) =>
        RecordingPrivacy.GetSessions(recordingsRoot, _security);

    public RecordingMigrationResult MigrateSession(string sessionDirectory) =>
        _migration.MigrateSession(sessionDirectory);

    public void DeleteSession(string recordingsRoot, string sessionDirectory) =>
        RecordingPrivacy.DeleteSession(recordingsRoot, sessionDirectory, _security);

    public IReadOnlyList<string> ApplyRetention(string recordingsRoot, int retentionDays, DateTime utcNow) =>
        RecordingPrivacy.ApplyRetention(recordingsRoot, retentionDays, utcNow, _security);
}
