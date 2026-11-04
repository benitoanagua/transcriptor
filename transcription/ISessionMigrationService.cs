namespace Transcriptor.Transcription;

/// <summary>Outcome of migrating a legacy plaintext session to encrypted tracks.</summary>
public sealed record RecordingMigrationResult(int AudioTracksMigrated, int TranscriptsMigrated, int AlreadyEncrypted);

/// <summary>
/// Port for resumable migration of legacy plaintext sessions into encrypted copies.
/// The Windows implementation verifies every encrypted copy before deleting any
/// plaintext original; deletions and verification semantics must not change.
/// </summary>
public interface ISessionMigrationService
{
    RecordingMigrationResult MigrateSession(string sessionDirectory);
}
