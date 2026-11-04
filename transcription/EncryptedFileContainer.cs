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
    private const int LegacyVersion = 1;
    internal const int CurrentVersion = 2;
    internal static readonly byte[] FooterMagic = "TRNEND02"u8.ToArray();
    public const int BlockSize = 64 * 1024;
    internal const int KeySize = 32;
    internal const int NoncePrefixSize = 8;
    internal const int NonceSize = 12;
    internal const int TagSize = 16;
    private const int MaximumProtectedKeySize = 16 * 1024;
    private const uint CryptProtectUiForbidden = 0x1;

    public static EncryptedFileWriter CreateWriter(Stream destination, ISecretProtector? secretProtector = null) => new(destination, secretProtector);

    public static void Encrypt(Stream plaintext, Stream destination, ISecretProtector? secretProtector = null)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(destination);
        if (!plaintext.CanRead) throw new ArgumentException("El origen no permite lectura.", nameof(plaintext));
        if (!destination.CanWrite) throw new ArgumentException("El destino no permite escritura.", nameof(destination));
        EnsureWindows();

        var buffer = new byte[BlockSize];
        try
        {
            using var writer = CreateWriter(destination, secretProtector);
            try
            {
                int read;
                while ((read = plaintext.Read(buffer, 0, buffer.Length)) > 0)
                    writer.Write(buffer, 0, read);
            }
            catch
            {
                // Una lectura incompleta no debe producir un contenedor válido
                // que parezca representar el archivo de origen completo.
                writer.Abort();
                throw;
            }
        }
        finally { CryptographicOperations.ZeroMemory(buffer); }
    }

    public static void Decrypt(Stream encrypted, Stream destination, ISecretProtector? secretProtector = null)
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
        if (version is not (LegacyVersion or CurrentVersion) || blockSize != BlockSize)
            throw new InvalidDataException("La versión o el tamaño de bloque del contenedor no es compatible.");

        var prefix = fixedHeader[16..24].ToArray();
        byte[]? protectedKey = null;
        byte[]? header = null;
        byte[]? headerTag = null;
        byte[]? key = null;
        try
        {
            var protectedKeyLength = BinaryPrimitives.ReadInt32LittleEndian(fixedHeader[24..28]);
            if (protectedKeyLength is <= 0 or > MaximumProtectedKeySize)
                throw new InvalidDataException("La longitud de la clave protegida no es válida.");

            protectedKey = new byte[protectedKeyLength];
            encrypted.ReadExactly(protectedKey);
            header = new byte[fixedHeader.Length + protectedKey.Length];
            fixedHeader.CopyTo(header);
            protectedKey.CopyTo(header, fixedHeader.Length);
            headerTag = new byte[TagSize];
            encrypted.ReadExactly(headerTag);

            key = (secretProtector ?? DpapiSecretProtector.Instance).Unprotect(protectedKey);
            if (key.Length != KeySize)
                throw new CryptographicException("DPAPI devolvió una clave de datos de tamaño no válido.");

            using var aes = new AesGcm(key, TagSize);
            var nonce = new byte[NonceSize];
            try
            {
                SetNonce(nonce, prefix, 0);
                aes.Decrypt(nonce, ReadOnlySpan<byte>.Empty, headerTag, Span<byte>.Empty, header);

                var counter = 1u;
                ulong totalLength = 0;
                Span<byte> lengthBytes = stackalloc byte[4];
                while (true)
                {
                    encrypted.ReadExactly(lengthBytes);
                    var count = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
                    if (count == 0) break;
                    if (count is < 0 or > BlockSize || counter == uint.MaxValue)
                        throw new InvalidDataException("El bloque o su contador no son válidos.");

                    var ciphertext = new byte[count];
                    var plaintextBlock = new byte[count];
                    var tag = new byte[TagSize];
                    var aad = BuildBlockAad(header, counter, count);
                    try
                    {
                        encrypted.ReadExactly(ciphertext);
                        encrypted.ReadExactly(tag);
                        SetNonce(nonce, prefix, counter);
                        aes.Decrypt(nonce, ciphertext, tag, plaintextBlock, aad);
                        destination.Write(plaintextBlock);
                        totalLength = checked(totalLength + (uint)count);
                        counter++;
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(ciphertext);
                        CryptographicOperations.ZeroMemory(plaintextBlock);
                        CryptographicOperations.ZeroMemory(tag);
                        CryptographicOperations.ZeroMemory(aad);
                    }
                }

                if (version == LegacyVersion)
                {
                    if (encrypted.ReadByte() != -1)
                        throw new InvalidDataException("El contenedor contiene datos adicionales después del terminador.");
                }
                else
                {
                    Span<byte> footer = stackalloc byte[20];
                    Span<byte> footerTag = stackalloc byte[TagSize];
                    encrypted.ReadExactly(footer);
                    encrypted.ReadExactly(footerTag);
                    if (!footer[..FooterMagic.Length].SequenceEqual(FooterMagic))
                        throw new InvalidDataException("El cierre autenticado del contenedor no es válido.");

                    var recordedBlocks = BinaryPrimitives.ReadUInt32LittleEndian(footer[8..12]);
                    var recordedLength = BinaryPrimitives.ReadUInt64LittleEndian(footer[12..20]);
                    if (recordedBlocks != counter - 1 || recordedLength != totalLength)
                        throw new InvalidDataException("El recuento de bloques o la longitud total no coinciden con el cierre autenticado.");

                    var footerAad = BuildFooterAad(header, footer);
                    try
                    {
                        SetNonce(nonce, prefix, counter);
                        aes.Decrypt(nonce, ReadOnlySpan<byte>.Empty, footerTag, Span<byte>.Empty, footerAad);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(footerAad);
                        CryptographicOperations.ZeroMemory(footerTag);
                    }

                    if (encrypted.ReadByte() != -1)
                        throw new InvalidDataException("El contenedor contiene datos adicionales después del cierre autenticado.");
                }
                destination.Flush();
            }
            finally { CryptographicOperations.ZeroMemory(nonce); }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(prefix);
            if (protectedKey is not null) CryptographicOperations.ZeroMemory(protectedKey);
            if (header is not null) CryptographicOperations.ZeroMemory(header);
            if (headerTag is not null) CryptographicOperations.ZeroMemory(headerTag);
            if (key is not null) CryptographicOperations.ZeroMemory(key);
        }
    }

    internal static byte[] BuildHeader(byte[] prefix, byte[] protectedKey, int version = CurrentVersion)
    {
        if (prefix.Length != NoncePrefixSize)
            throw new ArgumentException("El prefijo nonce no tiene el tamaño esperado.", nameof(prefix));
        if (protectedKey.Length is <= 0 or > MaximumProtectedKeySize)
            throw new CryptographicException("DPAPI devolvió una clave protegida con tamaño no válido.");

        var header = new byte[28 + protectedKey.Length];
        Magic.CopyTo(header, 0);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8, 4), version);
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

    internal static byte[] BuildFooterAad(byte[] header, ReadOnlySpan<byte> footer)
    {
        if (footer.Length != 20)
            throw new ArgumentException("El cierre no tiene el tamaño esperado.", nameof(footer));
        var aad = new byte[header.Length + footer.Length];
        header.CopyTo(aad, 0);
        footer.CopyTo(aad.AsSpan(header.Length));
        return aad;
    }

    internal static void SetNonce(byte[] nonce, byte[] prefix, uint counter)
    {
        prefix.CopyTo(nonce, 0);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(NoncePrefixSize), counter);
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

    internal static byte[] UnprotectKey(byte[] protectedKey)
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
        try
        {
            Marshal.Copy(value, 0, pointer, value.Length);
            return new DataBlob { Size = value.Length, Data = pointer };
        }
        catch
        {
            Marshal.FreeHGlobal(pointer);
            throw;
        }
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
            try
            {
                if (blob.Data != IntPtr.Zero && blob.Size > 0)
                    Marshal.Copy(new byte[blob.Size], 0, blob.Data, blob.Size);
            }
            finally
            {
                if (blob.Data != IntPtr.Zero) LocalFree(blob.Data);
            }
        }
    }

    private static void FreeInput(DataBlob blob)
    {
        if (blob.Data == IntPtr.Zero) return;
        try
        {
            if (blob.Size > 0)
                Marshal.Copy(new byte[blob.Size], 0, blob.Data, blob.Size);
        }
        finally { Marshal.FreeHGlobal(blob.Data); }
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
