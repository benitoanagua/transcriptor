using NAudio.CoreAudioApi;

namespace Transcriptor.Rec;

public sealed record AudioDeviceOption(string Id, string Name);

/// <summary>Sesión de captura simultánea de micrófono y salida del sistema.</summary>
public sealed class RecordingSession : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;
    private readonly MMDevice _microphone;
    private readonly MMDevice _output;
    private readonly DualRecorder _recorder;
    private bool _started;
    private bool _stopped;
    private bool _disposed;

    public string MicrophoneName => _microphone.FriendlyName;
    public string OutputName => _output.FriendlyName;
    public TimeSpan Elapsed => _recorder.Elapsed;
    public float MicrophoneLevel => _recorder.MicLevel;
    public float SystemLevel => _recorder.SysLevel;

    public static IReadOnlyList<AudioDeviceOption> GetCaptureDevices() => GetDevices(DataFlow.Capture);
    public static IReadOnlyList<AudioDeviceOption> GetRenderDevices() => GetDevices(DataFlow.Render);

    public static string? GetDefaultDeviceId(DataFlow flow)
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
                device.Dispose();
        }
    }

    public RecordingSession(string outputDirectory, string? microphoneDeviceId = null, string? outputDeviceId = null)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("La carpeta de salida es obligatoria.", nameof(outputDirectory));

        _enumerator = new MMDeviceEnumerator();
        try
        {
            _microphone = string.IsNullOrWhiteSpace(microphoneDeviceId)
                ? _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications)
                : _enumerator.GetDevice(microphoneDeviceId);
            _output = string.IsNullOrWhiteSpace(outputDeviceId)
                ? _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Communications)
                : _enumerator.GetDevice(outputDeviceId);
            _recorder = new DualRecorder(_microphone, _output, outputDirectory);
        }
        catch
        {
            _enumerator.Dispose();
            throw;
        }
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) throw new InvalidOperationException("La sesión de grabación ya fue iniciada.");
        _recorder.Start();
        _started = true;
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_started || _stopped) return;
        _recorder.Stop();
        _stopped = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _recorder.Dispose(); }
        finally
        {
            _microphone.Dispose();
            _output.Dispose();
            _enumerator.Dispose();
        }
    }
}
