namespace Transcriptor.Transcription;

/// <summary>
/// Port for protecting per-file data-encryption keys. The production implementation
/// remains Windows DPAPI CurrentUser; alternate implementations enable isolated tests
/// and future platform adapters without changing the AES-GCM container format.
/// </summary>
public interface ISecretProtector
{
    byte[] Protect(byte[] secret);
    byte[] Unprotect(byte[] protectedSecret);
}

/// <summary>Windows DPAPI adapter. DPAPI scope and UI flags remain owned by the container.</summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    public static DpapiSecretProtector Instance { get; } = new();

    private DpapiSecretProtector() { }

    public byte[] Protect(byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return EncryptedFileContainer.ProtectKey(secret);
    }

    public byte[] Unprotect(byte[] protectedSecret)
    {
        ArgumentNullException.ThrowIfNull(protectedSecret);
        return EncryptedFileContainer.UnprotectKey(protectedSecret);
    }
}
