# Transcriptor

Graba llamadas de atención al cliente en dos pistas (tu micrófono y el audio del sistema) y las transcribe **localmente** con Whisper. Solo transcripción. Filosofía KISS.

- `rec/` – grabador de consola para Windows (C#, NAudio). Genera `yo.wav` y `cliente.wav`.
- `transcribe/` – transcripción en WSL con whisper.cpp; une ambas pistas en `transcripcion.txt` y `.srt`.
- La **carpeta de grabaciones es la biblioteca**: no hay base de datos.

Diseño completo en [ARCHITECTURE.md](ARCHITECTURE.md). La especificación de producto está en el documento de requerimientos y casos de uso.

> **Antes de grabar:** confirma con TI y con el área legal que tienes autorización, y que el cliente sea informado o dé su consentimiento.

## Requisitos

- Windows 11 con WSL2 (Arch) y VS Code con la extensión WSL.
- Todo lo demás lo instala `scripts/setup-wsl.sh`: SDK de .NET, ffmpeg, python3, whisper.cpp y el modelo `small`.

## Inicio rápido

```bash
./scripts/setup-wsl.sh                 # una sola vez
make build                             # comprueba que compila
make run ARGS='--ref "FAE soporte"'    # compila, copia a Windows y graba (Enter = detener)
make transcribe                        # transcribe las llamadas nuevas
grep -ri "viaje" "$TRANSCRIPTOR_DIR"   # buscar en todas las llamadas
```

`make help` lista todos los comandos.

## Resultado

```
Grabaciones/
└── 2026-11-01/
    └── 091608_FAE-soporte/
        ├── yo.wav
        ├── cliente.wav
        ├── yo.srt            (intermedio)
        ├── cliente.srt       (intermedio)
        ├── transcripcion.txt  [00:00:12] Cliente: …
        └── transcripcion.srt
```

## Ajustes

Sin pantalla de ajustes: argumentos y variables de entorno.

| Qué | Cómo |
|---|---|
| Micrófono / salida a capturar | `--mic`, `--sys` (parte del nombre; `--list` muestra los disponibles) |
| Carpeta de grabaciones | `--out` o `TRANSCRIPTOR_DIR` |
| Modelo, idioma | `WHISPER_MODEL` (por defecto `~/models/ggml-small.bin`), `WHISPER_LANG` (`es`) |

## Problemas conocidos

- El audio del sistema incluye **todo lo que suena** en la salida elegida. Para la llamada, usa una salida dedicada (por ejemplo, el headset) y cierra otras fuentes de sonido.
- Si Avaya Workplace, Teams o WhatsApp usan otro dispositivo, indícalo con `--sys`.
- Si compilar desde WSL falla, instala solo el SDK de .NET en Windows y usa `dotnet.exe` desde WSL.
