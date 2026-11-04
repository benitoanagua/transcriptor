using System.IO;
using Transcriptor.Transcription;

namespace Transcriptor.Dev;

// ⚠️ DATOS FICTICIOS — SOLO DESARROLLO ⚠️
// Dispositivos inventados para probar selectores y flujos sin hardware real.
public sealed class FakeDeviceCatalog : IDeviceCatalog
{
    private static readonly AudioDeviceOption[] CaptureDevices =
    [
        new("dev-mic-a", "Micrófono de desarrollo (Auriculares)"),
        new("dev-mic-b", "Micrófono de desarrollo (Escritorio)"),
        new("dev-mic-c", "Webcam de desarrollo (micro base)")
    ];

    private static readonly AudioDeviceOption[] OutputDevices =
    [
        new("dev-out-a", "Altavoces de desarrollo (HDMI)"),
        new("dev-out-b", "Auriculares de desarrollo (USB)"),
        new("dev-out-c", "Monitor de desarrollo (DisplayPort)")
    ];

    public IReadOnlyList<AudioDeviceOption> GetCaptureDevices() => CaptureDevices;
    public IReadOnlyList<AudioDeviceOption> GetOutputDevices() => OutputDevices;
    public string? GetDefaultCaptureDeviceId() => "dev-mic-a";
    public string? GetDefaultOutputDeviceId() => "dev-out-a";
}
