using System.Buffers.Binary;
using System.Security.Cryptography;
using NAudio.Wave;

namespace Transcriptor.Transcription;

/// <summary>
/// Pista de audio cifrada: conserva la descripción PCM/IEEE-float y las muestras
/// dentro del contenedor autenticado. No escribe WAV en claro durante la captura.
/// </summary>
public static class EncryptedAudioTrack
{
    private static ReadOnlySpan<byte> Magic => "TRNAUD01"u8;
    private const int HeaderSize = 24;
    private static readonly Guid PcmSubFormat = new("00000001-0000-0010-8000-00AA00389B71");
    private static readonly Guid FloatSubFormat = new("00000003-0000-0010-8000-00AA00389B71");

    public static EncryptedAudioTrackWriter CreateWriter(string path, WaveFormat format) =>
        new(path, format);

    /// <summary>Reconstruye un WAV temporal autenticado en la carpeta protegida de la sesión.</summary>
    public static string DecryptToWaveFile(string encryptedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedPath);
        EncryptedFileContainer.EnsureWindows();
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
                Span<byte> header = stackalloc byte[HeaderSize];
                raw.ReadExactly(header);
                if (!header[..8].SequenceEqual(Magic))
                    throw new InvalidDataException("La pista cifrada no contiene una cabecera de audio reconocida.");

                var encoding = (WaveFormatEncoding)BinaryPrimitives.ReadUInt16LittleEndian(header[8..10]);
                var channels = BinaryPrimitives.ReadUInt16LittleEndian(header[10..12]);
                var sampleRate = BinaryPrimitives.ReadInt32LittleEndian(header[12..16]);
                var averageBytes = BinaryPrimitives.ReadInt32LittleEndian(header[16..20]);
                var blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(header[20..22]);
                var bits = BinaryPrimitives.ReadUInt16LittleEndian(header[22..24]);
                var minimumBlockAlign = channels * ((bits + 7) / 8);
                var validPcmDepth = bits is 8 or 16 or 24 or 32;
                var validEncoding = encoding == WaveFormatEncoding.Pcm
                    ? validPcmDepth
                    : encoding == WaveFormatEncoding.IeeeFloat && bits == 32;
                if (channels == 0 || sampleRate <= 0 || averageBytes <= 0 || blockAlign == 0 || bits == 0
                    || !validEncoding || blockAlign < minimumBlockAlign
                    || (long)averageBytes != (long)sampleRate * blockAlign
                    || (raw.Length - HeaderSize) % blockAlign != 0)
                    throw new InvalidDataException("Los parámetros o la longitud de audio de la pista cifrada no son compatibles.");

                var format = WaveFormat.CreateCustomFormat(encoding, sampleRate, channels, averageBytes, blockAlign, bits);
                using var wave = new WaveFileWriter(wavePath, format);
                raw.CopyTo(wave);
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

    internal static byte[] BuildHeader(WaveFormat format)
    {
        var encoding = format.Encoding;
        if (encoding == WaveFormatEncoding.Extensible && format is WaveFormatExtensible extensible)
        {
            if (extensible.SubFormat == PcmSubFormat) encoding = WaveFormatEncoding.Pcm;
            else if (extensible.SubFormat == FloatSubFormat) encoding = WaveFormatEncoding.IeeeFloat;
        }
        if (encoding is not (WaveFormatEncoding.Pcm or WaveFormatEncoding.IeeeFloat))
            throw new NotSupportedException($"Formato de audio no compatible con la captura cifrada: {format.Encoding}.");

        var header = new byte[HeaderSize];
        Magic.CopyTo(header);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(8, 2), (ushort)encoding);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(10, 2), checked((ushort)format.Channels));
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12, 4), format.SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16, 4), format.AverageBytesPerSecond);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(20, 2), checked((ushort)format.BlockAlign));
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(22, 2), checked((ushort)format.BitsPerSample));
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

    internal EncryptedAudioTrackWriter(string path, WaveFormat format)
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
