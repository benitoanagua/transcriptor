using System.Runtime.CompilerServices;
using Transcriptor.Platform.Windows;
using Transcriptor.Transcription;

namespace Transcriptor.Transcription.Tests.Windows;

/// <summary>
/// Configura el protector por defecto de este ensamblado de pruebas con DPAPI
/// real, replicando la resolución que la cabeza de producción de Windows hace
/// en su composition root. Así los servicios que completan el protector
/// predeterminado (p. ej. RecordingMigrationService) se ejercitan tal y como
/// lo hacen en producción.
/// </summary>
internal static class WindowsTestSetup
{
    [ModuleInitializer]
    internal static void Initialize() =>
        EncryptedFileContainer.ConfigureDefaultSecretProtector(DpapiSecretProtector.Instance);
}
