# Transcriptor

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

## Aplicación de escritorio

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

Cada sesión usa un bloqueo exclusivo de sistema operativo para impedir que dos instancias transcriban o publiquen derivados simultáneamente; el bloqueo se libera al cerrar el handle, incluso si el proceso termina inesperadamente. Whisper se ejecuta con drenaje concurrente de stdout/stderr y captura limitada de logs, timeout y cancelación del árbol de procesos. Si una ejecución se interrumpe, la siguiente vuelve a generar los artefactos desde las pistas originales. Una sesión solo se omite cuando están presentes los cuatro resultados cifrados, no quedan resultados en claro y cada contenedor supera la autenticación completa de su versión. Si un resultado está dañado, se regenera.

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

GitHub Actions ejecuta estos builds y las pruebas en Windows con .NET 10 para cada push a `main` y cada pull request dirigido a `main`. CI no sustituye las pruebas funcionales en un equipo Windows con dispositivos WASAPI y Whisper configurados.

## Variables de entorno

| Variable | Uso | Valor predeterminado |
|---|---|---|
| `TRANSCRIPTOR_DIR` | Raíz de grabaciones usada por la CLI | `%USERPROFILE%\Transcriptor\Grabaciones` |
| `WHISPER_BIN` | Ruta de `whisper-cli.exe` | `%USERPROFILE%\Transcriptor\whisper\whisper-cli.exe` |
| `WHISPER_MODEL` | Ruta del modelo GGML | `%USERPROFILE%\Transcriptor\models\ggml-small.bin` |
| `WHISPER_LANG` | Idioma de transcripción | `es` |
