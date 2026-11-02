using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Transcriptor.Rec;

/// <summary>Graba el micrófono (yo.wav) y el audio del sistema (cliente.wav) en paralelo.</summary>
internal sealed class DualRecorder : IDisposable
{
    private readonly WasapiCapture _mic;
    private readonly WasapiLoopbackCapture _sys;
    private readonly WasapiOut _silence;
    private readonly WaveFileWriter _micWriter;
    private readonly WaveFileWriter _sysWriter;
    private readonly CountdownEvent _stopped = new(2);
    private readonly Stopwatch _clock = new();
    private volatile float _micLevel;
    private volatile float _sysLevel;
    private Exception? _recordingError;
    private int _stopRequested;
    private int _started;

    public TimeSpan Elapsed => _clock.Elapsed;
    public float MicLevel => _micLevel;
    public float SysLevel => _sysLevel;

    public DualRecorder(MMDevice micDevice, MMDevice sysDevice, string dir)
    {
        Directory.CreateDirectory(dir);
        _mic = new WasapiCapture(micDevice);
        _sys = new WasapiLoopbackCapture(sysDevice);
        _micWriter = new WaveFileWriter(Path.Combine(dir, "yo.wav"), _mic.WaveFormat);
        _sysWriter = new WaveFileWriter(Path.Combine(dir, "cliente.wav"), _sys.WaveFormat);

        // El loopback no entrega muestras mientras nada suena. Una salida en silencio
        // mantiene activo el flujo de captura.
        _silence = new WasapiOut(sysDevice, AudioClientShareMode.Shared, false, 100);
        var silence = new SilenceProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));
        _silence.Init(new SampleToWaveProvider(silence));

        _mic.DataAvailable += (_, e) => WriteBlock(_micWriter, e.Buffer, e.BytesRecorded, _mic.WaveFormat, true);
        _sys.DataAvailable += (_, e) => WriteBlock(_sysWriter, e.Buffer, e.BytesRecorded, _sys.WaveFormat, false);
        _mic.RecordingStopped += (_, e) => Close(_micWriter, e.Exception);
        _sys.RecordingStopped += (_, e) => Close(_sysWriter, e.Exception);
    }

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("La grabación ya fue iniciada.");

        _clock.Start();
        _silence.Play();
        _mic.StartRecording();
        _sys.StartRecording();
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) != 0)
            return;

        Exception? stopError = null;
        try { _mic.StopRecording(); }
        catch (Exception exception) { stopError = exception; }

        try { _sys.StopRecording(); }
        catch (Exception exception) { stopError ??= exception; }

        var completed = _stopped.Wait(TimeSpan.FromSeconds(15));
        try { _silence.Stop(); }
        finally { _clock.Stop(); }

        if (!completed)
            throw new TimeoutException("Las pistas de audio no finalizaron dentro de 15 segundos; no se puede garantizar que los WAV estén completos.");

        if (_recordingError is not null)
            throw new IOException("Falló la escritura de una de las pistas de audio.", _recordingError);

        if (stopError is not null)
            throw new IOException("No se pudieron detener correctamente los dispositivos de audio.", stopError);
    }

    private void WriteBlock(WaveFileWriter writer, byte[] buffer, int count, WaveFormat format, bool microphone)
    {
        try
        {
            var peak = Peak(buffer, count, format);
            writer.Write(buffer, 0, count);
            if (microphone) _micLevel = peak;
            else _sysLevel = peak;
        }
        catch (Exception exception)
        {
            Interlocked.CompareExchange(ref _recordingError, exception, null);
        }
    }

    private void Close(WaveFileWriter writer, Exception? error)
    {
        if (error is not null)
            Interlocked.CompareExchange(ref _recordingError, error, null);

        try { writer.Dispose(); }
        catch (Exception exception) { Interlocked.CompareExchange(ref _recordingError, exception, null); }
        finally { _stopped.Signal(); }
    }

    private static float Peak(byte[] buffer, int count, WaveFormat format)
    {
        var maximum = 0f;
        var encoding = format.ToStandardWaveFormat().Encoding;

        if (encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            for (var index = 0; index + 4 <= count; index += 4)
            {
                var sample = BitConverter.ToSingle(buffer, index);
                if (float.IsFinite(sample)) maximum = Math.Max(maximum, Math.Abs(sample));
            }
        }
        else if (encoding == WaveFormatEncoding.Pcm)
        {
            switch (format.BitsPerSample)
            {
                case 8:
                    for (var index = 0; index < count; index++)
                        maximum = Math.Max(maximum, Math.Abs((buffer[index] - 128) / 128f));
                    break;
                case 16:
                    for (var index = 0; index + 2 <= count; index += 2)
                        maximum = Math.Max(maximum, Math.Abs(BitConverter.ToInt16(buffer, index) / 32768f));
                    break;
                case 24:
                    for (var index = 0; index + 3 <= count; index += 3)
                    {
                        var sample = buffer[index] | (buffer[index + 1] << 8) | (buffer[index + 2] << 16);
                        if ((sample & 0x800000) != 0) sample |= unchecked((int)0xFF000000);
                        maximum = Math.Max(maximum, Math.Abs(sample / 8388608f));
                    }
                    break;
                case 32:
                    for (var index = 0; index + 4 <= count; index += 4)
                        maximum = Math.Max(maximum, Math.Abs(BitConverter.ToInt32(buffer, index) / 2147483648f));
                    break;
            }
        }

        return Math.Min(maximum, 1f);
    }

    public void Dispose()
    {
        if (Volatile.Read(ref _started) != 0 && !_stopped.IsSet && Volatile.Read(ref _stopRequested) == 0)
        {
            try { Stop(); }
            catch { /* Preserve the original failure; disposal must still release native handles. */ }
        }

        _mic.Dispose();
        _sys.Dispose();
        _silence.Dispose();
        if (_stopped.IsSet)
            _stopped.Dispose();
    }
}
