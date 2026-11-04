# Publicar Transcriptor para Windows

## Validación previa

1. Confirma que .NET CI y Windows Release están verdes en el commit que se publicará.
2. Ejecuta las pruebas funcionales del procedimiento [HARDWARE-VALIDATION.md](HARDWARE-VALIDATION.md) en un equipo Windows.
3. Comprueba que los avisos de terceros y el inventario NuGet corresponden a las dependencias actuales.

## Smoke test de CI

`Transcriptor.Desktop.exe --smoke-test` inicia la aplicación Avalonia y la cierra automáticamente tras 2 segundos; el workflow `release-windows.yml` lo invoca en el paso “Smoke test Avalonia desktop startup” y espera el código de salida `0`.

## Paquetes

El workflow Windows Release genera dos ZIP autocontenidos x64:

- `Transcriptor-win-x64.zip`: aplicación de escritorio `Transcriptor.Desktop.exe`, `tools/setup/transcriptor-setup.exe`, `tools/rec/rec.exe`, documentación, licencia y avisos, e inventarios de dependencias.
- `Transcriptor-win-x64-portable.zip`: el mismo contenido, más `portable.flag` y el directorio `data/` para configuración y grabaciones portables.

Cada ZIP tiene un archivo SHA-256 independiente. El workflow inspecciona sus entradas y verifica los checksums antes de publicar.

## Publicación

Un push de una etiqueta `v*` ejecuta la publicación en GitHub Releases. Un `workflow_dispatch` valida los paquetes y sube los ZIP como artefactos temporales, sin crear un Release.

Whisper.cpp y el modelo GGML se obtienen por separado y no se incluyen en los paquetes.
