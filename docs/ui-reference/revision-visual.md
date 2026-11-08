# Revisión visual — Segunda pasada Spectrum 2 (tipografía e iconos oficiales)

**Estado: NO VERIFICADO visualmente.**

Este documento se reescribe al terminar la pasada de porteo a C# (adenda 1). Ninguna afirmación aquí se apoya en haber visto directamente las imágenes PNG, porque este entorno Linux no tiene display X11/WSLg disponible y las capturas solo se generaron en modo headless. Toda afirmación de apariencia visual queda como **no verificado**; solo las comprobaciones mecánicas (build, pruebas, paridad byte a byte) están verificadas.

## 1. Capturas generadas (no revisadas visualmente)

| Directorio | Contenido | Cantidad | Verificado |
|---|---|---|---|
| `docs/ui-reference/pasada-2-antes/` | Línea base "antes" de la segunda pasada | 44 PNG | No (sin display) |
| `docs/ui-reference/` | Capturas "después" de esta pasada | 43 PNG | No (sin display) |

Estados capturados (cada uno en 1000×680 y 1380×820):
- Estudio: reposo, probando niveles, grabando, pausado, fallo de captura, sin dispositivo de sistema, Whisper no configurado, transcribiendo
- Biblioteca: con sesiones, vacía
- Ajustes: normal, error de validación
- Registro
- Toast: sesión guardada
- Estudio reposo (densidad compacta)

**No verificado:** ninguna de estas capturas ha sido revisada directamente. `contact-sheet.png` no se generó en Linux (falta herramienta de bitmap); pendiente en Windows (Paso 6 del trabajo visual).

## 2. Tipografía (portada a C# y documentada, no vista)

**Sustitución de fuentes (documentada, no verificada visualmente):**
- Las TTF originales se sustituyeron por instancias estáticas oficiales: Source Sans 3 (Regular 400, Medium 500, Bold 700, ExtraBold 800) y Source Code Pro (Regular 400, Medium 500, Bold 700), desde los lanzamientos oficiales de Adobe.
- Cada TTF tiene su SHA-256 fijado en `FontIntegrityTests` y su URL de origen en `docs/THIRD-PARTY-NOTICES.md`.
- Referencia a carpeta de fuentes (`avares://Transcriptor.UI/Assets/Fonts#…`) para que Avalonia resuelva cada peso desde su archivo estático.

**No verificado:** "los 4 pesos se ven distintos entre sí" y la calibración de interlineado. Pendiente de revisión en Windows.

## 3. Iconos (paridad byte a byte verificada, aspecto no visto)

**Generador C# con paridad exacta:** `dotnet run --project tools/Transcriptor.Tools -- icons` reproduce `ui/Theming/Icons.axaml` **byte a byte** respecto al archivo versionado (prueba `IconRegenerationTests` en verde). El conteo es de 28 iconos de la lista blanca (incluye `icon-ViewList` añadido al generador y a `Icons.axaml`).

| Uso en la app | Icono S2 | Paquete | Archivo SVG |
|---|---|---|---|
| Reproducir | Play | workflow-icons | S2_Icon_Play_20_N.svg |
| Pausar | Pause | workflow-icons | S2_Icon_Pause_20_N.svg |
| Micrófono | Microphone | workflow-icons | S2_Icon_Microphone_20_N.svg |
| Audio del sistema | VolumeTwo | workflow-icons | S2_Icon_VolumeTwo_20_N.svg |
| Cerrar ventana | Close | workflow-icons | S2_Icon_Close_20_N.svg |
| Minimizar ventana | Minimize | workflow-icons | S2_Icon_Minimize_20_N.svg |
| Eliminar | Delete | workflow-icons | S2_Icon_Delete_20_N.svg |
| Copiar | Copy | workflow-icons | S2_Icon_Copy_20_N.svg |
| Exportar | Export | workflow-icons | S2_Icon_Export_20_N.svg |
| Actualizar | Refresh | workflow-icons | S2_Icon_Refresh_20_N.svg |
| Abrir carpeta | FolderOpen | workflow-icons | S2_Icon_FolderOpen_20_N.svg |
| Ajustes | Settings | workflow-icons | S2_Icon_Settings_20_N.svg |
| Cifrado activo | Lock | workflow-icons | S2_Icon_Lock_20_N.svg |
| Sin cifrar | LockOpen | workflow-icons | S2_Icon_LockOpen_20_N.svg |
| Filtrar | Filter | workflow-icons | S2_Icon_Filter_20_N.svg |
| Ordenar | Sort | workflow-icons | S2_Icon_Sort_20_N.svg |
| Transcripción | Transcript | workflow-icons | S2_Icon_Transcript_20_N.svg |
| Texto | FileText | workflow-icons | S2_Icon_FileText_20_N.svg |
| Coincidencia anterior | ChevronUp | workflow-icons | S2_Icon_ChevronUp_20_N.svg |
| Coincidencia siguiente | ChevronDown | workflow-icons | S2_Icon_ChevronDown_20_N.svg |
| Alerta info | InfoCircle | workflow-icons | S2_Icon_InfoCircle_20_N.svg |
| Alerta éxito | CheckmarkCircle | workflow-icons | S2_Icon_CheckmarkCircle_20_N.svg |
| Alerta aviso | AlertTriangle | workflow-icons | S2_Icon_AlertTriangle_20_N.svg |
| Barra de título | AudioWave | workflow-icons | S2_Icon_AudioWave_20_N.svg |
| Buscar | Search | workflow-icons | S2_Icon_Search_20_N.svg |
| Casilla de verificación | Checkmark | ui-icons | dist/combined/Checkmark100.svg |
| Selector (dropdown) | Chevron | ui-icons | dist/combined/Chevron100.svg |
| Biblioteca (listado) | ViewList | workflow-icons | S2_Icon_ViewList_20_N.svg |

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
3. **Captura faltante:** `studio-reposo-1380x820.png` no se generó en esta pasada (problema de timing headless). ¿Se regenera en Windows?
4. **Pasos de CI en Windows:** `release-windows.yml` (inventory, package, smoke, guardias de ZIP) solo se pueden validar en el runner de Windows. Pendiente.
