using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Transcriptor.Transcription;

/// <summary>
/// Contenedor local versionado con AES-256-GCM por bloques y DEK protegida
/// mediante Windows DPAPI en ámbito CurrentUser.
/// </summary>
public static class EncryptedFileContainer
{
    private static readonly byte[] Magic = "TRNCRYPT"u8.ToArray();
    private const int Version = 1;
    public const int BlockSize = 64 * 1024;
    internal const int KeySize = 32;
    internal const int NoncePrefixSize = 8;
    internal const int NonceSize = 12;
    internal const int TagSize = 16;
    private const int MaximumProtectedKeySize = 16 * 1024;
    private const uint CryptProtectUiForbidden = 0x1;

    public static EncryptedFileWriter CreateWriter(Stream destination) => new(destination);

    public static void Encrypt(Stream plaintext, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(destination);
        if (!plaintext.CanRead) throw new ArgumentException("El origen no permite lectura.", nameof(plaintext));
        if (!destination.CanWrite) throw new ArgumentException("El destino no permite escritura.", nameof(destination));
        EnsureWindows();

        var key = RandomNumberGenerator.GetBytes(KeySize);
        var prefix = RandomNumberGenerator.GetBytes(NoncePrefixSize);
        try
        {
            var protectedKey = ProtectKey(key);
            if (protectedKey.Length is 0 or > MaximumProtectedKeySize)
                throw new CryptographicException("DPAPI devolvió una clave protegida con tamaño no válido.");

            var header = BuildHeader(prefix, protectedKey);
            destination.Write(header);
            using var aes = new AesGcm(key, TagSize);
            var nonce = new byte[NonceSize];
            var tag = new byte[TagSize];
            var aad = new byte[header.Length];
            header.CopyTo(aad, 0);
            SetNonce(nonce, prefix, 0);
            aes.Encrypt(nonce, ReadOnlySpan<byte>.Empty, Span<byte>.Empty, tag, aad);
            destination.Write(tag);

            var buffer = new byte[BlockSize];
            var counter = 1u;
            while (true)
            {
                var count = ReadBlock(plaintext, buffer);
                if (count == 0) break;
                if (counter == uint.MaxValue)
                    throw new IOException("La sesión superó el límite de bloques del contenedor.");

                Span<byte> lengthBytes = stackalloc byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(lengthBytes, count);
                destination.Write(lengthBytes);

                var ciphertext = new byte[count];
                var blockTag = new byte[TagSize];
                var blockAad = BuildBlockAad(header, counter, count);
                SetNonce(nonce, prefix, counter);
                aes.Encrypt(nonce, buffer.AsSpan(0, count), ciphertext, blockTag, blockAad);
                destination.Write(ciphertext);
                destination.Write(blockTag);
                CryptographicOperations.ZeroMemory(ciphertext);
                counter++;
            }

            Span<byte> terminator = stackalloc byte[4];
            terminator.Clear();
            destination.Write(terminator);
            destination.Flush();
            CryptographicOperations.ZeroMemory(buffer);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(prefix);
        }
    }

