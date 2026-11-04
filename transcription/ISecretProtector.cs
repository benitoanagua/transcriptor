namespace Transcriptor.Transcription;

/// <summary>
/// Port for protecting per-file data-encryption keys. The production Windows
/// implementation (DPAPI CurrentUser) lives in Transcriptor.Platform.Windows; test
/// and development implementations remain outside the release head. Alternate
/// implementations enable isolated tests and future platform adapters without
/// changing the AES-GCM container format.
/// </summary>
public interface ISecretProtector
{
    byte[] Protect(byte[] secret);
    byte[] Unprotect(byte[] protectedSecret);
}
