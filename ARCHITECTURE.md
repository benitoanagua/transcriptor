# Arquitectura

## Visión general

Transcriptor es una aplicación Windows local. La captura, el cifrado, la biblioteca y la transcripción se ejecutan en el equipo del usuario. No hay base de datos ni servicio remoto para procesar audio. Whisper.cpp se ejecuta como proceso nativo local.

La solución usa C# y .NET 10. La interfaz de escritorio está implementada con Avalonia 12; la captura también está disponible por consola.

## Proyectos

| Proyecto | Tipo | Responsabilidad |
|---|---|---|
| `desktop-avalonia/` | Aplicación Avalonia | Preferencias, consentimiento, selección de dispositivos, captura, biblioteca, reproducción, visor, búsqueda, gestión de grabaciones y Mini-Mode |
| `rec/` | Ejecutable y API de captura | CLI y abstracción reutilizable `RecordingSession`; captura WASAPI de dos pistas |
| `transcription/` | Biblioteca | Contenedor cifrado, audio cifrado, ACL, operaciones de archivos, motor Whisper, normalización y combinación SRT |
| `tests/Transcriptor.Tests/` | xUnit | Suite unificada de pruebas para captura, escritorio, cifrado, transcripción, ACL, privacidad y configuración; referencia `rec/`, `transcription/` y `desktop-avalonia/` |
| `setup/` | Ejecutable | Descarga del modelo GGML y configuración de variables de entorno de Whisper |
| `.github/workflows/dotnet.yml` | GitHub Actions | Compilación de proyectos y suite de pruebas en Windows |

La aplicación referencia `rec/` para reutilizar `RecordingSession` y `transcription/` para invocar `TranscriptionEngine`. La biblioteca inicia `whisper-cli` con argumentos separados mediante `ProcessStartInfo.ArgumentList`, sin shell. La interfaz permite cancelar la transcripción y bloquea las operaciones incompatibles mientras se procesan sesiones. Al cerrar, espera el cierre coordinado de la captura y la liberación de recursos.

## Modelo de sesión

La estructura es:

```text
<TRANSCRIPTOR_DIR>/AAAA-MM-DD/HHMMSS_referencia/
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

La aplicación Avalonia proporciona la ventana principal y una ventana flotante Mini-Mode. La ventana compacta muestra estado, duración y niveles durante la captura, ofrece pausa/reanudación y detención confirmada, y permite consultar el estado de transcripción y controlar la reproducción.

## CI y distribución

GitHub Actions compila la aplicación Avalonia, los proyectos de captura, transcripción y configuración y ejecuta las pruebas xUnit en Windows. El workflow de release publica un ejecutable autocontenido `Transcriptor.Desktop.exe`, la utilidad de setup y la CLI dentro de los paquetes estándar y portable.
