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

    public event Action<Exception>? Faulted
    {
        add => _recorder.Faulted += value;
        remove => _recorder.Faulted -= value;
    }

    public string MicrophoneName => _microphone.FriendlyName;
    public string OutputName => _output.FriendlyName;
    public TimeSpan Elapsed => _recorder.Elapsed;
    public float MicrophoneLevel => _recorder.MicLevel;
    public float SystemLevel => _recorder.SysLevel;
    public bool IsPaused => _recorder.IsPaused;

    public void Pause()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_started || _stopped) throw new InvalidOperationException("No hay una grabación activa que pausar.");
        _recorder.Pause();
    }

    public void Resume()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_started || _stopped) throw new InvalidOperationException("No hay una grabación activa que reanudar.");
        _recorder.Resume();
    }

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
            {
                try { device.Dispose(); }
                catch { /* Continuar liberando los demás dispositivos nativos. */ }
            }
        }
    }

    public RecordingSession(string outputDirectory, string? microphoneDeviceId = null, string? outputDeviceId = null)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("La carpeta de salida es obligatoria.", nameof(outputDirectory));

        _enumerator = new MMDeviceEnumerator();
        MMDevice? microphone = null;
        MMDevice? output = null;
        DualRecorder? recorder = null;
        try
        {
            microphone = string.IsNullOrWhiteSpace(microphoneDeviceId)
                ? _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications)
                : _enumerator.GetDevice(microphoneDeviceId);
            output = string.IsNullOrWhiteSpace(outputDeviceId)
                ? _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Communications)
                : _enumerator.GetDevice(outputDeviceId);
            recorder = new DualRecorder(microphone, output, outputDirectory);

            _microphone = microphone;
            _output = output;
            _recorder = recorder;
        }
        catch
        {
            TryDispose(recorder);
            TryDispose(microphone);
            TryDispose(output);
            TryDispose(_enumerator);
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

    private static void TryDispose(IDisposable? resource)
    {
        try { resource?.Dispose(); }
        catch { /* La liberación de un recurso no debe impedir liberar los demás. */ }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _recorder.Dispose(); }
        finally
        {
            try { _microphone.Dispose(); }
            finally
            {
                try { _output.Dispose(); }
                finally { _enumerator.Dispose(); }
            }
        }
    }
}
