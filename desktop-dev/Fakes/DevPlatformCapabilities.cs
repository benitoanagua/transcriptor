using Transcriptor.Transcription;

namespace Transcriptor.Dev;

// ⚠️ DATOS FICTICIOS — SOLO DESARROLLO ⚠️
// "Capacidades" de la plataforma para la cabeza de desarrollo: reproduce (fake)
// y permite el carril de sistema (fake), pero deja claro que NO hay cifrado real.
public sealed class DevPlatformCapabilities : IPlatformCapabilities
{
    public bool SupportsSystemLoopback => true;
    public bool SupportsEncryptionAtRest => false;
    public bool SupportsAudioPlayback => true;
    public bool PrefersNativeWindowChrome => true; // Conserva título, resize y controles nativos en Linux/WSLg.
    public string Name => "Desarrollo (INSEGURO)";
}
