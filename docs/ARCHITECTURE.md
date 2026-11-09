# Arquitectura

## Visión general

Transcriptor es una aplicación Windows local. La captura, el cifrado, la biblioteca y la transcripción se ejecutan en el equipo del usuario. No hay base de datos ni servicio remoto para procesar audio. Whisper.cpp se ejecuta como proceso nativo local.

Los proyectos están implementados en C# y .NET 10. `Transcriptor.slnx` agrupa los nueve proyectos bajo `src`, `tests` y `tools` (con las cabezas de escritorio en `tools/heads`). `global.json` fija el SDK .NET 10.0.114 con avance dentro de la banda de características. La interfaz de escritorio está implementada con Avalonia 12; la captura también está disponible por consola.

## Proyectos

La base se divide en una capa portable (sin dependencias de Windows) y una capa Windows:

| Proyecto | Tipo | Responsabilidad |
|---|---|---|
| `transcription/` | Biblioteca portable (net10.0) | Contenedor cifrado `.trn` (formato exacto v1/v2), pistas de audio cifradas (sin NAudio), operaciones de archivos autenticadas, motor Whisper, puertos de captura y monitoreo de niveles, normalización, combinación SRT, leases e integridad de sesiones |
| `ui/` | Biblioteca portable (net10.0) | Ventana Avalonia y servicios de la aplicación: `UiServices` (puertos inyectados), `AppSettings`/`SettingsStore`, `ISessionStore`/`RecordingPrivacy`, `RecordingDirectoryFactory`, `IDefaultPaths`/`PlatformDefaultPaths` |
| `platform-windows/` | Biblioteca Windows (net10.0-windows) | DPAPI (`DpapiSecretProtector`), ACL NTFS (`WindowsSessionDirectorySecurity`), captura WASAPI (`DualRecorder`, `DualCaptureStopGate`), monitor WASAPI de solo lectura, reproducción y normalización NAudio/Media Foundation, migración de sesiones |
| `desktop-avalonia/` | Aplicación Windows | Cabeza de producción: composition root manual que registra DPAPI, ACLs, WASAPI y migración en `UiServices` |
| `desktop-dev/` | Aplicación portable | Cabeza INSEGURO de desarrollo: fakes rotulados (`InsecureDevSecretProtector` sin cifrado real ni captura) y biblioteca de ejemplo en `/tmp/TranscriptorDev`; permite ejecutar la UI en WSLg/Linux |
| `rec/` | Ejecutable Windows | CLI de captura que consume `platform-windows` |
| `setup/` | Ejecutable | Descarga del modelo GGML y configuración de variables de entorno de Whisper |
| `tests/Transcriptor.Tests/` | xUnit portable | Suite portable en Linux: cifrado (con protector reversible de pruebas), motor, configuración, puertos de aplicación |
| `tests/Transcriptor.Tests.Windows/` | xUnit Windows | Suite exclusiva Windows: DPAPI real, ACL NTFS, migración, árbol de procesos, gate de captura |

### Resolución del protector de secretos

El contenedor portable ya no asume DPAPI por defecto. Cada cabeza de aplicación (composition root) registra su `ISecretProtector` en el arranque con `EncryptedFileContainer.ConfigureDefaultSecretProtector`: DPAPI en Windows, protector INSEGURO rotulado en desarrollo y protector reversible de pruebas en la suite portable. El formato de bytes de `.trn` es idéntico en todas las plataformas y está cubierto por pruebas de ida y vuelta portables.

La suite portable usa un protector reversible que sepulta la independencia del formato `.trn` respecto de DPAPI: las mismas pruebas de v1, cierre autenticado y detección de manipulación se ejecutan en Linux.

## Desarrollo sin Windows (WSLg)

En WSLg/Linux se puede compilar todo lo portable, ejecutar la suite portable y arrancar la cabeza de desarrollo:

```bash
dotnet build Transcriptor.slnx -c Release
dotnet test Transcriptor.slnx -c Release
dotnet run --project desktop-dev/Transcriptor.Dev.csproj -- --smoke-test   # headless, para CI
dotnet run --project desktop-dev/Transcriptor.Dev.csproj                    # ventana en WSLg
dotnet watch --project desktop-dev/Transcriptor.Dev.csproj
```

