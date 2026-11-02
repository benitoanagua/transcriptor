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
        // mantiene el flujo y evita que cliente.wav quede más corto que yo.wav.
        _silence = new WasapiOut(sysDevice, AudioClientShareMode.Shared, false, 100);
        var silence = new SilenceProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));
        _silence.Init(new SampleToWaveProvider(silence));

        _mic.DataAvailable += (_, e) =>
        {
            _micLevel = Peak(e.Buffer, e.BytesRecorded, _mic.WaveFormat);
            _micWriter.Write(e.Buffer, 0, e.BytesRecorded);
            _micWriter.Flush();
        };
        _sys.DataAvailable += (_, e) =>
        {
            _sysLevel = Peak(e.Buffer, e.BytesRecorded, _sys.WaveFormat);
            _sysWriter.Write(e.Buffer, 0, e.BytesRecorded);
            _sysWriter.Flush();
        };
        _mic.RecordingStopped += (_, e) => Close(_micWriter, e.Exception, "micrófono");
        _sys.RecordingStopped += (_, e) => Close(_sysWriter, e.Exception, "audio del sistema");
    }

    public void Start()
    {
        _clock.Start();
        _silence.Play();
        _mic.StartRecording();
        _sys.StartRecording();
    }

    public void Stop()
    {
        _mic.StopRecording();
        _sys.StopRecording();
        _stopped.Wait(TimeSpan.FromSeconds(5));
        _silence.Stop();
        _clock.Stop();
    }

    private void Close(WaveFileWriter writer, Exception? error, string source)
    {
        if (error is not null) Console.Error.WriteLine($"\nError en {source}: {error.Message}");
        writer.Dispose();
        _stopped.Signal();
    }

    private static float Peak(byte[] buf, int count, WaveFormat format)
    {
        float max = 0;
        var encoding = format.ToStandardWaveFormat().Encoding;
        if (encoding == WaveFormatEncoding.IeeeFloat)
        {
            for (int i = 0; i + 4 <= count; i += 4)
                max = Math.Max(max, Math.Abs(BitConverter.ToSingle(buf, i)));
        }
        else if (format.BitsPerSample == 16)
        {
            for (int i = 0; i + 2 <= count; i += 2)
                max = Math.Max(max, Math.Abs(BitConverter.ToInt16(buf, i) / 32768f));
        }
        return Math.Min(max, 1f);
    }

    public void Dispose()
    {
        _mic.Dispose();
        _sys.Dispose();
        _silence.Dispose();
        _stopped.Dispose();
    }
}
