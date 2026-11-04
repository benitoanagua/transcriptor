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
- **Windows desktop:** C# / .NET 10, WPF (legacy desktop) and Avalonia 12 (migrated desktop), WASAPI, NAudio and Media Foundation.

> **Responsible recording:** notify everyone involved and obtain the required authorization before recording. Follow applicable laws and define an appropriate retention policy.


Transcriptor es una aplicación local para capturar llamadas en dos pistas independientes —micrófono y audio del sistema— y transcribirlas con Whisper.cpp. La aplicación y la lógica de producto están implementadas en C#/.NET 10; la interfaz de escritorio usa WPF. El procesamiento de audio y las transcripciones no requieren servicios en la nube.

## Funciones actuales

- Captura simultánea de micrófono y audio del sistema mediante WASAPI/NAudio.
- Selección de dispositivos y medidores de nivel.
- Escritura directa de las pistas en contenedores cifrados: `yo.trn` y `cliente.trn`.
- Detención coordinada de ambas pistas ante el primer fallo de escritura/captura; las sesiones incompletas se marcan y no se transcriben automáticamente.
- Cifrado AES-256-GCM por bloques y protección de claves con Windows DPAPI (`CurrentUser`).
- Transcripción local mediante Whisper.cpp, combinación de subtítulos SRT, visor, búsqueda y reproducción.
- Biblioteca local de sesiones, eliminación explícita y aplicación manual de retención.
- Migración voluntaria de sesiones heredadas en claro, con verificación de las copias cifradas antes de eliminar originales.
- Interfaz WPF y herramienta de captura por consola (`rec`).

> Antes de grabar, informa a todas las personas involucradas y confirma que cuentas con autorización. Define las reglas de retención y borrado que correspondan.

## Requisitos

- Windows 11.
- SDK de .NET 10.
- Ejecutable oficial de Whisper.cpp para Windows (`whisper-cli.exe`).
- Modelo GGML compatible con la versión del ejecutable.

WASAPI, Media Foundation, las ACL NTFS y DPAPI hacen que las operaciones de captura, normalización y cifrado dependan de Windows.

## Configuración de Whisper