La cabeza de desarrollo muestra el banner «MODO DESARROLLO — sin cifrado real ni captura de audio» y nunca debe distribuirse: el guard del workflow de release falla si detecta sus ensamblados o símbolos en el paquete.

## Modelo de sesión

La estructura es:

```text
%USERPROFILE%/Transcriptor/Grabaciones/AAAA-MM-DD/HHMMSS_referencia/
  yo.trn
  cliente.trn
  yo.srt.trn
  cliente.srt.trn
  transcripcion.srt.trn
  transcripcion.txt.trn
```

- `yo.trn`: micrófono.
- `cliente.trn`: loopback de la salida del sistema.
- `yo.srt.trn` y `cliente.srt.trn`: subtítulos de cada pista.
- `transcripcion.srt.trn` y `transcripcion.txt.trn`: resultado combinado.

Las sesiones antiguas pueden contener WAV, SRT y TXT en claro. La aplicación permite convertirlas manualmente y conserva los originales hasta verificar copias cifradas completas.

## Captura

1. La aplicación y la CLI exigen confirmación de autorización antes de capturar.
2. Se crea la carpeta de sesión y se aplica una ACL NTFS protegida para el usuario actual, SYSTEM y administradores locales. Se rechazan rutas con enlaces o puntos de reanálisis.
3. `RecordingSession` selecciona dispositivos WASAPI y delega en `DualRecorder`.
4. `DualRecorder` crea un `EncryptedAudioTrackWriter` por pista y escribe directamente los contenedores cifrados.
5. El micrófono y el loopback se capturan de forma independiente.

## Transcripción

El motor descifra las pistas a WAV temporales autenticados, normaliza con NAudio/Media Foundation y ejecuta Whisper.cpp localmente. Después combina los SRT y publica resultados cifrados solo tras verificar autenticidad y contenido. Los leases exclusivos coordinan reproducción, transcripción, conversión, eliminación y retención. Las operaciones fallidas pueden reintentarse desde las pistas originales.

## Seguridad y privacidad

- AES-256-GCM por bloques y claves protegidas por DPAPI `CurrentUser`.
- ACL NTFS para limitar el acceso a carpetas de sesión.
- Rechazo de rutas con puntos de reanálisis.
- Temporales en claro dentro de carpetas protegidas y limpieza al terminar.
- La transcripción automática al detener está desactivada por defecto y, si se activa, solo procesa sesiones completas.
- El borrado ordinario no equivale a borrado seguro.

## Interfaz

La interfaz sigue Adobe Spectrum 2 en tema oscuro, sin selector de tema. El cromo respeta el orden de `docs/especificacion/04-ux-ui-spectrum.md`: barra de título propia, Tabs (Estudio, Biblioteca, Ajustes, Registro), subencabezado con la ruta de la sesión actual y la Status light de cifrado, el aviso descartable de modo desarrollo cuando el cifrado real no está disponible, la zona de trabajo y el pie de estado de solo texto.

Estudio es una columna con tres tarjetas —estado y transporte, captura dual y sesión— dentro de una zona desplazable, de modo que a 1000×680 se conserve el acceso a todos los controles (RNUI-07). Biblioteca es una pestaña: la lista de sesiones ocupa `4/12` del ancho y puede ocultarse con la acción «Ocultar sesiones», cuyo estado se guarda en los ajustes; el detalle y el visor ocupan el resto. Ajustes y Registro son formularios y listas de una columna.

