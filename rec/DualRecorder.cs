using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Transcriptor.Transcription;

namespace Transcriptor.Rec;

/// <summary>Graba micrófono y audio del sistema directamente en pistas cifradas.</summary>
internal sealed class DualRecorder : IDisposable
{
    private static readonly Guid PcmSubFormat = new("00000001-0000-0010-8000-00AA00389B71");
    private static readonly Guid FloatSubFormat = new("00000003-0000-0010-8000-00AA00389B71");

    private readonly WasapiCapture _mic;
    private readonly WasapiLoopbackCapture _sys;
    private readonly WasapiOut _silence;
    private readonly EncryptedAudioTrackWriter _micWriter;
    private readonly EncryptedAudioTrackWriter _sysWriter;
    private readonly CountdownEvent _stopped = new(2);
    private readonly Stopwatch _clock = new();
    private volatile float _micLevel;
    private volatile float _sysLevel;
    private Exception? _recordingError;
    private int _stopRequested;
    private int _started;
    private int _micStarted;
    private int _sysStarted;
    private int _silenceStarted;
    private int _micClosed;
    private int _sysClosed;

    public TimeSpan Elapsed => _clock.Elapsed;
    public float MicLevel => _micLevel;
    public float SysLevel => _sysLevel;

    public DualRecorder(MMDevice micDevice, MMDevice sysDevice, string dir)
    {
        Directory.CreateDirectory(dir);
        var microphone = new WasapiCapture(micDevice);
        WasapiLoopbackCapture? system = null;
        EncryptedAudioTrackWriter? microphoneWriter = null;
        EncryptedAudioTrackWriter? systemWriter = null;
        WasapiOut? silenceOutput = null;

        try
        {
            system = new WasapiLoopbackCapture(sysDevice);
            microphoneWriter = EncryptedAudioTrack.CreateWriter(Path.Combine(dir, "yo.trn"), microphone.WaveFormat);
            systemWriter = EncryptedAudioTrack.CreateWriter(Path.Combine(dir, "cliente.trn"), system.WaveFormat);

            silenceOutput = new WasapiOut(sysDevice, AudioClientShareMode.Shared, false, 100);
            var silence = new SilenceProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));
            silenceOutput.Init(silence);

            _mic = microphone;
            _sys = system!;
            _micWriter = microphoneWriter!;
            _sysWriter = systemWriter!;
            _silence = silenceOutput!;

            _mic.DataAvailable += (_, e) => WriteBlock(_micWriter, e.Buffer, e.BytesRecorded, _mic.WaveFormat, true);
            _sys.DataAvailable += (_, e) => WriteBlock(_sysWriter, e.Buffer, e.BytesRecorded, _sys.WaveFormat, false);
            _mic.RecordingStopped += (_, e) => Close(_micWriter, e.Exception, microphone: true);
            _sys.RecordingStopped += (_, e) => Close(_sysWriter, e.Exception, microphone: false);
        }
        catch
        {
            TryDispose(silenceOutput);
            TryDispose(systemWriter);
            TryDispose(microphoneWriter);
            TryDispose(system);
            microphone.Dispose();
            throw;
        }
    }

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("La grabación ya fue iniciada.");

        _clock.Start();
        try
        {
            _silence.Play();
            Volatile.Write(ref _silenceStarted, 1);
            _mic.StartRecording();
            Volatile.Write(ref _micStarted, 1);
            _sys.StartRecording();
            Volatile.Write(ref _sysStarted, 1);
        }
        catch
        {
            // Start puede fallar después de haber iniciado solo uno de los
            // dispositivos. Cierra ambos escritores y detiene lo que sí arrancó.
            try { Stop(); }
            catch { /* Se conserva la excepción original del inicio. */ }
            throw;
        }
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) != 0)
            return;

        Exception? stopError = null;
        if (Volatile.Read(ref _micStarted) != 0)
        {
            try { _mic.StopRecording(); }
            catch (Exception exception)
            {
                stopError = exception;
                Close(_micWriter, exception, microphone: true);
            }
        }
        else
        {
            Close(_micWriter, null, microphone: true);
        }

        if (Volatile.Read(ref _sysStarted) != 0)
        {
            try { _sys.StopRecording(); }
            catch (Exception exception)
            {
                stopError ??= exception;
                Close(_sysWriter, exception, microphone: false);
            }
        }
        else
        {
            Close(_sysWriter, null, microphone: false);
        }

        var completed = _stopped.Wait(TimeSpan.FromSeconds(15));
        try
        {
            if (Volatile.Read(ref _silenceStarted) != 0)
                _silence.Stop();
        }
        finally { _clock.Stop(); }

        if (!completed)
            throw new TimeoutException("Las pistas de audio no finalizaron dentro de 15 segundos; no se puede garantizar que las pistas cifradas estén completas.");

        if (_recordingError is not null)
            throw new IOException("Falló la escritura de una de las pistas de audio.", _recordingError);

        if (stopError is not null)
            throw new IOException("No se pudieron detener correctamente los dispositivos de audio.", stopError);
    }

    private void WriteBlock(EncryptedAudioTrackWriter writer, byte[] buffer, int count, WaveFormat format, bool microphone)
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

    private void Close(EncryptedAudioTrackWriter writer, Exception? error, bool microphone)
    {
        ref int closed = ref (microphone ? ref _micClosed : ref _sysClosed);
        if (Interlocked.Exchange(ref closed, 1) != 0)
            return;

        if (error is not null)
            Interlocked.CompareExchange(ref _recordingError, error, null);

        try { writer.Dispose(); }
        catch (Exception exception) { Interlocked.CompareExchange(ref _recordingError, exception, null); }
        finally { _stopped.Signal(); }
    }

    private static float Peak(byte[] buffer, int count, WaveFormat format)
    {
        var maximum = 0f;
        var isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat
            || (format is WaveFormatExtensible floatExtensible && floatExtensible.SubFormat == FloatSubFormat);
        var isPcm = format.Encoding == WaveFormatEncoding.Pcm
            || (format is WaveFormatExtensible pcmExtensible && pcmExtensible.SubFormat == PcmSubFormat);

        if (isFloat && format.BitsPerSample == 32)
        {
            for (var index = 0; index + 4 <= count; index += 4)
            {
                var sample = BitConverter.ToSingle(buffer, index);
                if (float.IsFinite(sample)) maximum = Math.Max(maximum, Math.Abs(sample));
            }
        }
        else if (isPcm)
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

    private static void TryDispose(IDisposable? disposable)
    {
        try { disposable?.Dispose(); }
        catch { /* La limpieza de recursos parciales no debe ocultar el error original. */ }
    }

    public void Dispose()
    {
        if (Volatile.Read(ref _started) != 0 && !_stopped.IsSet && Volatile.Read(ref _stopRequested) == 0)
        {
            try { Stop(); }
            catch { /* Preserve the original failure; disposal must still release native handles. */ }
        }
        else if (Volatile.Read(ref _started) == 0)
        {
            Close(_micWriter, null, microphone: true);
            Close(_sysWriter, null, microphone: false);
        }

        _mic.Dispose();
        _sys.Dispose();
        _silence.Dispose();
        if (_stopped.IsSet)
            _stopped.Dispose();
    }
}
