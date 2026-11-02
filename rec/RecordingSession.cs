using NAudio.CoreAudioApi;

namespace Transcriptor.Rec;

/// <summary>
/// Sesión de captura simultánea de micrófono y salida del sistema.
/// Mantiene vivos los dispositivos WASAPI durante toda la grabación.
/// </summary>
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

    public RecordingSession(string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("La carpeta de salida es obligatoria.", nameof(outputDirectory));

        _enumerator = new MMDeviceEnumerator();
        try
        {
            _microphone = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            _output = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Communications);
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
        if (_started)
            throw new InvalidOperationException("La sesión de grabación ya fue iniciada.");

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
