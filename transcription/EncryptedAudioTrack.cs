using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Transcriptor.Transcription;

/// <summary>
/// Pista de audio cifrada: conserva la descripción PCM/IEEE-float y las muestras
/// dentro del contenedor autenticado. No escribe WAV en claro durante la captura.
/// Es portable: solo requiere un ISecretProtector para los bloques cifrados.
/// </summary>
public static class EncryptedAudioTrack
{
    private static ReadOnlySpan<byte> Magic => "TRNAUD01"u8;
    private const int TrackHeaderSize = 24;
    private static readonly byte[] RiffMagicBytes = "RIFF"u8.ToArray();
    private static readonly byte[] WaveMagicBytes = "WAVE"u8.ToArray();
    private static readonly byte[] FmtMagicBytes = "fmt "u8.ToArray();
    private static readonly byte[] DataMagicBytes = "data"u8.ToArray();

    public static EncryptedAudioTrackWriter CreateWriter(string path, AudioTrackFormat format) =>
        new(path, format);

    /// <summary>Reconstruye un WAV temporal autenticado en la carpeta protegida de la sesión.</summary>
    public static string DecryptToWaveFile(string encryptedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedPath);
        var source = Path.GetFullPath(encryptedPath);
        var directory = Path.GetDirectoryName(source)
            ?? throw new ArgumentException("La pista debe tener una carpeta.", nameof(encryptedPath));
        var rawPath = Path.Combine(directory, $".{Guid.NewGuid():N}.audio.tmp");
        var wavePath = Path.Combine(directory, $".{Guid.NewGuid():N}.playback.wav");
        try
        {
            using (var input = File.OpenRead(source))
            using (var raw = new FileStream(rawPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.WriteThrough))
            {
                EncryptedFileContainer.Decrypt(input, raw);
                raw.Flush(flushToDisk: true);
            }

            using (var raw = File.OpenRead(rawPath))
            {
                var format = ReadValidatedFormat(raw, raw.Length);
                WriteRiffWave(raw, wavePath, format);
            }

            File.Delete(rawPath);
            return wavePath;
        }
        catch
        {
            TryDelete(rawPath);
            TryDelete(wavePath);
            throw;
        }
    }

    /// <summary>
    /// Validates the decrypted track exactly as shipped before the portable split:
    /// magic, coherent PCM/IEEE-float description, and length aligned to blocks.
    /// </summary>
    internal static AudioTrackFormat ReadValidatedFormat(Stream raw, long rawLength)
    {
        Span<byte> header = stackalloc byte[TrackHeaderSize];
        raw.ReadExactly(header);
        if (!header[..8].SequenceEqual(Magic))
            throw new InvalidDataException("La pista cifrada no contiene una cabecera de audio reconocida.");

        var encoding = BinaryPrimitives.ReadUInt16LittleEndian(header[8..10]);
        var channels = BinaryPrimitives.ReadUInt16LittleEndian(header[10..12]);
        var sampleRate = BinaryPrimitives.ReadInt32LittleEndian(header[12..16]);
        var averageBytes = BinaryPrimitives.ReadInt32LittleEndian(header[16..20]);
        var blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(header[20..22]);
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(header[22..24]);
        var minimumBlockAlign = channels * ((bits + 7) / 8);
        var validPcmDepth = bits is 8 or 16 or 24 or 32;
        var validEncoding = encoding == AudioTrackFormat.PcmEncoding
            ? validPcmDepth
            : encoding == AudioTrackFormat.IeeeFloatEncoding && bits == 32;
        if (channels == 0 || sampleRate <= 0 || averageBytes <= 0 || blockAlign == 0 || bits == 0
            || !validEncoding || blockAlign < minimumBlockAlign
            || (long)averageBytes != (long)sampleRate * blockAlign
            || (rawLength - TrackHeaderSize) % blockAlign != 0)
            throw new InvalidDataException("Los parámetros o la longitud de audio de la pista cifrada no son compatibles.");

        return new AudioTrackFormat(encoding, channels, sampleRate, averageBytes, blockAlign, bits);
    }

    /// <summary>
    /// Escribe un WAV estándar (44 bytes de cabecera RIFF + muestras) a partir del
    /// contenido autenticado. Sustituye a WaveFileWriter para eliminar la dependencia
    /// de NAudio; las muestras se copian sin transformación.
    /// </summary>
    private static void WriteRiffWave(Stream raw, string wavePath, AudioTrackFormat format)
    {
        var dataLength = raw.Length - TrackHeaderSize;
        raw.Position = TrackHeaderSize;
        using var wave = new FileStream(wavePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024);
        var preamble = new byte[44];
        RiffMagicBytes.CopyTo(preamble, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(preamble.AsSpan(4), checked((uint)(dataLength + 36)));
        WaveMagicBytes.CopyTo(preamble, 8);
        FmtMagicBytes.CopyTo(preamble, 12);
        BinaryPrimitives.WriteInt32LittleEndian(preamble.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(preamble.AsSpan(20), format.Encoding);
        BinaryPrimitives.WriteUInt16LittleEndian(preamble.AsSpan(22), format.Channels);
        BinaryPrimitives.WriteInt32LittleEndian(preamble.AsSpan(24), format.SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(preamble.AsSpan(28), format.AverageBytesPerSecond);
        BinaryPrimitives.WriteUInt16LittleEndian(preamble.AsSpan(32), format.BlockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(preamble.AsSpan(34), format.BitsPerSample);
        DataMagicBytes.CopyTo(preamble, 36);
        BinaryPrimitives.WriteUInt32LittleEndian(preamble.AsSpan(40), checked((uint)dataLength));
        wave.Write(preamble);
        raw.CopyTo(wave);
        wave.Flush(flushToDisk: true);
    }

    internal static byte[] BuildHeader(AudioTrackFormat format)
    {
        var validPcmDepth = format.BitsPerSample is 8 or 16 or 24 or 32;
        var validEncoding = format.Encoding == AudioTrackFormat.PcmEncoding
            ? validPcmDepth
            : format.Encoding == AudioTrackFormat.IeeeFloatEncoding && format.BitsPerSample == 32;
        var minimumBlockAlign = (long)format.Channels * ((format.BitsPerSample + 7) / 8);
        if (format.Channels is <= 0 or > ushort.MaxValue || format.SampleRate <= 0 || format.AverageBytesPerSecond <= 0
            || format.BlockAlign is <= 0 or > ushort.MaxValue || format.BitsPerSample is <= 0 or > ushort.MaxValue
            || !validEncoding || format.BlockAlign < minimumBlockAlign
            || (long)format.AverageBytesPerSecond != (long)format.SampleRate * format.BlockAlign)
            throw new NotSupportedException("La descripción del formato PCM/IEEE-float no es coherente o no está admitida.");

        var header = new byte[TrackHeaderSize];
        Magic.CopyTo(header);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(8, 2), format.Encoding);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(10, 2), format.Channels);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12, 4), format.SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16, 4), format.AverageBytesPerSecond);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(20, 2), format.BlockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(22, 2), format.BitsPerSample);
        return header;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* El borrado normal es best effort, nunca se presenta como seguro. */ }
    }
}

public sealed class EncryptedAudioTrackWriter : IDisposable
{
    private readonly FileStream _destination;
    private readonly EncryptedFileWriter _writer;
    private bool _disposed;

    internal EncryptedAudioTrackWriter(string path, AudioTrackFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(format);
        var header = EncryptedAudioTrack.BuildHeader(format);
        _destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.WriteThrough);
        EncryptedFileWriter? writer = null;
        try
        {
            writer = EncryptedFileContainer.CreateWriter(_destination);
            writer.Write(header);
            _writer = writer;
        }
        catch
        {
            try { writer?.Dispose(); } catch { }
            _destination.Dispose();
            try { File.Delete(path); } catch { }
            throw;
        }
        finally { CryptographicOperations.ZeroMemory(header); }
    }

    public void Write(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _writer.Write(buffer, offset, count);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _writer.Dispose(); }
        finally { _destination.Dispose(); }
    }
}
