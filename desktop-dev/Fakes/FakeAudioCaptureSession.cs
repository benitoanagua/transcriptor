using System.Buffers.Binary;
using System.Diagnostics;
using Transcriptor.Transcription;

namespace Transcriptor.Dev;

// ⚠️ DATOS FICTICIOS — SOLO DESARROLLO ⚠️
// Señal sintética creíble: MIC alterna tramos tipo habla (envolvente), picos
// ocasionales cerca del clipping y tramos de silencio para probar la alerta de
// pista silenciosa; SISTEMA simula música/ruido continuo del sistema.
// Escribe pistas .trn con el protector inseguro de desarrollo y mantiene las
// mismas reglas de lectura de niveles (pico/RMS son indicadores de pantalla y
// nunca alteran los bytes escritos a las pistas).
public sealed class FakeAudioCaptureSession : IAudioCaptureSession
{
    private const int MicSampleRate = 16000;
    private const int SystemSampleRate = 48000;
    private const int SystemChannels = 2;
    private const int SampleBlockMilliseconds = 100;

    private readonly System.Threading.Timer? _timer;
    private readonly EncryptedAudioTrackWriter _micWriter;
    private readonly EncryptedAudioTrackWriter _systemWriter;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly object _writeGuard = new();
    private readonly string _microphoneDeviceId;
    private readonly string _systemDeviceId;
    private volatile float _micPeak;
    private volatile float _micRms;
    private volatile float _systemPeak;
    private volatile float _systemRms;
    private volatile bool _paused;
    private bool _stopped;
    private bool _disposed;

    public event Action<Exception>? Faulted;
    public string? MicrophoneDeviceName => $"Micrófono de desarrollo ({_microphoneDeviceId})";
    public string? SystemDeviceName => $"Salida de desarrollo ({_systemDeviceId})";
    public TimeSpan Elapsed => _clock.Elapsed;
    public bool IsPaused => _paused;
    public float MicrophonePeak => _micPeak;
    public float MicrophoneRms => _micRms;
    public float SystemPeak => _systemPeak;
    public float SystemRms => _systemRms;

    public FakeAudioCaptureSession(string outputDirectory, string? microphoneDeviceId, string? systemDeviceId)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("La carpeta de salida es obligatoria.", nameof(outputDirectory));
        _microphoneDeviceId = string.IsNullOrWhiteSpace(microphoneDeviceId) ? "dev-mic-a" : microphoneDeviceId;
        _systemDeviceId = string.IsNullOrWhiteSpace(systemDeviceId) ? "dev-out-a" : systemDeviceId;

        var layout = new SessionLayout(outputDirectory);
        _micWriter = EncryptedAudioTrack.CreateWriter(
            layout.AudioTrack(SessionLayout.MicrophoneTrackName),
            AudioTrackFormat.Pcm(MicSampleRate, channels: 1, bitsPerSample: 16));
        _systemWriter = EncryptedAudioTrack.CreateWriter(
            layout.AudioTrack(SessionLayout.SystemTrackName),
            AudioTrackFormat.IeeeFloat(SystemSampleRate, SystemChannels));
        _timer = new System.Threading.Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_stopped) throw new InvalidOperationException("La sesión de desarrollo ya finalizó.");
        _clock.Start();
        _timer?.Change(0, SampleBlockMilliseconds);
    }

    public void Pause() => _paused = true;

    public void Resume() => _paused = false;

    public void Stop()
    {
        if (_stopped) return;
        _stopped = true;
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        lock (_writeGuard)
        {
            _micWriter.Dispose();
            _systemWriter.Dispose();
        }
    }

    private void OnTick(object? state)
    {
        if (_paused || _stopped)
        {
            _micPeak = 0; _micRms = 0; _systemPeak = 0; _systemRms = 0;
            return;
        }
        try
        {
            var elapsed = _clock.Elapsed;
            var mic = FakeSignals.MicrophoneBlock(elapsed, SampleBlockMilliseconds);
            var system = FakeSignals.SystemBlock(elapsed, SampleBlockMilliseconds);
            _micPeak = mic.Peak;
            _micRms = mic.Rms;
            _systemPeak = system.Peak;
            _systemRms = system.Rms;

            lock (_writeGuard)
            {
                if (_stopped) return;
                _micWriter.Write(mic.Data, 0, mic.Data.Length);
                _systemWriter.Write(system.Data, 0, system.Data.Length);
            }
        }
        catch (Exception exception)
        {
            try { Faulted?.Invoke(exception); }
            catch { /* Los observadores no deben impedir la captura sintética. */ }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stopped = true;
        _timer?.Dispose();
        lock (_writeGuard)
        {
            try { _micWriter.Dispose(); } catch { /* ya cerrado */ }
            try { _systemWriter.Dispose(); } catch { /* ya cerrado */ }
        }
    }
}

