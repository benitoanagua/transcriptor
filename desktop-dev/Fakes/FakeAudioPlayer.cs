using System.Buffers.Binary;
using Transcriptor.Transcription;

namespace Transcriptor.Dev;

// ⚠️ DATOS FICTICIOS — SOLO DESARROLLO ⚠️
// Reproductor de desarrollo: avanza con un temporizador sobre la posición,
// calculando la duración leyendo el encabezado RIFF del WAV (decodificado por la
// vía portable real). Sin salida de audio real: sirve para probar Play/Pausa/
// Detener/Seek/tiempo y el evento de fin.
public sealed class FakeAudioPlayer : IAudioPlayer
{
    private readonly TimeSpan _duration;
    private readonly System.Threading.Timer _timer;
    private readonly object _positionGuard = new();
    private TimeSpan _position;
    private bool _paused;
    private bool _stopping;

    public event Action? Ended;
    public event Action<Exception>? Failed;
    public TimeSpan Duration => _duration;
    public TimeSpan Position => _position;
    public double PlaybackRate => 1;

    public FakeAudioPlayer(string waveFilePath)
    {
        _duration = ReadRiffDuration(waveFilePath);
        _timer = new System.Threading.Timer(_ =>
        {
            if (_paused || _stopping) return;
            lock (_positionGuard)
            {
                _position += TimeSpan.FromMilliseconds(100);
                if (_position >= _duration && _duration > TimeSpan.Zero)
                {
                    _stopping = true;
                    try { Ended?.Invoke(); }
                    catch { /* Los observadores no deben impedir el fin. */ }
                }
            }
        }, null, 100, 100);
    }

    public void Pause() => _paused = true;

    public void Resume() => _paused = false;

    public void Seek(TimeSpan position)
    {
        lock (_positionGuard)
        {
            _position = TimeSpan.FromSeconds(Math.Clamp(position.TotalSeconds, 0, Math.Max(0, _duration.TotalSeconds)));
            if (_position < _duration) _stopping = false;
        }
    }

    public void Stop()
    {
        _stopping = true;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private static TimeSpan ReadRiffDuration(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length < 44) return TimeSpan.Zero;
            Span<byte> header = stackalloc byte[44];
            stream.ReadExactly(header);
            if (!header.Slice(0, 4).SequenceEqual("RIFF"u8)) return TimeSpan.Zero;
            var byteRate = BinaryPrimitives.ReadInt32LittleEndian(header[28..32]);
            var dataLength = BinaryPrimitives.ReadUInt32LittleEndian(header[40..44]);
            if (byteRate <= 0) return TimeSpan.Zero;
            return TimeSpan.FromSeconds((double)dataLength / byteRate);
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }

    public void Dispose() => _timer.Dispose();
}

/// <summary>Factory used only by the development head.</summary>
public sealed class FakeAudioPlayerFactory : IAudioPlayerFactory
{
    public IAudioPlayer Create(string waveFilePath) => new FakeAudioPlayer(waveFilePath);
}
