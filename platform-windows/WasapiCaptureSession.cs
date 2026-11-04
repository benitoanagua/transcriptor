using NAudio.CoreAudioApi;
using Transcriptor.Transcription;

namespace Transcriptor.Platform.Windows;

/// <summary>
/// Windows/WASAPI implementation of the capture port. Device resolution, session
/// lifecycle gating and disposal semantics are the ones previously exposed by
/// RecordingSession; level reads are new, read-only metrics (peak preserved 0..1),
/// and never affect the bytes persisted into the encrypted tracks.
/// </summary>
public sealed class WasapiCaptureSession : IAudioCaptureSession
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
    public string? MicrophoneDeviceName => MicrophoneName;
    public string? SystemDeviceName => OutputName;
    public TimeSpan Elapsed => _recorder.Elapsed;
    public float MicrophonePeak => _recorder.MicLevel;
    public float MicrophoneRms => _recorder.MicRms;
    public float SystemPeak => _recorder.SysLevel;
    public float SystemRms => _recorder.SysRms;
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

    public WasapiCaptureSession(string outputDirectory, string? microphoneDeviceId = null, string? outputDeviceId = null)
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

/// <summary>Factory adapter used by the composition roots to satisfy IAudioCaptureFactory.</summary>
public sealed class WasapiCaptureFactory : IAudioCaptureFactory
{
    public IAudioCaptureSession Create(string outputDirectory, string? microphoneDeviceId, string? systemDeviceId) =>
        new WasapiCaptureSession(outputDirectory, microphoneDeviceId, systemDeviceId);
}
