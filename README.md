# Transcriptor

Aplicación local para grabar llamadas de atención al cliente en dos pistas independientes y transcribirlas con Whisper.cpp. La lógica del producto está implementada en **C# y .NET 10**. No requiere base de datos ni servicios en la nube.

- `rec/`: captura micrófono y audio del sistema con NAudio/WASAPI.
- `transcribe/`: normaliza WAV, ejecuta Whisper.cpp y combina los subtítulos en C#.
- `setup/`: configura el ejecutable nativo de Whisper y descarga el modelo.
- La carpeta de grabaciones es la biblioteca; los archivos WAV originales son la fuente de verdad.

> Antes de grabar, confirma que tienes autorización y que la otra parte ha sido informada o ha dado su consentimiento. Define también las reglas de retención y borrado aplicables.

## Requisitos

- Windows 11.
- SDK de .NET 10.
- Un ejecutable oficial de Whisper.cpp para Windows (`whisper-cli.exe`).

## Configuración inicial

Obtén `whisper-cli.exe` desde una distribución oficial de [whisper.cpp](https://github.com/ggml-org/whisper.cpp/releases) y ejecuta el configurador indicando su ruta:

```text
dotnet run --project setup/Transcriptor.Setup.csproj -- "C:\\Herramientas\\whisper\\whisper-cli.exe"
```

El configurador descarga el modelo `ggml-small.bin` y guarda `WHISPER_BIN` y `WHISPER_MODEL` en el entorno del usuario. Abre una terminal nueva después de configurarlo.

## Compilar y grabar

```text
dotnet build rec/Rec.csproj --configuration Release
dotnet run --project rec/Rec.csproj -- --list
dotnet run --project rec/Rec.csproj -- --ref "FAE soporte"
```

Pulsa Enter para detener la grabación. Se generan `yo.wav` y `cliente.wav` dentro de una carpeta por fecha y hora.

## Transcribir llamadas pendientes

```text
dotnet run --project transcribe/Transcriptor.Transcription.csproj
```

El proceso solo toma llamadas que contienen ambas pistas WAV y todavía no tienen `transcripcion.txt`. Convierte cada pista a 16 kHz mono PCM mediante NAudio/Media Foundation, ejecuta Whisper.cpp y combina los SRT por tiempo. Si falla una pista, no publica el archivo marcador de transcripción completa; se puede corregir la causa y volver a ejecutar.

## Configuración

| Variable | Uso | Valor predeterminado |
|---|---|---|
| `TRANSCRIPTOR_DIR` | Carpeta raíz de grabaciones | `%USERPROFILE%\\Transcriptor\\Grabaciones` |
| `WHISPER_BIN` | Ruta de `whisper-cli.exe` | `%USERPROFILE%\\Transcriptor\\whisper\\whisper-cli.exe` |
| `WHISPER_MODEL` | Ruta al modelo GGML | `%USERPROFILE%\\Transcriptor\\models\\ggml-small.bin` |
| `WHISPER_LANG` | Idioma de transcripción | `es` |

## Estructura de una llamada

```text
Grabaciones/
└── 2026-11-01/
    └── 091608_FAE-soporte/
        ├── yo.wav
        ├── cliente.wav
        ├── yo.srt
        ├── cliente.srt
        ├── transcripcion.txt
        └── transcripcion.srt
```

Los SRT individuales son resultados intermedios regenerables. Los WAV originales deben conservarse de acuerdo con la política de retención aprobada.

## Compilar y probar

```text
dotnet build rec/Rec.csproj --configuration Release
dotnet build transcribe/Transcriptor.Transcription.csproj --configuration Release
dotnet build setup/Transcriptor.Setup.csproj --configuration Release
dotnet test transcribe/tests/Transcriptor.Transcription.Tests.csproj --configuration Release
```

GitHub Actions ejecuta estos pasos en cada push a `main` y en cada pull request.

## Límites conocidos

- La captura WASAPI y la conversión Media Foundation requieren Windows.
- El loopback captura todo el audio que sale por el dispositivo seleccionado; usa una salida dedicada para evitar mezclar otras fuentes.
- La instalación del binario nativo Whisper.cpp se realiza por separado; el configurador valida su ruta y administra el modelo.
- El repositorio no sube grabaciones ni transcripciones a servicios externos.