Obtén `whisper-cli.exe` desde una distribución oficial de [whisper.cpp](https://github.com/ggml-org/whisper.cpp/releases). Después ejecuta el configurador con la ruta del ejecutable:

```powershell
dotnet run --project setup/Transcriptor.Setup.csproj -- "C:\Herramientas\whisper\whisper-cli.exe"
```

El configurador descarga `ggml-small.bin` desde una revisión fijada del repositorio oficial, verifica el SHA-256 del modelo existente y descarga de nuevo si falta o no coincide; solo publica la descarga después de validar tamaño y huella. Después guarda `WHISPER_BIN` y `WHISPER_MODEL` en las variables de entorno del usuario. Abre una terminal nueva para que los procesos iniciados desde ella reciban esos valores. En WPF también puedes indicar directamente las rutas del ejecutable y del modelo.

## Versión portable

El configurador también puede dejar la aplicación lista para ejecutarse desde una carpeta o unidad extraíble, sin tocar variables de entorno ni `%LOCALAPPDATA%`:

```powershell
tools\setup\transcriptor-setup.exe --portable "C:\Herramientas\whisper\whisper-cli.exe"
```

Esto copia `whisper-cli.exe` y sus DLL a `whisper\`, descarga y verifica `ggml-small.bin` en `models\`, crea `data\` y, al final, escribe `portable.flag` junto a `Transcriptor.Desktop.exe`. Con ese marcador la configuración se guarda en `data\settings.json` y las grabaciones por defecto en `data\Grabaciones`. Si la carpeta cambia de ubicación, las rutas de Whisper y del modelo se recuperan solas cuando existen en su sitio habitual. Usa `--target "carpeta"` si el configurador no está dentro del paquete. Para volver al modo normal, borra `portable.flag`.

Límites del modo portable:

- Las pistas `.trn` se cifran con DPAPI del usuario actual: solo se pueden abrir con la misma cuenta de Windows en el mismo equipo. Mover la carpeta a otro equipo conserva la aplicación y la configuración, pero no el acceso a audio ya cifrado.
- Transcriptor aplica ACL NTFS a cada sesión, así que la carpeta de grabaciones debe estar en una unidad NTFS (no exFAT ni FAT32). El configurador avisa si la unidad no lo es.

## Descargar e instalar en Windows

Las versiones publicadas se distribuyen desde [GitHub Releases](https://github.com/benitoanagua/transcriptor/releases). Descarga `Transcriptor-win-x64.zip`, extrae todo su contenido y ejecuta `Transcriptor.Desktop.exe`. El paquete de escritorio es autocontenido e incluye el runtime de .NET 10; no necesitas instalar .NET por separado.

La primera vez también debes configurar Whisper.cpp: descarga una compilación compatible de `whisper-cli.exe` desde [las versiones oficiales de whisper.cpp](https://github.com/ggml-org/whisper.cpp/releases) y un modelo GGML compatible. Puedes indicar ambas rutas en la configuración de Transcriptor o utilizar `tools/setup/transcriptor-setup.exe` incluido en el paquete para configurar el modelo y las variables de entorno. El ejecutable y el modelo de Whisper no se redistribuyen dentro del ZIP: se obtienen por separado para mantener sus versiones, licencias y requisitos de hardware bajo control.

Comprueba el archivo `Transcriptor-win-x64.zip.sha256` publicado junto al ZIP antes de instalar. Las primeras versiones pueden aparecer como *pre-release* mientras se completa la validación en equipos Windows reales.

## Aplicaciones de escritorio

La aplicación WPF se conserva como referencia durante la migración. La versión Avalonia 12 ya incluye captura dual, biblioteca, reproducción, transcripción, operaciones de ciclo de vida y Mini-Mode. El paquete Windows incluye ambas aplicaciones para permitir validación funcional antes de retirar WPF.

Para ejecutar Avalonia:

```powershell
dotnet run --project desktop-avalonia/Transcriptor.Desktop.Avalonia.csproj
```

Para ejecutar la versión WPF heredada:

```powershell
dotnet run --project desktop/Transcriptor.Desktop.csproj
```

La ventana permite configurar la carpeta de grabaciones, elegir micrófono y salida, confirmar autorización, iniciar y detener la captura, transcribir sesiones pendientes, consultar y buscar transcripciones, reproducir pistas, eliminar sesiones y aplicar retención. El plazo se expresa en días: `0` desactiva la retención y el máximo admitido es 3650; el borrado solo ocurre al pulsar **Aplicar retención** y confirmar. Durante una transcripción puedes pulsar **Cancelar transcripción**: la aplicación detiene el árbol de procesos de Whisper, limpia los WAV normalizados y espera a que la operación termine antes de cerrar la ventana. Mientras transcribe, bloquea inicio de grabación, migración, eliminación, retención y cambios de carpeta para evitar interferencias. Al cerrar la ventana, cancela y espera tanto la transcripción como el cierre de ambas pistas antes de salir.

Las preferencias de la aplicación se guardan en:

```text
%LOCALAPPDATA%\Transcriptor\settings.json
```

Este archivo contiene rutas, idioma, dispositivos y preferencias de retención; no contiene claves criptográficas.

## Captura por consola

```powershell
dotnet run --project rec/Rec.csproj -- --list
dotnet run --project rec/Rec.csproj -- --ref "FAE soporte"
```

La CLI comparte la captura WASAPI y el cifrado con WPF. Antes de iniciar solicita confirmación y aplica la ACL NTFS a la carpeta de sesión.

Argumentos disponibles:

- `--ref`: referencia que se agrega al nombre de la carpeta.
- `--mic`: fragmento del nombre del micrófono.
- `--sys`: fragmento del nombre de la salida de audio.
- `--out`: carpeta raíz alternativa.
- `--list`: muestra los dispositivos disponibles.
- `--help`: muestra la ayuda.

Escribe `S` para confirmar y pulsa Enter para detener. La CLI captura; la transcripción se inicia desde WPF.

## Flujo de transcripción

El botón **Transcribir pendientes** procesa las sesiones que contienen las dos pistas y no tienen un conjunto completo y autenticado de resultados cifrados. Las sesiones con resultados parciales, archivos en claro o contenedores dañados se vuelven a generar desde el audio original:

1. Valida la ruta y omite directorios que sean enlaces o puntos de reanálisis.
2. Aplica ACL a la sesión antes de crear archivos de trabajo.
3. Descifra las pistas `.trn` a WAV temporales autenticados; también admite pistas WAV heredadas.
4. Normaliza cada pista a 16 kHz, mono y PCM de 16 bits mediante NAudio/Media Foundation.
5. Ejecuta `whisper-cli` localmente, sin shell.
6. Combina los SRT de ambas pistas y genera SRT/TXT.
7. Cifra cada archivo de transcripción, lo descifra para comparar SHA-256 byte a byte y solo entonces elimina su original. El TXT cifrado se publica al final como marcador de finalización.

Cada sesión usa un bloqueo exclusivo de sistema operativo, almacenado junto a la carpeta de sesión, para impedir que dos instancias transcriban o publiquen derivados simultáneamente. La migración y la eliminación adquieren el mismo bloqueo, evitando competir con una transcripción; el archivo de bloqueo queda fuera de la carpeta que se elimina y se libera al cerrar el handle, incluso si el proceso termina inesperadamente. Whisper se ejecuta con drenaje concurrente de stdout/stderr y captura limitada de logs, timeout y cancelación del árbol de procesos. Si una ejecución se interrumpe, la siguiente vuelve a generar los artefactos desde las pistas originales. Una sesión solo se omite cuando están presentes los cuatro resultados cifrados, no quedan resultados en claro y cada contenedor supera la autenticación completa de su versión. Los SRT se limitan a 64 MiB por pista, 100 000 segmentos y 32 768 caracteres por segmento para acotar entradas hostiles. Si un resultado está dañado, se regenera.

La transcripción no se ejecuta automáticamente al grabar. Si una sesión falla, el motor informa el error y continúa revisando las demás sesiones; al final comunica los fallos.

## Archivos de una sesión

Una sesión nueva tiene esta forma:

```text
Grabaciones/
└── AAAA-MM-DD/
    └── HHMMSS_referencia/
        ├── yo.trn
        ├── cliente.trn
        ├── yo.srt.trn
        ├── cliente.srt.trn
        ├── transcripcion.srt.trn
        └── transcripcion.txt.trn
```

Las pistas de audio son la fuente de verdad; las transcripciones son derivados regenerables. Las sesiones antiguas pueden conservar WAV y SRT en claro hasta que el usuario inicie la migración.

## Cifrado, recuperación y límites

- Las escrituras nuevas usan el formato v2: AES-256-GCM por bloques de 64 KiB, clave aleatoria independiente protegida por DPAPI `CurrentUser` y un cierre autenticado que vincula el número de bloques y la longitud total.
- El lector mantiene compatibilidad con archivos v1. En v1, el terminador no autentica la cantidad total de bloques, por lo que los archivos heredados conservan esa limitación frente al truncamiento exacto de bloques finales. No se reescriben automáticamente.
- La recuperación depende del perfil de Windows que creó la clave. Perder el perfil, sus credenciales o el acceso a DPAPI puede hacer que los datos sean irrecuperables.
- Las carpetas de sesión reciben ACL NTFS para el usuario actual, SYSTEM y administradores locales. Esto limita el acceso de otras cuentas ordinarias, pero no protege frente al usuario autorizado, malware ejecutado bajo esa identidad, administradores, SYSTEM ni acceso físico comprometido.
- La transcripción y la reproducción crean temporales en claro dentro de la carpeta protegida. Se eliminan al terminar cuando es posible; una interrupción o un error del sistema puede dejarlos atrás.
- El borrado normal no es borrado seguro ni forense.
- Los nombres de archivos, fechas, estructura de carpetas y otros metadatos no están cifrados.

Consulta [SECURITY.md](SECURITY.md) para el modelo de amenazas y los detalles del formato.

## Migrar sesiones heredadas

La migración es voluntaria y se inicia por sesión desde la biblioteca de WPF:

1. Selecciona la sesión y confirma **Migrar a cifrado**.
2. La aplicación crea los contenedores cifrados y los descifra para verificar su autenticidad y contenido frente a los originales.
3. Los originales compatibles solo se eliminan cuando todos los archivos reconocidos presentes en la sesión tienen una copia cifrada verificada.

La operación puede reanudarse: los contenedores válidos existentes se verifican y se continúa con los archivos pendientes. Si falla una verificación, los originales no se eliminan. El audio compatible es WAV PCM/IEEE-float; otros formatos permanecen intactos. La eliminación final no es borrado forense.

## Compilar y probar

```powershell
dotnet build rec/Rec.csproj --configuration Release
dotnet build transcribe/Transcriptor.Transcription.csproj --configuration Release
dotnet build setup/Transcriptor.Setup.csproj --configuration Release
dotnet build desktop/Transcriptor.Desktop.csproj --configuration Release
dotnet test transcribe/tests/Transcriptor.Transcription.Tests.csproj --configuration Release
```

GitHub Actions ejecuta estos builds y las pruebas en Windows con .NET 10 para cada push a `main` y cada pull request dirigido a `main`. Las pruebas adversariales incluyen un normalizador y un ejecutor Whisper simulados para provocar fallos de cada pista, salidas ausentes, cancelaciones, reintentos, concurrencia, límites de SRT y limpieza de temporales. También se prueba la coordinación concurrente del cierre de ambas pistas sin depender de hardware. CI no sustituye las pruebas funcionales en un equipo Windows con dispositivos WASAPI y Whisper configurados.

## Variables de entorno

| Variable | Uso | Valor predeterminado |
|---|---|---|
| `TRANSCRIPTOR_DIR` | Raíz de grabaciones usada por la CLI | `%USERPROFILE%\Transcriptor\Grabaciones` |
| `WHISPER_BIN` | Ruta de `whisper-cli.exe` | `%USERPROFILE%\Transcriptor\whisper\whisper-cli.exe` |
| `WHISPER_MODEL` | Ruta del modelo GGML | `%USERPROFILE%\Transcriptor\models\ggml-small.bin` |
| `WHISPER_LANG` | Idioma de transcripción | `es` |


## Licencia

Transcriptor se distribuye bajo la licencia **Apache License 2.0**. Consulta el archivo [LICENSE](LICENSE) para conocer los términos completos.

Los avisos de componentes de terceros están en [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) y también se incluyen en los paquetes oficiales. Los paquetes Windows son autocontenidos e incluyen componentes del runtime de .NET 10; revisa las condiciones y avisos que acompañan a esa distribución. Whisper.cpp y sus modelos se obtienen por separado y no se redistribuyen en los ZIP oficiales.

Cada ZIP también contiene `dependency-inventory/` con informes JSON de paquetes NuGet directos y transitivos por proyecto. Estos informes ayudan a revisar versiones, pero no son un SBOM completo ni sustituyen los avisos/licencias de cada componente.
