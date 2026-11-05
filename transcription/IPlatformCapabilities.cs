namespace Transcriptor.Transcription;

/// <summary>
/// Port describing what the current host platform actually supports. The UI must
/// use these answers to show, hide or degrade features instead of assuming
/// Windows. No platform tailoring may be expressed with conditional compilation
/// inside portable views.
/// </summary>
public interface IPlatformCapabilities
{
    /// <summary>True when the platform can capture the system (loopback) track for the selected output.</summary>
    bool SupportsSystemLoopback { get; }

    /// <summary>True when stored sessions are cryptographically protected at rest with real keys (DPAPI or equivalent).</summary>
    bool SupportsEncryptionAtRest { get; }

    /// <summary>True when the platform can play decrypted audio through an output device.</summary>
    bool SupportsAudioPlayback { get; }

    /// <summary>
    /// True when the head prefers native window decorations. Supplied desktop
    /// heads use them to preserve platform resize and snap behavior; a head may
    /// opt out and draw compact chrome with <c>WindowDecorations.BorderOnly</c>.
    /// </summary>
    bool PrefersNativeWindowChrome { get; }

    /// <summary>Short human-readable platform name for diagnostics.</summary>
    string Name { get; }
}
