# Transcriptor: requisitos

Fecha: 2026-11-05

Qué debe hacer la aplicación (RA, RNA) y qué debe cumplir su interfaz (RUI, RNUI). Fuentes: `README.md`, `docs/SECURITY.md`, `docs/HARDWARE-VALIDATION.md`, `AppSettings.cs`, los `ViewModels` y la solución `Transcriptor.slnx` de este repositorio.

## Alcance, actores y módulos

**Producto.** Aplicación de escritorio para Windows 11, local y privada, que graba en dos pistas independientes el micrófono y el audio del sistema (loopback WASAPI), las guarda cifradas y las transcribe sin salir del equipo con Whisper.cpp. La interfaz es solo en tema oscuro.

| Actor | Rol |
|---|---|
| Persona usuaria | Graba, transcribe, consulta, exporta y administra las sesiones |
| Personas grabadas | Participantes de la llamada; su autorización es condición para grabar |
| Windows | Proporciona WASAPI, DPAPI (clave por perfil), ACL NTFS y Media Foundation |
| Whisper.cpp | Proceso externo (`whisper-cli`) que genera los subtítulos; no se redistribuye |
| Quien mantiene el proyecto | Compila, publica el paquete y valida con hardware real |

| Módulo | Responsabilidad |
|---|---|
| `transcription` | Dominio y puertos: cifrado, motor de transcripción, SRT, integridad de grabaciones, lease de sesión |
| `platform-windows` | Adaptadores: captura WASAPI, normalizador, DPAPI, ACL, reproductor, migración |
| `ui` | ViewModels, vistas y configuración |
| `desktop-avalonia` | Host de producción |
| `desktop-dev` | Host de desarrollo con simuladores y capturas de referencia |
| `rec` | Grabador por consola |
| `setup` | Configurador de Whisper y modelo |

**Fuera de alcance:** envío de audio a la nube, clave maestra de recuperación, borrado seguro forense, modo portable, tema claro y producción en sistemas distintos de Windows 11.

## Requisitos de la aplicación

| ID | Área | Requisito |
|---|---|---|
| RA-01 | Captura | Capturar a la vez micrófono y audio del sistema en dos pistas independientes (`yo` y `cliente`) |
| RA-02 | Captura | Elegir dispositivos de entrada y salida, y mostrar medidores de nivel, pico y RMS |
| RA-03 | Captura | Iniciar, pausar, reanudar y detener; impedir cambiar dispositivos o iniciar otra captura durante una grabación |
| RA-04 | Captura | Exigir confirmación de autorización de las personas involucradas antes de grabar |
| RA-05 | Captura | Probar niveles sin crear sesión ni guardar audio |
| RA-06 | Captura | Avisar de clipping y de silencio prolongado (más de 10 s) por pista, y de poco espacio libre (umbral configurable, 1 GB por defecto) |
| RA-07 | Captura | Ante un fallo de captura o escritura, detener ambas pistas, marcar la sesión como incompleta (`.recording-incomplete`) y no transcribirla automáticamente |
| RA-08 | Almacenamiento | Escribir las pistas directamente en contenedores cifrados `yo.trn` y `cliente.trn`, en `AAAA-MM-DD/HHMMSS_referencia` |
| RA-09 | Seguridad | Cifrar con AES-256-GCM por bloques de 64 KiB (formato v2) con clave por archivo protegida por DPAPI `CurrentUser` |
| RA-10 | Seguridad | Aplicar ACL NTFS a la carpeta de sesión y rechazar directorios que sean enlaces o puntos de reanálisis |
| RA-11 | Compatibilidad | Leer contenedores v1 y sesiones heredadas en claro hasta que la persona decida migrarlas |
| RA-12 | Transcripción | Transcribir localmente: descifrar a WAV temporal, normalizar a 16 kHz mono 16 bits, ejecutar Whisper sin shell, combinar SRT de ambas pistas y cifrar los resultados |
| RA-13 | Transcripción | Verificar autenticidad y SHA-256 de los resultados cifrados antes de eliminar archivos en claro |
| RA-14 | Transcripción | Usar un lease exclusivo por sesión para evitar transcripción, migración o eliminación concurrentes, incluso desde otra instancia |
| RA-15 | Transcripción | Permitir cancelar; terminar el proceso de Whisper y limpiar los temporales |
| RA-16 | Transcripción | Permitir reintentar desde las pistas originales sin reutilizar resultados parciales |
| RA-17 | Transcripción | No iniciar sola al detener una grabación, salvo que el ajuste correspondiente esté activo; procesar pendientes en lote bajo petición |
| RA-18 | Transcripción | Idioma configurable (español por defecto; autodetección, inglés, francés, alemán, portugués o código personalizado) |
| RA-19 | Biblioteca | Listar sesiones con estado (completa o incompleta, pendiente, transcrita, cifrada o en claro), con filtro, orden y búsqueda por fecha o referencia |
| RA-20 | Biblioteca | Mostrar la transcripción por segmentos con tiempos, búsqueda interna y resaltado del segmento en reproducción |
| RA-21 | Biblioteca | Reproducir pista del micrófono, del sistema o mezcla sincronizada, con velocidad variable donde la plataforma lo permita |
| RA-22 | Biblioteca | Exportar a SRT, TXT y MD, y copiar al portapapeles |
| RA-23 | Biblioteca | Eliminar sesiones solo de forma explícita y tras confirmación |
| RA-24 | Retención | Admitir de 0 a 3650 días (0 desactiva); aplicar solo al pulsar «Aplicar retención» y confirmar |
| RA-25 | Migración | Migrar por sesión a cifrado de forma reanudable; borrar originales solo cuando todos los archivos reconocidos tengan copia cifrada verificada |
| RA-26 | Ajustes | Persistir en `%LOCALAPPDATA%\Transcriptor\settings.json`: carpeta de grabaciones, ejecutable y modelo de Whisper, idioma, retención, espacio mínimo, auto-transcripción, dispositivos y panel de biblioteca |
| RA-27 | Ajustes | «Probar Whisper» ejecuta `whisper-cli --help` con límite de 15 s sin cargar el modelo |
| RA-28 | Operación | Bloquear grabación y operaciones destructivas mientras se transcribe |
| RA-29 | Operación | Al cerrar, esperar el cierre coordinado de la captura y cancelar la transcripción en curso antes de salir |
| RA-30 | Operación | Mantener un registro de sesión acotado (2000 líneas) que se pueda copiar y vaciar |
| RA-31 | Instalación | El configurador descarga el modelo `ggml-small.bin` desde una revisión fijada, verifica tamaño y SHA-256 y guarda `WHISPER_BIN` y `WHISPER_MODEL` |
| RA-32 | Distribución | Publicar un ZIP autocontenido con el runtime de .NET 10 y su SHA-256; sin instalador y sin modo portable |
| RA-33 | Consola | Grabar por consola con `rec` (`--list`, `--ref`, `--mic`, `--sys`, `--out`) con la misma captura, cifrado y ACL |