`ui/Theming/Tokens.axaml` y `ui/Theming/Icons.axaml` se generan desde `@adobe/spectrum-tokens` y los paquetes de iconos oficiales con `dotnet run --project tools/Transcriptor.Tools -- tokens` y `-- icons`; contienen solo el tema oscuro: colores semánticos, escala tipográfica, espaciados, radios, alturas de componente y geometrías vectoriales. `ui/Theming/ControlThemes.axaml` define los `ControlTheme` de los componentes `Transcriptor.UI.Spectrum` (Button, Action button, Checkbox, Switch, Radio group, Text field, Search field, Picker, Slider, Tabs, Badge, Status light, Divider, Tooltip, Progress bar, Progress circle, Alert dialog, Inline alert, Toast y List view) con sus estados default, hover, down, focus-visible, disabled, selected y error; `ui/Theming/Components.axaml` reúne estilos locales. `ui/Theming/SpectrumTheme.axaml` fusiona tokens, iconos y temas de control y se incluye una sola vez en `Application.Resources` de cada cabeza de escritorio. Las fuentes son Source Sans 3 para la interfaz y Source Code Pro para rutas y valores, con sus avisos OFL en `docs/THIRD-PARTY-NOTICES.md`. Las vistas no llevan colores, tamaños ni fuentes literales (RNUI-05). Las fuentes son Source Sans 3 para la interfaz y Source Code Pro para rutas y valores, con sus avisos OFL en `docs/THIRD-PARTY-NOTICES.md`. Las vistas no llevan colores, tamaños ni fuentes literales (RNUI-05). `ui/Controls/WaveformView.cs` dibuja los niveles reales muestreados por el tick de UI y presenta barras estáticas cuando no hay captura activa; es una de las excepciones documentadas por no tener equivalente oficial en Spectrum. La composición visual no añade operaciones de audio ni modifica el formato de sesión.

El estado de cada requisito RUI y RNUI, su verificación y las excepciones justificadas están en `docs/especificacion/trazabilidad.md`.

Las capturas headless con datos ficticios de desarrollo se escriben en `docs/ui-reference/`; solo se versiona la imagen del README y el resto se genera en local:

```bash
dotnet run --project desktop-dev/Transcriptor.Dev.csproj -c Release -- --capture-ui-reference
```

### Decoración y arrastre de ventana

Cada cabeza decide la decoración mediante `IPlatformCapabilities.PrefersNativeWindowChrome`, no con directivas de compilación en la vista. La cabeza de Windows usa `false` y por tanto `WindowDecorations.BorderOnly` con `ExtendClientAreaToDecorationsHint`: la barra de título propia de Spectrum (icono, título y botones de minimizar y cerrar con iconos vectoriales) gestiona el arrastre desde `TitleBar_PointerPressed` y los roles de decoración, mientras `CanResize` conserva el redimensionado por bordes; `CanMaximize` es `False`, así que no hay doble clic de maximizado. La cabeza de desarrollo prefiere `WindowDecorations.Full` para conservar título, resize y controles nativos en Linux/WSLg; en ese modo los botones propios quedan ocultos. Cualquier cierre, incluido Alt+F4 y el botón nativo, pasa por `Closing` y la confirmación segura del ViewModel.

### Accesibilidad, atajos y verificación manual

Los controles siguen el orden de tabulación visual; los botones de icono tienen nombres accesibles y tooltips, y los controles principales muestran un borde de foco. Atajos:

| Atajo | Acción |
|---|---|
| Ctrl+R | Iniciar grabación (el consentimiento obligatorio sigue siendo necesario) |
| Ctrl+P | Pausar/reanudar grabación; en Biblioteca, pausar/reanudar reproducción |
| Espacio | Pausar/reanudar reproducción en Biblioteca |
| Ctrl+F | Abrir Biblioteca y enfocar su búsqueda |
| F5 | Actualizar la biblioteca |
| Ctrl+1/2/3/4 | Ir a Estudio/Biblioteca/Ajustes/Registro |
| ← → | Mover la selección entre las pestañas del Tabs principal |
| Inicio / Fin | Primera y última pestaña |
| Escape | Descartar el aviso de modo desarrollo |

Checklist manual en WSLg: arrastrar la ventana desde espacios de la cabecera; doble clic para maximizar/restaurar; resize por bordes; navegar y operar con Tab/Enter/Espacio; probar todos los atajos; recorrer las pestañas con las flechas y seleccionar con Ctrl+1/2/3/4; recorrer grabación, reproducción, transcripción y exportación con fakes; comprobar el desplazamiento de la zona de captura y los paneles a 1000×680. La captura y reproducción reales no están disponibles en la cabeza de desarrollo.

