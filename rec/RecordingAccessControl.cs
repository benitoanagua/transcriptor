namespace Transcriptor.Rec;

/// <summary>Fachada para la protección compartida de carpetas de sesión.</summary>
public static class RecordingAccessControl
{
    public static void RestrictSessionDirectory(string sessionDirectory) =>
        Transcriptor.Transcription.RecordingAccessControl.RestrictSessionDirectory(sessionDirectory);
}
