namespace Transcriptor.Transcription;

/// <summary>
/// Default ISecretProtector resolution for call sites that do not receive one
/// explicitly. Each application head (composition root) must register its
/// implementation at startup: DPAPI on Windows, or a clearly insecure test or
/// development protector elsewhere.
/// </summary>
internal static class SecretProtectorDefaults
{
    private static volatile ISecretProtector? _default;

    public static void Configure(ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(protector);
        _default = protector;
    }

    public static ISecretProtector Required() =>
        _default ?? throw new InvalidOperationException(
            "No se configuró un protector de secretos predeterminado. La cabeza de la aplicación debe llamar a " +
            "EncryptedFileContainer.ConfigureDefaultSecretProtector con DpapiSecretProtector (Windows) o su implementación de ISecretProtector.");
}