## Requisitos no funcionales de la aplicación

| ID | Atributo | Requisito |
|---|---|---|
| RNA-01 | Privacidad | Ningún audio ni transcripción sale del equipo |
| RNA-02 | Integridad | Detectar modificación, reordenamiento, sustitución y truncamiento de bloques en contenedores v2 |
| RNA-03 | Recuperación | Toda operación interrumpida es reintentable; los temporales en claro quedan en carpeta protegida y se limpian al reintentar o cancelar |
| RNA-04 | Capacidad de respuesta | La interfaz no se bloquea durante captura, transcripción, reproducción ni migración |
| RNA-05 | Plataforma | Windows 11, .NET 10, Avalonia 12; el paquete incluye el runtime |
| RNA-06 | Verificabilidad | Suite portable y suite Windows en CI, más validación manual con hardware según `HARDWARE-VALIDATION.md` |
| RNA-07 | Límites declarados | La recuperación depende del perfil de Windows; no hay clave maestra; el borrado no es forense; nombres, fechas y estructura de carpetas no se cifran |
| RNA-08 | Licencias | Apache-2.0; Whisper.cpp y sus modelos se obtienen por separado; avisos de terceros actualizados, incluida la licencia OFL de las fuentes |

## Requisitos de interfaz (Adobe Spectrum)

| ID | Requisito | Prioridad |
|---|---|---|
| RUI-01 | Tokens generados desde los tokens oficiales de Spectrum 2, solo tema oscuro | Alta |
| RUI-02 | Eliminar el selector de tema, `ThemeService`, el ajuste `ThemeMode` y el diccionario claro; la app arranca siempre en oscuro y ignora `ThemeMode` en `settings.json` existentes | Alta |
| RUI-03 | Componentes Spectrum dentro de `Transcriptor.UI`: Button, Action button, Action group, Checkbox, Switch, Radio group, Text field, Search field, Picker, Slider, Tabs, Badge, Status light, Divider, Tooltip, Progress bar, Progress circle, Alert dialog, Inline alert, Toast, List view | Alta |
| RUI-04 | Navegación principal con Tabs (Estudio, Biblioteca, Ajustes, Registro) y atajos documentados | Alta |
| RUI-05 | Barra de título propia con icono, título y controles de ventana con iconos; se conservan arrastre y redimensionado | Media |
| RUI-06 | Transporte con Action buttons e iconos; el botón de grabar usa la variante roja solo mientras graba | Alta |
| RUI-07 | Estados de sesión con Status light y Badge con texto, nunca solo con color | Alta |
| RUI-08 | Todas las acciones destructivas (eliminar, retención, migrar, cerrar con captura en curso, cancelar transcripción) usan un único Alert dialog | Alta |
| RUI-09 | Reproducción con Slider de posición, Action group de pista (Mic, Sistema, Mezcla) y Picker de velocidad | Media |
| RUI-10 | Validación en línea en Ajustes (rutas, modelo, retención 0–3650) con ayuda y error por campo | Media |
| RUI-11 | Todo el copy en sentence case, centralizado en recursos | Media |
| RUI-12 | Densidad regular por defecto con modo compacto opcional en Ajustes | Baja |

## Requisitos no funcionales de interfaz

| ID | Atributo | Criterio medible |
|---|---|---|
| RNUI-01 | Contraste | Texto ≥ 4,5:1 y componentes/gráficos ≥ 3:1 en el tema oscuro (adaptar `ThemeContrastTests`) |
| RNUI-02 | Teclado | Todo flujo de los casos de uso completable sin ratón; orden de tabulación lógico |
| RNUI-03 | Lector de pantalla | Todo control interactivo con `AutomationProperties.Name`; estados anunciados (grabando, pausado, transcribiendo) |
| RNUI-04 | Rendimiento | Cambio de pestaña sin parpadeo; la forma de onda mantiene su tasa actual de refresco |
| RNUI-05 | Mantenibilidad | Ningún color, tamaño o fuente literal en `MainWindow.axaml`, verificado por prueba automática |
| RNUI-06 | Regresión visual | Capturas headless de referencia por pantalla y tamaño, regeneradas con `desktop-dev` y comparadas en CI |
| RNUI-07 | Ventana mínima | Funcional a 1000×680 sin cortar controles críticos; la zona de captura conserva desplazamiento |
| RNUI-08 | Privacidad | El rediseño no altera el cifrado ni el consentimiento obligatorio |