/// <summary>Synthetic signal generators used only by the development head.</summary>
internal static class FakeSignals
{
    public readonly record struct Block(byte[] Data, float Peak, float Rms);

    public static Block MicrophoneBlock(TimeSpan elapsed, int blockMilliseconds)
    {
        const int MicSampleRate = 16000;
        var frames = (int)(0.001 * blockMilliseconds * MicSampleRate);
        var data = new byte[frames * 2];
        double squareSum = 0;
        float peak = 0;
        for (var frame = 0; frame < frames; frame++)
        {
            var t = elapsed.TotalSeconds + frame / (double)MicSampleRate;
            var value = MicrophoneSample(t);
            if (Math.Abs(value) > peak) peak = Math.Abs(value);
            squareSum += (double)value * value;
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(frame * 2), (short)Math.Clamp(value * 32767, short.MinValue, short.MaxValue));
        }
        return new Block(data, peak, frames == 0 ? 0 : (float)Math.Sqrt(squareSum / frames));
    }

    public static Block SystemBlock(TimeSpan elapsed, int blockMilliseconds)
    {
        const int SystemSampleRate = 48000;
        const int SystemChannels = 2;
        var frames = (int)(0.001 * blockMilliseconds * SystemSampleRate);
        var data = new byte[frames * SystemChannels * 4];
        double squareSum = 0;
        float peak = 0;
        for (var frame = 0; frame < frames; frame++)
        {
            var t = elapsed.TotalSeconds + frame / (double)SystemSampleRate;
            var value = SystemSample(t);
            if (Math.Abs(value) > peak) peak = Math.Abs(value);
            squareSum += (double)value * value;
            foreach (var channel in Enumerable.Range(0, SystemChannels))
                BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan((frame * SystemChannels + channel) * 4), value);
        }
        return new Block(data, peak, frames == 0 ? 0 : (float)Math.Sqrt(squareSum / frames));
    }

    private static float MicrophoneSample(double t)
    {
        const double periodSilent = 40.0;
        var cyclePosition = t % periodSilent;
        if (cyclePosition >= 35.0 && cyclePosition < 38.0) return 0f;

        var speechEnvelope = 0.45 + 0.4 * Math.Sin(2.2 * t) * (0.5 + 0.5 * Math.Sin(0.9 * t));
        var cycle = t % 17.0;
        var level = cycle >= 16.6
            ? 1.0
            : speechEnvelope * (0.4 + 0.6 * Math.Abs(Math.Sin(3.1 * t)));
        var tone = 0.6 * Math.Sin(2 * Math.PI * 210 * t) + 0.4 * Math.Sin(2 * Math.PI * 480 * t) + 0.07 * Math.Sin(2 * Math.PI * 47 * t);
        return (float)Math.Clamp(level * tone, -1.0, 1.0);
    }

    private static float SystemSample(double t)
    {
        const double spikePeriod = 23.0;
        var cycle = t % spikePeriod;
        var level = cycle >= 22.5
            ? 0.97
            : 0.3 + 0.2 * Math.Sin(1.1 * t) + 0.12 * Math.Sin(2.6 * t);
        var tone = Math.Sin(2 * Math.PI * 120 * t) * Math.Sin(2 * Math.PI * 0.4 * t)
            + 0.3 * Math.Sin(2 * Math.PI * 900 * t);
        return (float)Math.Clamp(0.9 * level * tone, -1.0, 1.0);
    }
}