    public static void Decrypt(Stream encrypted, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(encrypted);
        ArgumentNullException.ThrowIfNull(destination);
        if (!encrypted.CanRead) throw new ArgumentException("El origen no permite lectura.", nameof(encrypted));
        if (!destination.CanWrite) throw new ArgumentException("El destino no permite escritura.", nameof(destination));
        EnsureWindows();

        Span<byte> fixedHeader = stackalloc byte[28];
        encrypted.ReadExactly(fixedHeader);
        if (!fixedHeader[..8].SequenceEqual(Magic))
            throw new InvalidDataException("El archivo no es un contenedor Transcriptor reconocido.");

        var version = BinaryPrimitives.ReadInt32LittleEndian(fixedHeader[8..12]);
        var blockSize = BinaryPrimitives.ReadInt32LittleEndian(fixedHeader[12..16]);
        if (version != Version || blockSize != BlockSize)
            throw new InvalidDataException("La versión o el tamaño de bloque del contenedor no es compatible.");

        var prefix = fixedHeader[16..24].ToArray();
        var protectedKeyLength = BinaryPrimitives.ReadInt32LittleEndian(fixedHeader[24..28]);
        if (protectedKeyLength is <= 0 or > MaximumProtectedKeySize)
            throw new InvalidDataException("La longitud de la clave protegida no es válida.");

        var protectedKey = new byte[protectedKeyLength];
        encrypted.ReadExactly(protectedKey);
        var header = new byte[fixedHeader.Length + protectedKey.Length];
        fixedHeader.CopyTo(header);
        protectedKey.CopyTo(header, fixedHeader.Length);
        var headerTag = new byte[TagSize];
        encrypted.ReadExactly(headerTag);

        var key = UnprotectKey(protectedKey);
        try
        {
            if (key.Length != KeySize)
                throw new CryptographicException("DPAPI devolvió una clave de datos de tamaño no válido.");

            using var aes = new AesGcm(key, TagSize);
            var nonce = new byte[NonceSize];
            SetNonce(nonce, prefix, 0);
            aes.Decrypt(nonce, ReadOnlySpan<byte>.Empty, headerTag, Span<byte>.Empty, header);

            var counter = 1u;
            while (true)
            {
                Span<byte> lengthBytes = stackalloc byte[4];
                encrypted.ReadExactly(lengthBytes);
                var count = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
                if (count == 0) break;
                if (count is < 0 or > BlockSize || counter == uint.MaxValue)
                    throw new InvalidDataException("El bloque o su contador no son válidos.");

                var ciphertext = new byte[count];
                var plaintext = new byte[count];
                var tag = new byte[TagSize];
                encrypted.ReadExactly(ciphertext);
                encrypted.ReadExactly(tag);
                SetNonce(nonce, prefix, counter);
                var aad = BuildBlockAad(header, counter, count);
                aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
                destination.Write(plaintext);
                CryptographicOperations.ZeroMemory(ciphertext);
                CryptographicOperations.ZeroMemory(plaintext);
                counter++;
            }

            if (encrypted.ReadByte() != -1)
                throw new InvalidDataException("El contenedor contiene datos adicionales después del terminador.");
            destination.Flush();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(prefix);
            CryptographicOperations.ZeroMemory(protectedKey);
        }
    }

    internal static byte[] BuildHeader(byte[] prefix, byte[] protectedKey)
    {
        var header = new byte[28 + protectedKey.Length];
        Magic.CopyTo(header, 0);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8, 4), Version);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12, 4), BlockSize);
        prefix.CopyTo(header, 16);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24, 4), protectedKey.Length);
        protectedKey.CopyTo(header, 28);
        return header;
    }

    internal static byte[] BuildBlockAad(byte[] header, uint counter, int count)
    {
        var aad = new byte[header.Length + 8];
        header.CopyTo(aad, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(aad.AsSpan(header.Length, 4), counter);
        BinaryPrimitives.WriteInt32LittleEndian(aad.AsSpan(header.Length + 4, 4), count);
        return aad;
    }

    internal static void SetNonce(byte[] nonce, byte[] prefix, uint counter)
    {
        prefix.CopyTo(nonce, 0);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(NoncePrefixSize), counter);
    }

    private static int ReadBlock(Stream source, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = source.Read(buffer, total, buffer.Length - total);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    internal static byte[] ProtectKey(byte[] key)
    {
        var input = ToBlob(key);
        try
        {
            if (!CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    CryptProtectUiForbidden, out var output))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo proteger la clave con DPAPI.");
            return FromBlobAndFree(output);
        }
        finally { FreeInput(input); }
    }

    private static byte[] UnprotectKey(byte[] protectedKey)
    {
        var input = ToBlob(protectedKey);
        try
        {
            if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    CryptProtectUiForbidden, out var output))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo recuperar la clave DPAPI para este usuario de Windows.");
            return FromBlobAndFree(output);
        }
        finally { FreeInput(input); }
    }

    private static DataBlob ToBlob(byte[] value)
    {
        var pointer = Marshal.AllocHGlobal(value.Length);
        Marshal.Copy(value, 0, pointer, value.Length);
        return new DataBlob { Size = value.Length, Data = pointer };
    }

    private static byte[] FromBlobAndFree(DataBlob blob)
    {
        try
        {
            var value = new byte[blob.Size];
            if (blob.Size > 0) Marshal.Copy(blob.Data, value, 0, blob.Size);
            return value;
        }
        finally
        {
            if (blob.Data != IntPtr.Zero) LocalFree(blob.Data);
        }
    }

    private static void FreeInput(DataBlob blob)
    {
        if (blob.Data == IntPtr.Zero) return;
        var zeros = new byte[blob.Size];
        Marshal.Copy(zeros, 0, blob.Data, blob.Size);
        Marshal.FreeHGlobal(blob.Data);
    }

    internal static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("El cifrado de claves requiere Windows DPAPI.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Size;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref DataBlob input, string? description,
        IntPtr optionalEntropy, IntPtr reserved, IntPtr prompt, uint flags, out DataBlob output);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description,
        IntPtr optionalEntropy, IntPtr reserved, IntPtr prompt, uint flags, out DataBlob output);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}