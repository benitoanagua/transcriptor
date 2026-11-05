using NAudio.CoreAudioApi;
using NAudio.Wave;
using Transcriptor.Transcription;

namespace Transcriptor.Platform.Windows;

/// <summary>WASAPI level-only monitoring; this type never creates a session or audio file.</summary>
public sealed class WasapiAudioLevelMonitor : IAudioLevelMonitor
{
    private readonly MMDeviceEnumerator _enumerator;
    private readonly MMDevice _microphoneDevice;
    private readonly MMDevice? _systemDevice;
    private readonly WasapiCapture _microphone;
    private readonly WasapiLoopbackCapture? _system;
    private int _microphoneStarted;
    private int _systemStarted;
    private int _disposed;
    private volatile float _microphonePeak;
    private volatile float _microphoneRms;
    private volatile float _systemPeak;
    private volatile float _systemRms;

    public event Action<Exception>? Faulted;
    public float MicrophonePeak => _microphonePeak;
    public float MicrophoneRms => _microphoneRms;
    public float SystemPeak => _systemPeak;
    public float SystemRms => _systemRms;

    public WasapiAudioLevelMonitor(
        string? microphoneDeviceId, string? systemDeviceId, bool includeSystemLoopback)
    {
        _enumerator = new MMDeviceEnumerator();
        MMDevice? microphone = null;
        MMDevice? systemDevice = null;
        WasapiCapture? microphoneCapture = null;
        WasapiLoopbackCapture? systemCapture = null;
        try
        {
            microphone = string.IsNullOrWhiteSpace(microphoneDeviceId)
                ? _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications)
                : _enumerator.GetDevice(microphoneDeviceId);
            if (includeSystemLoopback)
            {
                systemDevice = string.IsNullOrWhiteSpace(systemDeviceId)
                    ? _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Communications)
                    : _enumerator.GetDevice(systemDeviceId);
            }

            microphoneCapture = new WasapiCapture(microphone);
            if (systemDevice is not null)
                systemCapture = new WasapiLoopbackCapture(systemDevice);

            _microphoneDevice = microphone;
            _systemDevice = systemDevice;
            _microphone = microphoneCapture;
            _system = systemCapture;
            _microphone.DataAvailable += (_, args) =>
            {
                var (peak, rms) = Measure(args.Buffer, args.BytesRecorded, _microphone.WaveFormat);
                _microphonePeak = peak;
                _microphoneRms = rms;
            };
            _microphone.RecordingStopped += (_, args) =>
            {
                if (args.Exception is not null) Faulted?.Invoke(args.Exception);
            };
            if (_system is not null)
            {
                _system.DataAvailable += (_, args) =>
                {
                    var (peak, rms) = Measure(args.Buffer, args.BytesRecorded, _system.WaveFormat);
                    _systemPeak = peak;
                    _systemRms = rms;
                };
                _system.RecordingStopped += (_, args) =>
                {
                    if (args.Exception is not null) Faulted?.Invoke(args.Exception);
                };
            }
        }
        catch
        {
            systemCapture?.Dispose();
            microphoneCapture?.Dispose();
            systemDevice?.Dispose();
            microphone?.Dispose();
            _enumerator.Dispose();
            throw;
        }
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Volatile.Read(ref _microphoneStarted) != 0 || Volatile.Read(ref _systemStarted) != 0)
            throw new InvalidOperationException("La prueba de niveles ya está iniciada.");

        try
        {
            _microphone.StartRecording();
            Volatile.Write(ref _microphoneStarted, 1);
            if (_system is not null)
            {
                _system.StartRecording();
                Volatile.Write(ref _systemStarted, 1);
            }
        }
        catch (Exception startException)
        {
            try { Stop(); }
            catch (Exception stopException)
            {
                throw new AggregateException(
                    "No se pudo iniciar ni detener limpiamente el monitor de audio.",
                    startException, stopException);
            }
            throw;
        }
    }

    public void Stop()
    {
        Exception? failure = null;
        if (Interlocked.Exchange(ref _microphoneStarted, 0) != 0)
        {
            try { _microphone.StopRecording(); }
            catch (Exception exception) { failure = exception; }
        }
        if (Interlocked.Exchange(ref _systemStarted, 0) != 0 && _system is not null)
        {
            try { _system.StopRecording(); }
            catch (Exception exception) { failure ??= exception; }
        }

        _microphonePeak = 0;
        _microphoneRms = 0;
        _systemPeak = 0;
        _systemRms = 0;
        if (failure is not null)
            throw new IOException("No se pudieron detener ambos dispositivos del monitor de audio.", failure);
    }

    private static (float Peak, float Rms) Measure(byte[] buffer, int count, WaveFormat format)
    {
        var encoding = format.Encoding;
        if (format is WaveFormatExtensible extensible)
        {
            var pcm = new Guid("00000001-0000-0010-8000-00AA00389B71");
            var ieeeFloat = new Guid("00000003-0000-0010-8000-00AA00389B71");
            if (extensible.SubFormat == pcm) encoding = WaveFormatEncoding.Pcm;
            else if (extensible.SubFormat == ieeeFloat) encoding = WaveFormatEncoding.IeeeFloat;
        }

        double squareSum = 0;
        var sampleCount = 0;
        var peak = 0d;
        void Add(double sample)
        {
            if (!double.IsFinite(sample)) return;
            var magnitude = Math.Abs(sample);
            peak = Math.Max(peak, magnitude);
            squareSum += sample * sample;
            sampleCount++;
        }

        if (encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            for (var index = 0; index + 4 <= count; index += 4)
                Add(BitConverter.ToSingle(buffer, index));
        }
        else if (encoding == WaveFormatEncoding.Pcm)
        {
            switch (format.BitsPerSample)
            {
                case 8:
                    for (var index = 0; index < count; index++)
                        Add((buffer[index] - 128) / 128d);
                    break;
                case 16:
                    for (var index = 0; index + 2 <= count; index += 2)
                        Add(BitConverter.ToInt16(buffer, index) / 32768d);
                    break;
                case 24:
                    for (var index = 0; index + 3 <= count; index += 3)
                    {
                        var sample = buffer[index] | buffer[index + 1] << 8 | buffer[index + 2] << 16;
                        if ((sample & 0x800000) != 0) sample |= unchecked((int)0xFF000000);
                        Add(sample / 8388608d);
                    }
                    break;
                case 32:
                    for (var index = 0; index + 4 <= count; index += 4)
                        Add(BitConverter.ToInt32(buffer, index) / 2147483648d);
                    break;
            }
        }

        return sampleCount == 0
            ? (0, 0)
            : ((float)Math.Min(peak, 1), (float)Math.Min(Math.Sqrt(squareSum / sampleCount), 1));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { Stop(); }
        finally
        {
            try { _microphone.Dispose(); }
            finally
            {
                try { _system?.Dispose(); }
                finally
                {
                    try { _microphoneDevice.Dispose(); }
                    finally
                    {
                        try { _systemDevice?.Dispose(); }
                        finally { _enumerator.Dispose(); }
                    }
                }
            }
        }
    }
}

public sealed class WasapiAudioLevelMonitorFactory : IAudioLevelMonitorFactory
{
    public IAudioLevelMonitor Create(
        string? microphoneDeviceId, string? systemDeviceId, bool includeSystemLoopback) =>
        new WasapiAudioLevelMonitor(microphoneDeviceId, systemDeviceId, includeSystemLoopback);
}