Checklist manual en Windows tras una release: resize, snap, maximize, tema y cierre seguro; monitoreo de niveles sin crear carpeta ni archivos; captura de MIC y loopback con consentimiento; comprobar pistas cifradas, reproducción y limpieza de temporales; validar DPAPI, ACL y migración de una sesión antigua.

Se omite drag-and-drop: no existe un flujo de importación que cree sesiones cifradas con confirmación e integridad, por lo que aceptar archivos externos sería inseguro. Se omite el icono de bandeja para no añadir otra dependencia sin confirmar que haga falta.

No se integran DevTools: el paquete `Avalonia.Diagnostics` no publica una versión 12.1.0 en NuGet (la versión más cercana disponible es 11.3.22), que no se debe mezclar con Avalonia 12.1.0.

En Ajustes, la carpeta de grabaciones se valida con un archivo vacío de prueba con nombre aleatorio, abierto con `CreateNew` y borrado al cerrar; no se crean carpetas por defecto. «Probar Whisper» ejecuta el binario configurado directamente, sin shell, con `--help` y límite de 15 segundos. Solo confirma que el ejecutable responde y que el `.bin` existe; deliberadamente no carga el modelo ni procesa audio.

La retención se aplica únicamente desde Ajustes. Antes de borrar, se cuenta usando el mismo criterio de fecha de última escritura de las pistas que usa la política existente y se presenta ese número en la confirmación; tras confirmar, la operación de borrado reutiliza `ISessionStore.ApplyRetention` y sus leases y validaciones actuales. El detalle de biblioteca solo permite eliminar una sesión individual con su confirmación.

La reproducción de MIC y SISTEMA individual está disponible cuando el host lo soporta. La mezcla sincronizada y la velocidad variable permanecen deshabilitadas mediante `IPlatformCapabilities`: el reproductor Windows actual abre una pista y no aporta un pipeline de mezcla ni un control de tasa validado. No se simulan esas funciones ni se duplican decodificadores/leases; habilitarlas requiere diseñar y verificar el pipeline en Windows antes de declararlas compatibles.

La lista de biblioteca muestra tamaños leídos del sistema de archivos, pero no duración de audio: el contenedor `.trn` no expone metadatos de duración y obtener un valor exacto exigiría descifrar cada pista durante el escaneo, lo que no se considera un metadato barato. La duración real se muestra al iniciar reproducción, bajo el lease y con limpieza del temporal. Tampoco se ofrece renombrar sesiones: su carpeta es la identidad usada por biblioteca, operaciones y leases, y el puerto de sesiones no define un cambio de ruta seguro.

Al arrancar se buscan temporales huérfanos exclusivamente en sesiones reconocidas de la carpeta de grabaciones configurada. `EncryptedAudioTrack.DecryptToWaveFile` crea `.{guid:N}.audio.tmp` y `.{guid:N}.playback.wav` junto a la pista; la limpieza exige esos patrones exactos, omite enlaces/directorios y adquiere el lease de la sesión antes de borrar. Las sesiones ocupadas se dejan intactas.

«Probar niveles» usa un puerto distinto de `IAudioCaptureFactory`: la implementación WASAPI solo lee buffers en memoria y la cabeza de desarrollo usa señales sintéticas. No crea una carpeta, escritores `.trn` ni archivos WAV. Se detiene y libera los dispositivos al iniciar una grabación, cambiar de vista o cerrar la ventana.

## CI y distribución

`dotnet.yml` ejecuta dos jobs: uno en Linux que compila los proyectos portables, corre la suite portable y hace el smoke test headless de la cabeza de desarrollo; y otro en Windows que compila todo y corre ambas suites (portable y Windows-only). `release-windows.yml` se ejecuta con `workflow_dispatch` o etiquetas `v*`; compila los proyectos Windows, ejecuta ambas suites, realiza el smoke test de escritorio y genera el ZIP único `Transcriptor-win-x64.zip` con la aplicación autocontenida, setup, CLI, documentación, licencia, avisos e inventarios NuGet. Antes de publicar, una guardia verifica que el ZIP no contenga ensamblados ni símbolos del head INSEGURO de desarrollo. No se ejecuta en pull requests porque esa validación ya corresponde a `dotnet.yml`.
