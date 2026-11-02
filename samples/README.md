# Muestras

Coloca aquí un WAV de unos 20 segundos (`ejemplo.wav`) para probar la transcripción sin hacer una llamada.
Los `.wav` están ignorados por git.

Prueba rápida:

```bash
mkdir -p /tmp/prueba/2026-01-01/000000_demo
cp samples/ejemplo.wav /tmp/prueba/2026-01-01/000000_demo/yo.wav
TRANSCRIPTOR_DIR=/tmp/prueba make transcribe
```
