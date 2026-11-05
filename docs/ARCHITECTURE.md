# Arquitectura

## Visión general

Transcriptor es una aplicación Windows local. La captura, el cifrado, la biblioteca y la transcripción se ejecutan en el equipo del usuario. No hay base de datos ni servicio remoto para procesar audio. Whisper.cpp se ejecuta como proceso nativo local.

Los proyectos están implementados en C# y .NET 10. No hay un archivo `.sln`: los proyectos se compilan y prueban mediante sus rutas `.csproj`. La interfaz de escritorio está implementada con Avalonia 12; la captura también está disponible por consola.

## Proyectos

La base se divide en una capa portable (sin dependencias de Windows) y una capa Windows:

| Proyecto | Tipo | Responsabilidad |
|---|---|---|
| `transcription/` | Biblioteca portable (net10.0) | Contenedor cifrado `.trn` (formato exacto v1/v2), pistas de audio cifradas (sin NAudio), operaciones de archivos autenticadas, motor Whisper, normalización como puerto, combinación SRT, leases e integridad de sesiones |
| `ui/` | Biblioteca portable (net10.0) | Ventana Avalonia y servicios de la aplicación: `UiServices` (puertos inyectados), `AppSettings`/`SettingsStore`, `ISessionStore`/`RecordingPrivacy`, `RecordingDirectoryFactory`, `IDefaultPaths`/`PlatformDefaultPaths` |
| `platform-windows/` | Biblioteca Windows (net10.0-windows) | DPAPI (`DpapiSecretProtector`), ACL NTFS (`WindowsSessionDirectorySecurity`), captura WASAPI (`DualRecorder`, `DualCaptureStopGate`), reproducción y normalización NAudio/Media Foundation, migración de sesiones |
| `desktop-avalonia/` | Aplicación Windows | Cabeza de producción: composition root manual que registra DPAPI, ACLs, WASAPI y migración en `UiServices` |
| `desktop-dev/` | Aplicación portable | Cabeza INSEGURO de desarrollo: fakes rotulados (`InsecureDevSecretProtector` sin cifrado real ni captura) y biblioteca de ejemplo en `/tmp/TranscriptorDev`; permite ejecutar la UI en WSLg/Linux |
| `rec/` | Ejecutable Windows | CLI de captura que consume `platform-windows` |
| `setup/` | Ejecutable | Descarga del modelo GGML y configuración de variables de entorno de Whisper |
| `tests/Transcriptor.Tests/` | xUnit portable | Suite portable en Linux: cifrado (con protector reversible de pruebas), motor, configuración, puertos de aplicación |
| `tests/Transcriptor.Tests.Windows/` | xUnit Windows | Suite exclusiva Windows: DPAPI real, ACL NTFS, migración, árbol de procesos, gate de captura |

### Resolución del protector de secretos

El contenedor portable ya no asume DPAPI por defecto. Cada cabeza de aplicación (composition root) registra su `ISecretProtector` en el arranque con `EncryptedFileContainer.ConfigureDefaultSecretProtector`: DPAPI en Windows, protector INSEGURO rotulado en desarrollo y protector reversible de pruebas en la suite portable. El formato de bytes de `.trn` es idéntico en todas las plataformas y está cubierto por pruebas de ida y vuelta portables.

La suite portable usa un protector reversible que sepulta la independencia del formato `.trn` respecto de DPAPI: las mismas pruebas de v1, cierre autenticado y detección de manipulación se ejecutan en Linux.

## Desarrollo sin Windows (WSLg)

En WSLg/Linux se puede compilar todo lo portable, ejecutar la suite portable y arrancar la cabeza de desarrollo:

```bash
dotnet test tests/Transcriptor.Tests/Transcriptor.Tests.csproj
dotnet run --project desktop-dev/Transcriptor.Dev.csproj -- --smoke-test   # headless, para CI
dotnet run --project desktop-dev/Transcriptor.Dev.csproj                    # ventana en WSLg
```

La cabeza de desarrollo muestra el banner «MODO DESARROLLO — sin cifrado real ni captura de audio» y nunca debe distribuirse: el guard del workflow de release falla si detecta sus ensamblados o símbolos en el paquete.

## Modelo de sesión

La estructura es:

```text
%USERPROFILE%/Transcriptor/Grabaciones/AAAA-MM-DD/HHMMSS_referencia/
  yo.trn
  cliente.trn
  yo.srt.trn
  cliente.srt.trn
  transcripcion.srt.trn
  transcripcion.txt.trn
```

- `yo.trn`: micrófono.
- `cliente.trn`: loopback de la salida del sistema.
- `yo.srt.trn` y `cliente.srt.trn`: subtítulos de cada pista.
- `transcripcion.srt.trn` y `transcripcion.txt.trn`: resultado combinado.

Las sesiones antiguas pueden contener WAV, SRT y TXT en claro. La aplicación permite convertirlas manualmente y conserva los originales hasta verificar copias cifradas completas.

## Captura

1. La aplicación y la CLI exigen confirmación de autorización antes de capturar.
2. Se crea la carpeta de sesión y se aplica una ACL NTFS protegida para el usuario actual, SYSTEM y administradores locales. Se rechazan rutas con enlaces o puntos de reanálisis.
3. `RecordingSession` selecciona dispositivos WASAPI y delega en `DualRecorder`.
4. `DualRecorder` crea un `EncryptedAudioTrackWriter` por pista y escribe directamente los contenedores cifrados.
5. El micrófono y el loopback se capturan de forma independiente.

## Transcripción

El motor descifra las pistas a WAV temporales autenticados, normaliza con NAudio/Media Foundation y ejecuta Whisper.cpp localmente. Después combina los SRT y publica resultados cifrados solo tras verificar autenticidad y contenido. Los leases exclusivos coordinan reproducción, transcripción, conversión, eliminación y retención. Las operaciones fallidas pueden reintentarse desde las pistas originales.

## Seguridad y privacidad

- AES-256-GCM por bloques y claves protegidas por DPAPI `CurrentUser`.
- ACL NTFS para limitar el acceso a carpetas de sesión.
- Rechazo de rutas con puntos de reanálisis.
- Temporales en claro dentro de carpetas protegidas y limpieza al terminar.
- La transcripción no se inicia automáticamente al completar una grabación.
- El borrado ordinario no equivale a borrado seguro.

## Interfaz

La aplicación Avalonia utiliza una única ventana sin decoraciones nativas, con cabecera propia, controles de ventana integrados y cuatro paneles funcionales (biblioteca, dispositivos, ajustes y registro). Los paneles se pueden mostrar u ocultar desde la cabecera; su visibilidad se persiste en la configuración local y siempre permanece al menos un panel visible. La captura y la transcripción continúan de forma independiente de la visibilidad del panel activo. La interfaz no incorpora herramientas de edición de audio.


## CI y distribución

`dotnet.yml` ejecuta dos jobs: uno en Linux que compila los proyectos portables, corre la suite portable y hace el smoke test headless de la cabeza de desarrollo; y otro en Windows que compila todo y corre ambas suites (portable y Windows-only). `release-windows.yml` se ejecuta con `workflow_dispatch` o etiquetas `v*`; compila los proyectos Windows, ejecuta ambas suites, realiza el smoke test de escritorio y genera el ZIP único `Transcriptor-win-x64.zip` con la aplicación autocontenida, setup, CLI, documentación, licencia, avisos e inventarios NuGet. Antes de publicar, una guardia verifica que el ZIP no contenga ensamblados ni símbolos del head INSEGURO de desarrollo. No se ejecuta en pull requests porque esa validación ya corresponde a `dotnet.yml`.
