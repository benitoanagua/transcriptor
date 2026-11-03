# Transcriptor

[![.NET CI](https://github.com/benitoanagua/transcriptor/actions/workflows/dotnet.yml/badge.svg?branch=main)](https://github.com/benitoanagua/transcriptor/actions/workflows/dotnet.yml)
[![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D6)](https://github.com/benitoanagua/transcriptor)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)

**Local-first, privacy-focused call recorder and transcription app for Windows.** Captures microphone and system audio into separate encrypted tracks, then transcribes them locally with Whisper.cpp — no cloud audio upload.

*Aplicación local-first para grabar y transcribir llamadas en Windows. Captura micrófono y audio del sistema en pistas cifradas independientes y genera transcripciones localmente con Whisper.cpp, sin subir el audio a servicios en la nube.*

## Why Transcriptor?

- **Privacy by design:** audio tracks and completed transcripts are encrypted at rest with AES-256-GCM; encryption keys are protected with Windows DPAPI.
- **Two independent tracks:** records microphone and system/loopback audio simultaneously, preserving speaker-source separation.
- **Offline transcription:** runs Whisper.cpp locally on the user's Windows machine.
- **Recoverable workflows:** validates encrypted outputs, retries interrupted transcription, and marks incomplete recordings instead of silently treating partial captures as valid.
- **Built for Windows:** C# / .NET 10, WPF, WASAPI, NAudio and Media Foundation.

> **Responsible recording:** notify everyone involved and obtain the required authorization before recording. Follow applicable laws and define an appropriate retention policy.

## Licencia y componentes de terceros

El código de Transcriptor se distribuye bajo Apache License 2.0; consulta [LICENSE](LICENSE). Los avisos de componentes de terceros se incluyen en [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Los paquetes oficiales de Windows incluyen el runtime de .NET 10 en modalidad autocontenida; revisa también las condiciones y avisos que acompañan a esa distribución. Whisper.cpp y sus modelos se obtienen por separado y no se incluyen en los ZIP oficiales.

El aviso de terceros identifica las dependencias directas declaradas y no sustituye un inventario/SBOM completo de dependencias transitivas.
