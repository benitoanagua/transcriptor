# Transcriptor

[![.NET CI](https://github.com/benitoanagua/transcriptor/actions/workflows/dotnet.yml/badge.svg?branch=main)](https://github.com/benitoanagua/transcriptor/actions/workflows/dotnet.yml)
[![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D6)](https://github.com/benitoanagua/transcriptor)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)

**Local-first, privacy-focused call recorder and transcription app for Windows.** Captures microphone and system audio into separate encrypted tracks, then transcribes them locally with Whisper.cpp — no cloud audio upload.

*Aplicación local-first para grabar y transcribir llamadas en Windows. Captura micrófono y audio del sistema en pistas cifradas independientes y genera transcripciones localmente con Whisper.cpp, sin subir el audio a servicios en la nube.*

## Por qué Transcriptor

- **Privacidad por diseño:** pistas de audio y transcripciones cifradas en reposo con AES-256-GCM; las claves están protegidas mediante Windows DPAPI.
- **Dos pistas independientes:** captura simultánea de micrófono y audio del sistema mediante loopback.
- **Transcripción offline:** ejecuta Whisper.cpp localmente en el equipo Windows.
- **Recuperación:** valida resultados cifrados y permite reintentar transcripciones interrumpidas sin aceptar capturas incompletas.
- **Escritorio Windows:** C# / .NET 10, Avalonia 12, WASAPI, NAudio y Media Foundation.

> **Grabación responsable:** informa a todas las personas involucradas y obtén la autorización necesaria antes de grabar. Sigue la legislación aplicable y define una política de retención adecuada.

Transcriptor captura llamadas en dos pistas independientes —micrófono y audio del sistema— y las transcribe con Whisper.cpp. La interfaz y la lógica de producto están implementadas en C#/.NET 10; el procesamiento de audio y las transcripciones funcionan localmente.

## Funciones

- Captura simultánea de micrófono y audio del sistema mediante WASAPI/NAudio.
- Selección de dispositivos y medidores de nivel.
- Escritura directa de pistas en contenedores cifrados: yo.trn y cliente.trn.
- Detención coordinada de ambas pistas ante un fallo de captura/escritura; las sesiones incompletas no se transcriben automáticamente.
- Cifrado AES-256-GCM por bloques y protección de claves con Windows DPAPI (CurrentUser).
- Transcripción local mediante Whisper.cpp, combinación de subtítulos SRT, visor, búsqueda y reproducción.
- Biblioteca local de sesiones, eliminación explícita y aplicación manual de retención.
- Migración voluntaria de sesiones antiguas en claro, con verificación de copias cifradas antes de eliminar originales.
- Ventana principal y Mini-Mode con controles de grabación, biblioteca y reproducción.

> Antes de grabar, informa a todas las personas involucradas y confirma que cuentas con autorización.

## Requisitos

- Windows 11.
- SDK de .NET 10.
- Ejecutable oficial de Whisper.cpp para Windows (whisper-cli.exe).
- Modelo GGML compatible con la versión del ejecutable.

WASAPI, Media Foundation, ACL NTFS y DPAPI hacen que las operaciones de captura, normalización y cifrado dependan de Windows.

## Configurar Whisper

Obtén whisper-cli.exe desde una distribución oficial de [whisper.cpp](https://github.com/ggml-org/whisper.cpp/releases). Ejecuta el configurador con la ruta del ejecutable:

```powershell
dotnet run --project setup/Transcriptor.Setup.csproj -- "C:\Herramientas\whisper\whisper-cli.exe"
```

El configurador descarga ggml-small.bin desde una revisión fijada, verifica el SHA-256 y solo publica la descarga después de validar tamaño y huella. Después guarda WHISPER_BIN y WHISPER_MODEL en las variables de entorno del usuario.

## Versión portable

```powershell
tools\setup\transcriptor-setup.exe --portable "C:\Herramientas\whisper\whisper-cli.exe"
```

El configurador copia whisper-cli.exe y sus DLL a whisper\, descarga y verifica el modelo en models\, crea data\ y escribe portable.flag junto a Transcriptor.Desktop.exe. La configuración y las grabaciones se guardan en data\. Usa --target "carpeta" para indicar otra ubicación. Para volver al modo normal, elimina portable.flag.

Las pistas cifradas con DPAPI solo se pueden abrir con la misma cuenta de Windows en el mismo equipo. La carpeta de grabaciones debe estar en NTFS para aplicar ACL.

## Descargar e instalar

Descarga Transcriptor-win-x64.zip desde [GitHub Releases](https://github.com/benitoanagua/transcriptor/releases), extrae el contenido y ejecuta Transcriptor.Desktop.exe. El paquete incluye el runtime de .NET 10. Verifica el archivo SHA-256 publicado junto al ZIP.

Configura Whisper.cpp y su modelo por separado; no se redistribuyen dentro del paquete.

## Aplicación de escritorio

La aplicación usa Avalonia 12 sobre .NET 10.

Para ejecutar desde el repositorio:

```powershell
dotnet run --project desktop-avalonia/Transcriptor.Desktop.csproj
```

La ventana permite elegir carpeta de grabaciones, dispositivos de entrada/salida y confirmar autorización; iniciar, pausar y detener la captura; consultar y buscar en la biblioteca; reproducir pistas; transcribir sesiones pendientes; y eliminar sesiones o aplicar retención manual. La retención admite de 0 a 3650 días; 0 la desactiva y el borrado solo ocurre tras pulsar Aplicar retención y confirmar.

Durante una transcripción se puede cancelar el proceso. La aplicación bloquea grabación y operaciones destructivas sobre la biblioteca mientras se procesan sesiones; al cerrar, espera el cierre de la captura y la finalización de recursos.

Las preferencias se guardan en %LOCALAPPDATA%\Transcriptor\settings.json. En modo portable se utiliza data\settings.json.

## Captura por consola

```powershell
dotnet run --project rec/Rec.csproj -- --list
dotnet run --project rec/Rec.csproj -- --ref "FAE soporte"
```

La CLI comparte la captura WASAPI y el cifrado con la aplicación. Solicita confirmación y aplica ACL NTFS a la carpeta de sesión. Para iniciar la transcripción se utiliza la aplicación de escritorio.

Argumentos: --ref referencia; --mic fragmento del nombre del micrófono; --sys fragmento de la salida; --out carpeta raíz; --list enumera dispositivos; --help muestra ayuda. Escribe S para confirmar y pulsa Enter para detener.

## Flujo de transcripción

El botón Transcribir pendientes procesa sesiones con ambas pistas que aún no tienen resultados cifrados completos y autenticados:

1. Valida la ruta y rechaza directorios que sean enlaces o puntos de reanálisis.
2. Aplica ACL a la sesión.
3. Descifra las pistas a WAV temporales autenticados; también admite WAV antiguos.
4. Normaliza a 16 kHz, mono y PCM de 16 bits mediante NAudio/Media Foundation.
5. Ejecuta whisper-cli localmente, sin shell.
6. Combina los SRT de ambas pistas y genera SRT/TXT.
7. Cifra cada resultado, verifica autenticidad y SHA-256 frente al original, y elimina el archivo en claro solo después de verificarlo.

Cada sesión utiliza un lease exclusivo para impedir transcripción, migración o eliminación concurrentes. Una operación interrumpida puede reintentarse desde las pistas originales. La transcripción no se inicia automáticamente al terminar una grabación.

## Estructura de una sesión

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

Las pistas son la fuente de verdad; las transcripciones son derivados regenerables. Las sesiones antiguas pueden conservar WAV/SRT en claro hasta que se inicie manualmente la migración.

## Migrar grabaciones antiguas

La migración se inicia por sesión desde la biblioteca:

1. Selecciona la sesión y confirma Migrar a cifrado.
2. La aplicación crea contenedores cifrados y verifica autenticidad y contenido frente a los originales.
3. Los originales se eliminan solo cuando todos los archivos reconocidos presentes tienen una copia cifrada verificada.

La operación puede reanudarse. Si falla una verificación, los originales se conservan. El audio compatible es WAV PCM/IEEE-float; otros formatos permanecen intactos. El borrado no es forense.

## Cifrado y límites

- Formato v2: AES-256-GCM por bloques de 64 KiB, clave aleatoria independiente protegida por DPAPI CurrentUser y cierre autenticado.
- El lector mantiene compatibilidad con archivos v1, que conservan una limitación frente al truncamiento exacto de bloques finales.
- La recuperación depende del perfil de Windows que creó la clave.
- Las ACL limitan el acceso de otras cuentas ordinarias, pero no protegen frente a la identidad autorizada, malware bajo esa identidad, administradores, SYSTEM ni acceso físico comprometido.
- La transcripción y reproducción generan temporales en claro en la carpeta protegida; una interrupción puede dejarlos atrás.
- El borrado ordinario no equivale a borrado seguro ni forense. Los nombres, fechas y estructura de carpetas no están cifrados.

Consulta SECURITY.md para el modelo de amenazas.

## Compilar y probar

```powershell
dotnet build rec/Rec.csproj --configuration Release
dotnet build transcribe/Transcriptor.Transcription.csproj --configuration Release
dotnet build setup/Transcriptor.Setup.csproj --configuration Release
dotnet build desktop-avalonia/Transcriptor.Desktop.csproj --configuration Release
dotnet test transcribe/tests/Transcriptor.Transcription.Tests.csproj --configuration Release
```

GitHub Actions compila los proyectos y ejecuta xUnit en Windows con .NET 10 para pushes a main y pull requests. CI no sustituye la validación funcional con hardware WASAPI y una instalación configurada de Whisper.

## Avisos y licencia

Transcriptor usa Apache License 2.0. Consulta LICENSE y THIRD-PARTY-NOTICES.md. Los paquetes Windows son autocontenidos e incluyen .NET 10. Whisper.cpp y sus modelos se obtienen por separado.

Los ZIP incluyen dependency-inventory/ con inventarios JSON de paquetes NuGet directos y transitivos para la aplicación, grabador y biblioteca de transcripción. No constituyen un SBOM completo.
