# Revisión visual — Segunda pasada Spectrum 2 (tipografía e iconos oficiales)

**Estado: NO VERIFICADO visualmente.**

Este documento se reescribe al terminar la pasada de porteo a C# (adenda 1). Ninguna afirmación aquí se apoya en haber visto directamente las imágenes PNG, porque este entorno Linux no tiene display X11/WSLg disponible y las capturas solo se generaron en modo headless. Toda afirmación de apariencia visual queda como **no verificado**; solo las comprobaciones mecánicas (build, pruebas, paridad byte a byte) están verificadas.

## 1. Capturas generadas (no revisadas visualmente)

| Directorio | Contenido | Cantidad | Verificado |
|---|---|---|---|
| `docs/ui-reference/pasada-2-antes/` | Línea base "antes" de la segunda pasada | 44 PNG | No (sin display) |
| `docs/ui-reference/` | Capturas "después" de esta pasada | 43 PNG | No (sin display) |

**Proceso de generación de capturas:**

Las capturas de referencia de UI se generan automáticamente ejecutando:

```bash
dotnet run --project desktop-dev/Transcriptor.Dev.csproj -c Release -- --capture-ui-reference
```

Este comando:
1. Inicia la aplicación de desarrollo en modo headless
2. Navega a través de cada estado de UI (reposo, grabando, pausado, etc.)
3. Captura pantalla en dos tamaños: 520×780 y 768×780 píxeles
4. Usa renderizado 2× para sobremuestrear trazos finos (/iconos de 1px)
5. Aplica filtro Mitchell para reducir sobremuestreo manteniendo nitidez

**Archivos generados:**
- Cada estado genera 2 imágenes (uno por resolución)
- Formato PNG con compresión al 100%
- Guardados en `docs/ui-reference/` con nombre `studio-{estado}-{tamaño}.png`

**Estados capturados:**
- Estudio: reposo, probando niveles, grabando, pausado, fallo de captura, sin dispositivo de sistema, Whisper no configurado, transcribiendo
- Biblioteca: con sesiones, vacía
- Ajustes: normal, error de validación
- Registro
- Toast: sesión guardada
- Estudio reposo (densidad compacta)

**Notas sobre imágenes:**
- El README usa `studio-estudio-reposo-520x780.png` como imagen destacada
- `studio-reposo-1380x820.png` **no se generó** - el generador crea solo 520×780 y 768×780
- `contact-sheet.png` no se generó en Linux (requiere herramienta bitmap de Windows)

**No verificado:** ninguna de estas capturas ha sido revisada directamente. `contact-sheet.png` no se generó en Linux (falta herramienta de bitmap); pendiente en Windows (Paso 6 del trabajo visual).

## 2. Tipografía (portada a C# y documentada, no vista)

**Sustitución de fuentes (documentada, no verificada visualmente):**
- Actualización: la fuente monoespaciada es ahora JetBrains Mono (Regular, Medium, Bold; v2.304) y se añadió Source Sans 3 SemiBold (600), siguiendo el diseño de Google Stitch. Las TTF originales se sustituyeron por instancias estáticas oficiales: Source Sans 3 (Regular 400, Medium 500, Bold 700, ExtraBold 800) y Source Code Pro (Regular 400, Medium 500, Bold 700), desde los lanzamientos oficiales de Adobe.
- Cada TTF tiene su SHA-256 fijado en `FontIntegrityTests` y su URL de origen en `docs/THIRD-PARTY-NOTICES.md`.
- Referencia a carpeta de fuentes (`avares://Transcriptor.UI/Assets/Fonts#…`) para que Avalonia resuelva cada peso desde su archivo estático.

**No verificado:** "los 4 pesos se ven distintos entre sí" y la calibración de interlineado. Pendiente de revisión en Windows.

## 3. Iconos (paridad byte a byte verificada, aspecto no visto)

