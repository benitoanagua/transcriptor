using System.Runtime.CompilerServices;
using Transcriptor.Transcription;
using Transcriptor.UI;

namespace Transcriptor.Transcription.Tests;

/// <summary>
/// Shared test fakes for the portable suite. The reversible protector exercises the
/// real container format without DPAPI, so the .trn bytes are proven independent of
/// any Windows secret protection.
/// </summary>
public sealed class ReversibleTestSecretProtector : ISecretProtector
{
    public int ProtectCalls { get; private set; }
    public int UnprotectCalls { get; private set; }

    public byte[] Protect(byte[] secret)
    {
        ProtectCalls++;
        return secret.Reverse().ToArray();
    }

    public byte[] Unprotect(byte[] protectedSecret)
    {
        UnprotectCalls++;
        return protectedSecret.Reverse().ToArray();
    }
}

/// <summary>Inert migration adapter so portable tests can construct the LocalSessionStore port.</summary>
public sealed class ReversibleSessionMigration : ISessionMigrationService
{
    public RecordingMigrationResult MigrateSession(string sessionDirectory) => new(0, 0, 0);
}

/// <summary>Permissive security adapter standing in for the platform ACL implementation.</summary>
public sealed class PassThroughSessionSecurity : ISessionDirectorySecurity
{
    public void ValidateDirectoryPath(string directory) { }
    public void ValidateSessionDirectory(string sessionDirectory) { }
    public void RestrictSessionDirectory(string sessionDirectory) { }
}

/// <summary>Simple IDefaultPaths for tests; lives in a temporary directory.</summary>
public sealed class TestDefaultPaths : IDefaultPaths
{
    public string SettingsPath { get; } = Path.Combine(Path.GetTempPath(), "TranscriptorTests", "settings.json");
    public string RecordingsRoot { get; } = Path.Combine(Path.GetTempPath(), "TranscriptorTests", "Grabaciones");
    public string WhisperExecutablePath { get; } = "whisper-test";
    public string WhisperModelPath { get; } = "model-test.bin";
    public string? WhisperExecutablePickerPattern => null;
}

internal static class PortableTestSetup
{
    [ModuleInitializer]
    internal static void Initialize() =>
        EncryptedFileContainer.ConfigureDefaultSecretProtector(new ReversibleTestSecretProtector());
}
