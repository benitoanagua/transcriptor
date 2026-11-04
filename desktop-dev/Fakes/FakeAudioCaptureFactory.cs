using Transcriptor.Transcription;

namespace Transcriptor.Dev;

/// <summary>Factory used only by the development head.</summary>
public sealed class FakeAudioCaptureFactory : IAudioCaptureFactory
{
    public IAudioCaptureSession Create(string outputDirectory, string? microphoneDeviceId, string? systemDeviceId) =>
        new FakeAudioCaptureSession(outputDirectory, microphoneDeviceId, systemDeviceId);
}
