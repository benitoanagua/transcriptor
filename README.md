# Transcriptor

Aplicación local para capturar llamadas en dos pistas independientes —micrófono y audio del sistema— y transcribirlas con Whisper.cpp. La lógica de producto está implementada en **C# y .NET 10**; la interfaz de escritorio usa WPF. No requiere base de datos ni servicios en la nube.

## Capacidades

- Captura simultánea de micrófono y audio del sistema mediante WASAPI/NAudio.
- Selección de dispositivos de entrada y salida y medidores de nivel peak.
- Grabación directa en contenedores cifrados: `yo.trn` y `cliente.trn`. No se crea un WAV en claro durante la captura.
- Cifrado AES-256-GCM por bloques; cada clave de datos se protege con Windows DPAPI en ámbito `CurrentUser`.
- Transcripción local con Whisper.cpp, visor y búsqueda de SRT, y reproducción de cada pista.
- Biblioteca local con eliminación de sesiones y retención manual.
- Migración voluntaria de sesiones heredadas en claro a cifrado, con verificación antes de eliminar originales.

> Antes de grabar, confirma que todas las personas involucradas han sido informadas y que cuentas con autorización. Define también las reglas de retención y borrado aplicables.

## Requisitos

- Windows 11.
- SDK de .NET 10.
- Un ejecutable oficial de Whisper.cpp para Windows (`whisper-cli.exe`).
- Modelo GGML compatible con la versión de Whisper.cpp.

## Configuración inicial

