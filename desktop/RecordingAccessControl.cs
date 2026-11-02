namespace Transcriptor.Desktop;

/// <summary>Fachada de compatibilidad para proteger carpetas de sesión.</summary>
public static class RecordingAccessControl
{
    public static void RestrictSessionDirectory(string sessionDirectory) =>
        Transcriptor.Rec.RecordingAccessControl.RestrictSessionDirectory(sessionDirectory);
}
