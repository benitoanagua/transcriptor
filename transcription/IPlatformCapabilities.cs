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
    /// True when the host prefers native window decorations (title bar and
    /// system resize frame). Consoles on Linux/macOS answer true; Windows heads
    /// answer false and draw their own compact chrome using
    /// <c>WindowDecorations.BorderOnly</c> plus embedded move drag.
    /// </summary>
    bool PrefersNativeWindowChrome { get; }

    /// <summary>Short human-readable platform name for diagnostics.</summary>
    string Name { get; }
}