Obtén `whisper-cli.exe` desde una distribución oficial de [whisper.cpp](https://github.com/ggml-org/whisper.cpp/releases) y ejecuta el configurador indicando su ruta:

```powershell
dotnet run --project setup/Transcriptor.Setup.csproj -- "C:\Herramientas\whisper\whisper-cli.exe"
```

El configurador descarga `ggml-small.bin` y guarda `WHISPER_BIN` y `WHISPER_MODEL` en el entorno del usuario. Abre una terminal nueva después de configurarlo. La aplicación WPF también permite seleccionar manualmente el ejecutable y el modelo.

## Aplicación de escritorio

```powershell
dotnet build desktop/Transcriptor.Desktop.csproj --configuration Release
dotnet run --project desktop/Transcriptor.Desktop.csproj
```

La ventana permite configurar la carpeta de grabaciones, elegir micrófono y salida, confirmar autorización, iniciar/detener la captura, transcribir pendientes, consultar y buscar transcripciones, reproducir pistas, eliminar sesiones y aplicar retención.

Las preferencias se guardan en:

```text
%LOCALAPPDATA%\Transcriptor\settings.json
```

El archivo de configuración contiene rutas y preferencias, no claves criptográficas ni credenciales.

## Grabación por CLI

La herramienta de consola comparte la misma captura WASAPI y cifrado que WPF. Aplica ACL NTFS a la carpeta de sesión y pide confirmación antes de comenzar:

```powershell
dotnet build rec/Rec.csproj --configuration Release
dotnet run --project rec/Rec.csproj -- --list
dotnet run --project rec/Rec.csproj -- --ref "FAE soporte"
```

Argumentos:

- `--ref`: referencia que se agrega al nombre de la carpeta.
- `--mic`: fragmento del nombre del micrófono.
- `--sys`: fragmento del nombre de la salida de audio.
- `--out`: carpeta raíz alternativa.
- `--list`: muestra dispositivos disponibles.

Escribe `S` en la confirmación para iniciar; pulsa Enter para detener. La CLI también genera `yo.trn` y `cliente.trn`.

## Transcripción

La transcripción se ejecuta desde el botón **Transcribir pendientes** de WPF. El motor:

1. Busca sesiones completas con ambas pistas.
2. Descifra y autentica cada pista a un WAV temporal dentro de la carpeta de sesión protegida por ACL.
3. Normaliza a 16 kHz mono PCM con NAudio/Media Foundation.
4. Ejecuta Whisper.cpp localmente.
5. Combina los SRT por tiempo y cifra los SRT/TXT resultantes.
6. Elimina los WAV y temporales de trabajo mediante borrado normal de mejor esfuerzo.

La CLI `rec` solo captura; no ejecuta la transcripción.

## Cifrado, temporales y recuperación

- Las pistas de audio `.trn` contienen los parámetros PCM/IEEE-float y las muestras dentro del contenedor autenticado.
- Los SRT y TXT terminados también se guardan como contenedores cifrados `.trn`.
- La reproducción y el visor descifran bajo demanda en temporales dentro de la carpeta de sesión protegida.
- DPAPI `CurrentUser` vincula la recuperación al perfil de Windows que creó las claves. Reinstalar Windows, perder el perfil o cambiar de usuario puede hacer que los datos sean irrecuperables. Mantén copias de seguridad del perfil conforme a tu política.
- Los temporales en claro pueden permanecer si el proceso o el equipo se interrumpen inesperadamente. La aplicación intenta eliminarlos al terminar, pero no ofrece borrado forense.
- Las ACL protegen frente a otras cuentas locales ordinarias; no protegen frente al usuario autorizado, malware ejecutado bajo esa identidad, administradores, SYSTEM ni acceso físico comprometido.
- Los nombres, fechas y estructura de carpetas siguen revelando metadatos.

Consulta [SECURITY.md](SECURITY.md) antes de usar el sistema con información sensible.

## Migrar sesiones heredadas

Las sesiones antiguas pueden contener WAV/SRT en claro. La migración no se ejecuta automáticamente:

1. Selecciona una sesión en la biblioteca.
2. Pulsa **Migrar a cifrado** y confirma.
3. La aplicación crea contenedores temporales, descifra y verifica cada uno contra su original.
4. Solo cuando todos los archivos compatibles de la sesión están verificados elimina los originales.

La operación es reanudable: si se interrumpe, vuelve a ejecutar la migración de esa sesión. Los archivos cifrados válidos se verifican y se continúa. Si una verificación falla, se conservan los originales. La migración de audio admite WAV PCM/IEEE-float; formatos no compatibles se dejan intactos. La eliminación de originales no es borrado seguro.

## Estructura de una sesión nueva

```text
Grabaciones/
└── 2026-11-02/
    └── 144608_FAE-soporte/
        ├── yo.trn
        ├── cliente.trn
        ├── yo.srt.trn
        ├── cliente.srt.trn
        ├── transcripcion.txt.trn
        └── transcripcion.srt.trn
```

Las transcripciones son derivados regenerables; las pistas de audio son la fuente de verdad. Las sesiones heredadas conservan sus nombres originales hasta que el usuario inicia la migración.

## Compilar y probar

```powershell
dotnet build rec/Rec.csproj --configuration Release
dotnet build transcribe/Transcriptor.Transcription.csproj --configuration Release
dotnet build setup/Transcriptor.Setup.csproj --configuration Release
dotnet build desktop/Transcriptor.Desktop.csproj --configuration Release
dotnet test transcribe/tests/Transcriptor.Transcription.Tests.csproj --configuration Release
```

GitHub Actions ejecuta compilación y pruebas en Windows en cada push a `main` y en cada pull request.

## Configuración por entorno

| Variable | Uso | Valor predeterminado |
|---|---|---|
| `TRANSCRIPTOR_DIR` | Carpeta raíz de grabaciones | `%USERPROFILE%\Transcriptor\Grabaciones` |
| `WHISPER_BIN` | Ruta de `whisper-cli.exe` | `%USERPROFILE%\Transcriptor\whisper\whisper-cli.exe` |
| `WHISPER_MODEL` | Ruta al modelo GGML | `%USERPROFILE%\Transcriptor\models\ggml-small.bin` |
| `WHISPER_LANG` | Idioma de transcripción | `es` |
