using Transcriptor.Transcription;

namespace Transcriptor.UI;

/// <summary>
/// Composition bundle that each application head wires at startup. The UI never
/// constructs platform services itself and never assumes a particular host
/// platform; portable code sees only these ports.
/// </summary>
public sealed record UiServices(
    IDeviceCatalog Devices,
    IAudioCaptureFactory CaptureFactory,
    IAudioPlayerFactory PlayerFactory,
    ISessionDirectorySecurity Security,
    ISessionMigrationService Migration,
    IPlatformCapabilities Capabilities,
    IClock Clock,
    IDefaultPaths Paths,
    Func<string, string, string, TranscriptionEngine> CreateTranscriptionEngine,
    Action<string?> ApplyTheme)
{
    public ISessionStore CreateSessionStore() => new LocalSessionStore(Security, Migration);
}
