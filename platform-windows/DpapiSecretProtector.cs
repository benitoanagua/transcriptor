using System.ComponentModel;
using System.Runtime.InteropServices;
using Transcriptor.Transcription;

namespace Transcriptor.Platform.Windows;

/// <summary>Windows DPAPI adapter. DPAPI scope and UI flags remain owned by this implementation.</summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    public static DpapiSecretProtector Instance { get; } = new();

    private const uint CryptProtectUiForbidden = 0x1;

    private DpapiSecretProtector() { }

    public byte[] Protect(byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return ProtectKey(secret);
    }

    public byte[] Unprotect(byte[] protectedSecret)
    {
        ArgumentNullException.ThrowIfNull(protectedSecret);
        return UnprotectKey(protectedSecret);
    }

    internal static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("El cifrado de claves requiere Windows DPAPI.");
    }

    // Protección de claves transferida sin cambios desde Transcriptor.Transcription.
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
