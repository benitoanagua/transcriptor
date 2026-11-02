# Arquitectura

## Principios

1. **C# y .NET 10 para toda la lógica del producto.** No se incorporan scripts Bash, Python o JavaScript.
2. Los WAV originales son la fuente de verdad; las transcripciones son derivados regenerables.
3. La carpeta de grabaciones es la biblioteca. No hay base de datos ni servicio remoto.
4. Whisper.cpp se conserva únicamente como motor nativo especializado. La aplicación .NET configura y ejecuta el proceso y gestiona sus resultados.

## Componentes

| Proyecto | Responsabilidad | Tecnología |
|---|---|---|
| `rec/` | Captura simultánea del micrófono y loopback | C#, .NET 10, NAudio/WASAPI |
| `transcribe/` | Descubrimiento de llamadas, normalización WAV, ejecución Whisper y unión SRT | C#, .NET 10, NAudio, Process |
| `setup/` | Validación del ejecutable Whisper, descarga del modelo y configuración de entorno | C#, .NET 10, HttpClient |
| `.github/workflows/dotnet.yml` | Compilación y pruebas automatizadas | GitHub Actions |

## Contrato de datos

```text
<TRANSCRIPTOR_DIR>/AAAA-MM-DD/HHMMSS[_referencia]/
  yo.wav
  cliente.wav
  yo.srt
  cliente.srt
  transcripcion.txt
  transcripcion.srt
```

Una llamada está pendiente cuando no tiene `transcripcion.txt` y contiene ambas pistas WAV. Las carpetas incompletas se omiten para no generar transcripciones engañosas.

## Flujo de grabación

1. `DualRecorder` abre los dispositivos WASAPI y los escritores WAV.
2. Las pistas se escriben de forma independiente.
3. Al detener, se esperan las notificaciones `RecordingStopped` de ambas capturas antes de considerar cerrados los archivos.
4. Los errores de escritura y captura se conservan y se comunican al proceso principal.

## Flujo de transcripción

1. `TranscriptionEngine` busca carpetas de llamadas bajo la raíz configurada.
2. Solo procesa carpetas con `yo.wav` y `cliente.wav` sin marcador `transcripcion.txt`.
3. NAudio/Media Foundation convierte cada pista a 16 kHz, mono, PCM de 16 bits en un archivo temporal.
4. La aplicación inicia `whisper-cli` mediante `ProcessStartInfo.ArgumentList`; no utiliza una shell ni concatena argumentos en una línea de comandos.
5. `SrtMerger` valida marcas de tiempo, conserva la etiqueta del hablante y ordena los segmentos.
6. Los archivos combinados se escriben primero en temporales. El marcador `transcripcion.txt` solo se publica después de que ambas pistas se hayan procesado y exista contenido válido.

## Configuración

`Transcriptor.Setup` valida la ruta del ejecutable Whisper, descarga `ggml-small.bin` si no existe y guarda las variables de entorno del usuario. La distribución del motor nativo se obtiene desde el proyecto oficial de whisper.cpp; no se compila ni se modifica desde este repositorio.

## Pruebas y CI

Las pruebas xUnit cubren el orden temporal, las etiquetas de hablante, la escritura de resultados y el rechazo de transcripciones vacías. GitHub Actions compila los tres proyectos y ejecuta las pruebas en Windows con .NET 10.

## Límites y seguridad

- WASAPI y Media Foundation requieren Windows.
- El loopback captura todo el sonido del dispositivo elegido.
- La ejecución de Whisper es local; no se envían audios a APIs externas.
- Los permisos del directorio, el consentimiento, la retención y el borrado verificable deben ajustarse a las políticas y leyes aplicables.
