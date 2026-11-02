using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Transcriptor.Transcription;

/// <summary>Escritor secuencial que cifra cada bloque recibido sin crear WAV intermedio.</summary>
public sealed class EncryptedFileWriter : IDisposable
{
    private readonly Stream _destination;
    private readonly byte[] _key;
    private readonly byte[] _prefix;
    private readonly byte[] _header;
    private readonly AesGcm _aes;
    private readonly byte[] _nonce = new byte[EncryptedFileContainer.NonceSize];
    private readonly byte[] _pending = new byte[EncryptedFileContainer.BlockSize];
    private int _pendingCount;
    private uint _counter = 1;
    private bool _disposed;
    private bool _faulted;

    public EncryptedFileWriter(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite) throw new ArgumentException("El destino no permite escritura.", nameof(destination));
        EncryptedFileContainer.EnsureWindows();
        _destination = destination;
        _key = RandomNumberGenerator.GetBytes(EncryptedFileContainer.KeySize);
        _prefix = RandomNumberGenerator.GetBytes(EncryptedFileContainer.NoncePrefixSize);
        try
        {
            var protectedKey = EncryptedFileContainer.ProtectKey(_key);
            try
            {
                _header = EncryptedFileContainer.BuildHeader(_prefix, protectedKey);
                _destination.Write(_header);
                _aes = new AesGcm(_key, EncryptedFileContainer.TagSize);
                var tag = new byte[EncryptedFileContainer.TagSize];
                EncryptedFileContainer.SetNonce(_nonce, _prefix, 0);
                _aes.Encrypt(_nonce, ReadOnlySpan<byte>.Empty, Span<byte>.Empty, tag, _header);
                _destination.Write(tag);
            }
            finally { CryptographicOperations.ZeroMemory(protectedKey); }
        }
        catch
        {
            ClearSecrets();
            throw;
        }
    }

    public void Write(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || count < 0 || offset > buffer.Length - count)
            throw new ArgumentOutOfRangeException(nameof(offset));
        Write(buffer.AsSpan(offset, count));
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_faulted) throw new IOException("El escritor cifrado ya falló y no admite más datos.");
        try
        {
            while (!data.IsEmpty)
            {
                var take = Math.Min(EncryptedFileContainer.BlockSize - _pendingCount, data.Length);
                data[..take].CopyTo(_pending.AsSpan(_pendingCount));
                _pendingCount += take;
                data = data[take..];
                if (_pendingCount == EncryptedFileContainer.BlockSize) WritePendingBlock();
            }
        }
        catch
        {
            _faulted = true;
            throw;
        }
    }

    private void WritePendingBlock()
    {
        if (_counter == uint.MaxValue) throw new IOException("La sesión superó el límite de bloques del contenedor.");
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, _pendingCount);
        _destination.Write(length);
        var ciphertext = new byte[_pendingCount];
        var tag = new byte[EncryptedFileContainer.TagSize];
        var aad = EncryptedFileContainer.BuildBlockAad(_header, _counter, _pendingCount);
        EncryptedFileContainer.SetNonce(_nonce, _prefix, _counter);
        _aes.Encrypt(_nonce, _pending.AsSpan(0, _pendingCount), ciphertext, tag, aad);
        _destination.Write(ciphertext);
        _destination.Write(tag);
        CryptographicOperations.ZeroMemory(ciphertext);
        CryptographicOperations.ZeroMemory(_pending);
        _pendingCount = 0;
        _counter++;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (!_faulted)
            {
                if (_pendingCount > 0) WritePendingBlock();
                Span<byte> terminator = stackalloc byte[4];
                terminator.Clear();
                _destination.Write(terminator);
                _destination.Flush();
            }
        }
        finally
        {
            _aes.Dispose();
            ClearSecrets();
        }
    }

    private void ClearSecrets()
    {
        CryptographicOperations.ZeroMemory(_key);
        CryptographicOperations.ZeroMemory(_prefix);
        CryptographicOperations.ZeroMemory(_pending);
        CryptographicOperations.ZeroMemory(_nonce);
    }
}
