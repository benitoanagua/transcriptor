# Arquitectura actual

## Visión general

Transcriptor es una aplicación Windows local. La captura, el cifrado, la biblioteca y la transcripción se ejecutan en el equipo del usuario. No hay base de datos ni servicio remoto para procesar audio. Whisper.cpp es el único motor externo de procesamiento: se ejecuta como proceso nativo local.

La solución usa .NET 10. La interfaz principal es WPF y la captura también está disponible por consola.

## Proyectos

| Proyecto | Tipo | Responsabilidad |
|---|---|---|
| `desktop/` | WPF | Configuración, consentimiento, selección de dispositivos, captura, biblioteca, reproducción, visor, búsqueda, migración, eliminación y retención |
| `rec/` | Ejecutable y API de captura | CLI y abstracción reutilizable `RecordingSession`; captura WASAPI de dos pistas |
| `transcribe/` | Biblioteca | Contenedor cifrado, audio cifrado, ACL, operaciones de archivos, motor Whisper, normalización y combinación SRT |
| `transcribe/tests/` | xUnit | Pruebas de cifrado, pistas, SRT, ACL, migración, privacidad y configuración |
| `setup/` | Ejecutable | Descarga del modelo GGML y configuración de variables de entorno de Whisper |
| `.github/workflows/dotnet.yml` | GitHub Actions | Compilación de proyectos y ejecución de pruebas en Windows |

WPF referencia `rec/` para reutilizar `RecordingSession` y referencia `transcribe/` para invocar directamente `TranscriptionEngine`. No inicia otro proceso .NET para transcribir. El cierre de ventana se aplaza hasta que termine el cierre de la captura; las solicitudes simultáneas reutilizan la misma tarea de finalización. La biblioteca inicia `whisper-cli` con argumentos separados mediante `ProcessStartInfo.ArgumentList`, sin shell.

## Modelo de sesión

La estructura nueva es:

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

Las sesiones heredadas pueden contener `yo.wav`, `cliente.wav`, SRT y TXT en claro. La aplicación mantiene compatibilidad y no migra automáticamente esos archivos.

## Captura

1. WPF exige la confirmación de autorización; la CLI también solicita confirmación interactiva.
2. Se crea la carpeta de sesión y se aplica una ACL NTFS protegida para el usuario actual, SYSTEM y administradores locales. Se rechazan rutas con enlaces o puntos de reanálisis.
3. `RecordingSession` selecciona los dispositivos WASAPI y delega en `DualRecorder`.
4. `DualRecorder` crea un `EncryptedAudioTrackWriter` por pista y escribe directamente `yo.trn` y `cliente.trn`; no genera WAV intermedio durante la captura.
5. Se captura el micrófono con WASAPI y el audio de salida mediante loopback. Una salida silenciosa mantiene activo el flujo loopback cuando no hay sonido de usuario.
6. Al detener, se esperan las notificaciones de cierre de ambas capturas (con un límite de 15 segundos) y se propagan errores de captura o escritura.
7. Si el inicio falla después de arrancar solo uno de los dispositivos, se detiene el que sí arrancó y se cierran ambos escritores de forma idempotente. Si la sesión se descarta sin iniciarse, los escritores también se cierran. La liberación de un dispositivo no impide intentar liberar los demás recursos nativos.

## Cifrado y archivos

`EncryptedFileContainer` implementa un contenedor versionado con AES-256-GCM por bloques de 64 KiB. Cada archivo usa una DEK aleatoria de 256 bits protegida con DPAPI `CurrentUser`. El contenedor autentica cabecera, contador y longitud de cada bloque. El método de cifrado de streams reutiliza `EncryptedFileWriter`, de modo que la escritura normal y la incremental comparten una única implementación de bloques y cierre. `EncryptedAudioTrack` conserva la descripción de formato PCM/IEEE-float junto con las muestras.

`EncryptedFileOperations` publica archivos cifrados mediante temporales en el mismo directorio y movimiento final. El descifrado se realiza primero a un temporal privado y solo devuelve la ruta después de autenticar el contenedor completo.

Los archivos temporales en claro se eliminan mediante borrado normal de mejor esfuerzo. No existe garantía de borrado seguro ante interrupciones, fallos del sistema o copias del sistema de archivos.

## Transcripción

1. `TranscriptionEngine` valida la raíz y recorre las carpetas de fecha y sesión, omitiendo enlaces y puntos de reanálisis.
2. Una sesión es candidata cuando tiene ambas pistas (cifradas o WAV heredadas) y todavía no tiene `transcripcion.txt` ni `transcripcion.txt.trn`.
3. Antes de escribir en una sesión heredada o nueva, aplica la ACL de sesión.
4. Cada pista cifrada se autentica y reconstruye a WAV temporal; los WAV heredados se leen directamente. NAudio/Media Foundation normaliza a 16 kHz, mono, PCM de 16 bits.
5. Se ejecuta Whisper.cpp localmente. Los SRT individuales se validan y combinan por marca temporal, conservando la etiqueta de hablante.
6. Se generan SRT/TXT combinados y se cifran como archivos `.trn`. Cada archivo se descifra a un temporal autenticado y se compara por SHA-256 con el original; solo después se elimina el original. El TXT cifrado se publica al final.

Los temporales WAV se eliminan en bloques `finally`; los archivos de trabajo se limpian mediante borrado normal cuando el flujo llega a su limpieza. Si se interrumpe la transcripción, la siguiente ejecución vuelve a generar los SRT desde las pistas originales. Una sesión solo se omite cuando los cuatro artefactos cifrados existen, no quedan equivalentes en claro y su autenticidad se valida. Un resultado dañado se trata como incompleto y puede regenerarse.

Los fallos se registran por sesión y no impiden intentar las demás. El proceso informa al final si hubo sesiones fallidas. Las transcripciones son derivados regenerables; el audio original sigue siendo la fuente de verdad.

## Biblioteca y ciclo de vida

WPF enumera sesiones por sus pistas reconocidas y valida la ruta y el contenido antes de leer transcripciones o reproducir pistas. Si el contenido está cifrado, aplica ACL antes de crear temporales en claro. También permite buscar texto, migrar sesiones heredadas, eliminar una sesión y aplicar retención manual. La biblioteca y las operaciones de eliminación reutilizan la validación compartida de rutas y contenido de sesión para rechazar enlaces en antecesores y dentro de la sesión. La migración verifica las copias cifradas antes de eliminar los originales reconocidos. La retención no se ejecuta automáticamente.

## Configuración

- Preferencias WPF: `%LOCALAPPDATA%\Transcriptor\settings.json`.
- La CLI usa `TRANSCRIPTOR_DIR` o `%USERPROFILE%\Transcriptor\Grabaciones`.
- Whisper se configura en WPF mediante rutas explícitas o mediante `WHISPER_BIN`, `WHISPER_MODEL` y `WHISPER_LANG` en el entorno.
- `setup/` descarga `ggml-small.bin` desde una revisión fijada, verifica su huella SHA-256, vuelve a descargarlo si falta o no coincide y configura las variables de entorno de usuario para el ejecutable y el modelo.

## Validación

GitHub Actions usa Windows y .NET 10 para compilar recorder, biblioteca de transcripción, setup y WPF, y ejecutar xUnit. Las pruebas no validan dispositivos de audio reales, Media Foundation en todas las máquinas, calidad del modelo Whisper ni comportamiento frente a cortes eléctricos. Esos escenarios requieren pruebas funcionales en Windows.

