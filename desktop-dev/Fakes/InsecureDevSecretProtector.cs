using System.IO;
using Transcriptor.Transcription;

namespace Transcriptor.Dev;

// ⚠️ INSEGURO — SOLO DESARROLLO ⚠️
// Sustituto de DPAPI sin ninguna protección: el "secreto protegido" es una simple
// copia en claro con un prefijo literal. Nunca debe existir en una build de release.
public sealed class InsecureDevSecretProtector : ISecretProtector
{
    private static readonly byte[] Marker = "DEVPRT01"u8.ToArray();

    public byte[] Protect(byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        var result = new byte[Marker.Length + secret.Length];
        Marker.CopyTo(result, 0);
        secret.CopyTo(result, Marker.Length);
        return result;
    }

    public byte[] Unprotect(byte[] protectedSecret)
    {
        ArgumentNullException.ThrowIfNull(protectedSecret);
        if (protectedSecret.Length < Marker.Length
            || !protectedSecret.AsSpan(0, Marker.Length).SequenceEqual(Marker))
            throw new InvalidOperationException("El secreto no fue protegido por el protector inseguro de desarrollo.");

        return protectedSecret[Marker.Length..].ToArray();
    }
}
