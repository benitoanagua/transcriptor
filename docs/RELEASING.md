# Publicar Transcriptor para Windows

## Validación previa

1. Confirma que .NET CI y Windows Release están verdes en el commit que se publicará.
2. Ejecuta las pruebas funcionales del procedimiento [HARDWARE-VALIDATION.md](HARDWARE-VALIDATION.md) en un equipo Windows.
3. Comprueba que los avisos de terceros y el inventario NuGet corresponden a las dependencias actuales.

## Smoke test de CI

`Transcriptor.Desktop.exe --smoke-test` inicia la aplicación Avalonia y la cierra automáticamente tras 2 segundos; el workflow `release-windows.yml` lo invoca en el paso “Smoke test Avalonia desktop startup” y espera el código de salida `0`.

## Paquetes

El workflow Windows Release genera un ZIP autocontenido x64:

- `Transcriptor-win-x64.zip`: aplicación de escritorio `Transcriptor.Desktop.exe`, `tools/setup/transcriptor-setup.exe`, `tools/rec/rec.exe`, documentación, licencia y avisos, e inventarios de dependencias.

El ZIP tiene un archivo SHA-256 independiente. El workflow inspecciona sus entradas y verifica el checksum antes de publicar.

## Publicación

El workflow `release-windows.yml` se ejecuta únicamente mediante `workflow_dispatch` o un push de etiqueta `v*`; no se ejecuta en pull requests porque `dotnet.yml` ya compila los cuatro proyectos y ejecuta xUnit en cada PR hacia `main`. `workflow_dispatch` valida el paquete y sube el ZIP y su checksum como artefacto temporal, sin crear un Release. Una etiqueta `v*` realiza la misma validación y publica ambos archivos en GitHub Releases.

Whisper.cpp y el modelo GGML se obtienen por separado y no se incluyen en los paquetes.