**Sustitución del set de iconos:** `@adobe/spectrum-css-workflow-icons@5.0.0` (trazo fino, ilegible a 16 px sobre fondo oscuro) se sustituye por `@fluentui/svg-icons@1.1.345` (SVG relleno, rejilla de 20 px, MIT) para todos los iconos mostrados en la UI. `@spectrum-css/ui-icons@1.1.2` se conserva solo para `icon-Checkmark` y `icon-Chevron` (internos de componentes de Spectrum). Documentado en `docs/THIRD-PARTY-NOTICES.md` y `docs/especificacion/trazabilidad.md` (excepción justificada #6).

**Generador C# con paridad exacta:** `dotnet run --project tools/Transcriptor.Tools -- icons` reproduce `ui/Theming/Icons.axaml` **byte a byte** respecto al archivo versionado (prueba `IconRegenerationTests` en verde). Se confirmó idempotencia regenerando dos veces sin diferencia.

| Uso en la app | Icono Fluent | Archivo SVG | Tamaño |
|---|---|---|---|
| Reproducir | Play | play_20_filled.svg | 20 px |
| Pausar | Pause | pause_20_filled.svg | 20 px |
| Micrófono | Microphone | mic_20_filled.svg | 20 px |
| Audio del sistema | VolumeTwo | speaker_1_20_filled.svg | 20 px |
| Cerrar ventana | Close | dismiss_20_filled.svg | 20 px |
| Minimizar ventana | Minimize | arrow_minimize_20_filled.svg | 20 px |
| Eliminar | Delete | delete_20_filled.svg | 20 px |
| Copiar | Copy | copy_20_filled.svg | 20 px |
| Exportar | Export | send_20_filled.svg | 20 px |
| Actualizar | Refresh | arrow_clockwise_20_filled.svg | 20 px |
| Abrir carpeta | FolderOpen | folder_open_20_filled.svg | 20 px |
| Ajustes | Settings | settings_20_filled.svg | 20 px |
| Cifrado activo | Lock | lock_closed_20_filled.svg | 20 px |
| Sin cifrar | LockOpen | lock_open_20_filled.svg | 20 px |
| Filtrar | Filter | filter_20_filled.svg | 20 px |
| Ordenar | Sort | arrow_sort_down_20_filled.svg | 20 px |
| Transcripción | Transcript | document_20_filled.svg | 20 px |
| Texto | FileText | document_20_filled.svg | 20 px |
| Coincidencia anterior | ChevronUp | chevron_up_20_filled.svg | 20 px |
| Coincidencia siguiente | ChevronDown | chevron_down_20_filled.svg | 20 px |
| Alerta info | InfoCircle | info_20_filled.svg | 20 px |
| Alerta éxito | CheckmarkCircle | checkmark_circle_20_filled.svg | 20 px |
| Alerta aviso | AlertTriangle | warning_20_filled.svg | 20 px |
| Barra de título | AudioWave | mic_20_filled.svg | 22 px (size-200) |
| Buscar | Search | search_20_filled.svg | 20 px |
| Casilla de verificación | Checkmark | ui-icons Checkmark100.svg | 20 px |
| Selector (dropdown) | Chevron | ui-icons Chevron100.svg | 20 px |

**No verificado:** que cada `PathIcon` muestre efectivamente un glifo en pantalla. Pendiente de revisión en Windows (prueba `ResourceResolutionTests` verifica mecánicamente que las claves existan, no que se rendericen).

## 4. Huecos del set oficial cubiertos con primitivas (documentado, no visto)

| Icono faltante | Solución | Documentación |
|---|---|---|
| Stop (■) | `Rectangle` con tamaño de token | Excepción justificada en `trazability.md` |
| Record (●) | `Ellipse` con tamaño de token | Excepción justificada en `trazability.md` |

## 5. Glifos Unicode eliminados (documentado, no visto)

Todos los glifos Unicode de icono (▶ ■ ↑ ↓ ✓ — ×) fueron sustituidos por iconos vectoriales nombrados o primitivas. Verificado mecánicamente por `LiteralDesignTests.NoIconUnicodeGlyphsInViews` y `LiteralDesignTests.NoLiteralIconGeometryInViews` (en verde).

## 6. Tabla de criterios de aceptación visual (estado real)

| Criterio | Estado | Evidencia |
|---|---|---|
| Build Release sin avisos | ✅ | `dotnet build -c Release` 0 avisos |
| Suite portable en verde | ✅ | 169/169 pruebas (menos `StudioGeometryTests`, bloqueado por falta de display) |
| Paridad Tokens.axaml | ✅ | `TokenRegenerationTests` en verde (byte a byte) |
| Paridad Icons.axaml | ✅ | `IconRegenerationTests` en verde (byte a byte) |
| Recursos resueltos | ✅ | `ResourceResolutionTests` en verde |
| Ningún tooling no-.NET versionado | ✅ | `ToolingIsDotNetOnlyTests` en verde |
| Fuentes con SHA-256 fijado | ✅ | `FontIntegrityTests` en verde |
| Iconos visibles en pantalla | **No verificado** | Requiere revisión visual en Windows |
| contact-sheet.png | **Pendiente** | Paso 6, requiere Avalonia bitmap en Windows |

## 7. Valores de Spectrum no verificados

- Forma de onda (`WaveformView`): excepción documentada; no se pudo verificar en headless Linux.
- Verificación visual directa de capturas: requiere display X11/WSLg (no disponible en este entorno Linux).
- Apariencia de cada captura: no verificada (sin display).

## 8. Resultado de build, pruebas y smoke test (verificado en Linux)

| Verificación | Resultado |
|---|---|
| `dotnet build Transcriptor.slnx -c Release` | ✅ 0 avisos, 0 errores |
| `dotnet test tests/Transcriptor.Tests -c Release` | ✅ 169/169 (salvo `StudioGeometryTests`, bloqueado por falta de display en este entorno; preexistente en HEAD) |
| `--smoke-test` (cabeza de desarrollo) | ✅ Salida 0 |
| `TokenRegenerationTests` / `IconRegenerationTests` | ✅ Paridad byte a byte |
| `ThemeContrastTests`, `LiteralDesignTests`, `ResourceResolutionTests`, `ToolingIsDotNetOnlyTests`, `FontIntegrityTests`, `PortableProjectsStayOsNeutralTests` | ✅ En verde |

## 9. Preguntas abiertas

1. **contact-sheet.png:** no se generó en Linux. ¿Se genera en Windows con Avalonia bitmap (Paso 6 del trabajo visual)?
2. **Verificación visual directa:** las capturas no se revisaron directamente. ¿Se revisan en Windows antes de dar por completa la fase visual?
3. **Captura 1380×820:** el generador solo crea 520×780 y 768×780. Las dimensiones 1380×820 mencionadas en el documentamiento original no están implementadas. El README fue actualizado para usar 520×780.
4. **Pasos de CI en Windows:** `release-windows.yml` (inventory, package, smoke, guardias de ZIP) solo se pueden validar en el runner de Windows. Pendiente.
