namespace Transcriptor.Transcription;

/// <summary>Identifies one audio device as exposed by the platform. Kept from the original recording layer.</summary>
public sealed record AudioDeviceOption(string Id, string Name);

/// <summary>
/// Port for enumerating input (capture) and output (loopback) audio devices and
/// reporting the platform default identifiers.
/// </summary>
public interface IDeviceCatalog
{
    IReadOnlyList<AudioDeviceOption> GetCaptureDevices();
    /// <summary>Output devices that can be captured as the system track (loopback), when supported.</summary>
    IReadOnlyList<AudioDeviceOption> GetOutputDevices();
    string? GetDefaultCaptureDeviceId();
    string? GetDefaultOutputDeviceId();
}
