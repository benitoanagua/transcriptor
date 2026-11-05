using NAudio.CoreAudioApi;
using Transcriptor.Transcription;

namespace Transcriptor.Platform.Windows;

/// <summary>
/// WASAPI device enumeration extracted from the former RecordingSession statics:
/// same active-endpoint filter, same friendly names and same default endpoint
/// (Communications role) resolution including the silent fallback to null.
/// </summary>
public sealed class WasapiDeviceCatalog : IDeviceCatalog
{
    public IReadOnlyList<AudioDeviceOption> GetCaptureDevices() => GetDevices(DataFlow.Capture);
    public IReadOnlyList<AudioDeviceOption> GetOutputDevices() => GetDevices(DataFlow.Render);

    public string? GetDefaultCaptureDeviceId() => GetDefaultDeviceId(DataFlow.Capture);
    public string? GetDefaultOutputDeviceId() => GetDefaultDeviceId(DataFlow.Render);

    private static string? GetDefaultDeviceId(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        try
        {
            using var device = enumerator.GetDefaultAudioEndpoint(flow, Role.Communications);
            return device.ID;
        }
        catch (Exception) { return null; }
    }

    private static IReadOnlyList<AudioDeviceOption> GetDevices(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        var result = new List<AudioDeviceOption>();
        var devices = enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active);
        try
        {
            foreach (var device in devices)
                result.Add(new AudioDeviceOption(device.ID, device.FriendlyName));
            return result;
        }
        finally
        {
            foreach (var device in devices)
            {
                try { device.Dispose(); }
                catch { /* Continuar liberando los demás dispositivos nativos. */ }
            }
        }
    }
}

/// <summary>Windows capability answers for the UI (loopback, DPAPI-backed storage and playback all available).</summary>
public sealed class WindowsPlatformCapabilities : IPlatformCapabilities
{
    public bool SupportsSystemLoopback => true;
    public bool SupportsEncryptionAtRest => true;
    public bool SupportsAudioPlayback => true;
    public bool PrefersNativeWindowChrome => true; // El marco nativo conserva resize, snap y controles del sistema.
    public string Name => "Windows";
}
