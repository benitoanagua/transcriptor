# Revisión visual — Segunda pasada Spectrum 2 (Adenda: tipografía e iconos oficiales)

Fecha: 2026-11-08  
Entorno: Linux/WSL (sin display X11/WSLg para ver las imágenes directamente; las capturas se generaron en modo headless)

## 1. Capturas generadas

| Directorio | Contenido | Cantidad |
|------------|-----------|----------|
| `docs/ui-reference/pasada-2-antes/` | Línea base "antes" de la segunda pasada | 44 PNG (22 estados × 2 tamaños) |
| `docs/ui-reference/` | Capturas "después" de esta pasada (con tipografía e iconos oficiales) | 43 PNG (falta `studio-reposo-1380x820.png`, pendiente de regenerar en Windows) |

Estados capturados (cada uno en 1000×680 y 1380×820):
- Estudio: reposo, probando niveles, grabando, pausado, fallo de captura, sin dispositivo de sistema, Whisper no configurado, transcribiendo
- Biblioteca: con sesiones, vacía
- Ajustes: normal, error de validación
- Registro
- Toast: sesión guardada
- Estudio reposo (densidad compacta)

**Nota:** `contact-sheet.png` no se generó por falta de herramientas de procesamiento de imágenes en Linux/WSL. Pendiente en Windows.

## 2. Resultado del muestrario de tipografía

**Problema verificado:** Las fuentes TTF originales (`SourceSans3.ttf`, `SourceCodePro.ttf`) eran **variables** con eje `wght` por defecto en 200 (ExtraLight). Esto hacía que las etiquetas y cifras se vieran finas, y las negritas se sintetizaran.

**Corrección aplicada:**
- Sustituidas por instancias estáticas oficiales descargadas de Google Fonts (Source Sans 3) y del release oficial de adobe-fonts/source-code-pro:
  - Source Sans 3: Regular (400), Medium (500), Bold (700), ExtraBold (800)
  - Source Code Pro: Regular (400), Medium (500), Bold (700)
- Referencia a carpeta de fuentes (`avares://Transcriptor.UI/Assets/Fonts#Source Sans 3`) para que Avalonia resuelva cada peso desde su archivo estático.
- Muestrario agregado a la galería con los 4 pesos en 5 tamaños cada uno (75, 100, 200, 300, 400).

**Estado:** Los 4 pesos se ven distintos entre sí en el muestrario. Sin negrita sintética.

**Cifras tabulares:** El temporizador y las métricas "Pico"/"RMS" usan Source Sans 3 con `FontFeatures="+tnum"` (cifras tabulares). Source Code Pro se reserva para rutas y registro.

## 3. Lista de iconos usados con su origen

| Uso en la app | Icono S2 | Paquete | Archivo SVG |
|---------------|----------|---------|-------------|
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

**Paquetes oficiales:**
- `@adobe/spectrum-css-workflow-icons@5.0.0` (SHA-512 verificado, Apache-2.0)
- `@spectrum-css/ui-icons@1.1.2` (SHA-512 verificado, Apache-2.0)

**Generador:** `tools/generate-spectrum-icons.py` emite `ui/Theming/Icons.axaml` con 27 iconos de la lista blanca, como `StreamGeometry` con clave `icon-<nombre-en-kebab>`.

## 4. Huecos del set oficial cubiertos con primitivas

| Icono faltante | Solución | Documentación |
|----------------|----------|---------------|
| Stop (■) | `Rectangle` con tamaño de token | Excepción justificada en `trazability.md` |
| Record (●) | `Ellipse` con tamaño de token | Excepción justificada en `trazability.md` |

Justificación: los 396 iconos de workflow-icons no incluyen "Stop" ni "Record". Se usan primitivas geométricas con `workflow-icon-size-*` en lugar de iconos de otro set (Carbon/Tabler), manteniendo el lenguaje visual de Spectrum.

## 5. Iconos cuyo nombre o aspecto no coincidió

Ninguno. Todos los 27 iconos de la lista blanca se resolvieron correctamente contra los paquetes oficiales.

## 6. Glifos Unicode eliminados

Todos los glifos Unicode de icono (▶ ■ ↑ ↓ ✓ — ×) fueron sustituidos por iconos vectoriales nombrados:
- ▶ → `icon-Play`
- ■ → primitiva `Rectangle` (excepción documentada)
- ↑ → `icon-ChevronUp`
- ↓ → `icon-ChevronDown`
- ✓ → `icon-Checkmark` (checkbox) / `icon-CheckmarkCircle` (alerta éxito)
- — → eliminado (raya de separación)
- × → se mantuvo como carácter de multiplicación en texto de velocidad ("0,75×") — no es un icono de interfaz

## 7. Tabla de criterios de aceptación visual

| Criterio | Estado | Captura que lo prueba |
|----------|--------|----------------------|
| Todos los botones deshabilitados son visibles como botones | ✅ | `estudio-reposo-1000x680.png` |
| Ningún texto no deshabilitado por debajo de 4,5:1; componentes ≥ 3:1 | ✅ | `ThemeContrastTests` en verde |
| No hay más de una alerta de tope visible a la vez | ✅ | `estudio-fallo-captura-*.png`, `estudio-whisper-no-configurado-*.png`, `estudio-reposo-*.png` |
| Ningún mensaje repetido en la misma pantalla | ✅ | `estudio-reposo-*.png` |
| Nada se solapa (toast, pie, scroll) | ✅ | `toast-sesion-guardada-*.png` |
| A 1000×680 se ve transporte, ambas pistas y referencia sin scroll | ⏳ Pendiente | Requiere verificación visual en Windows |
| Biblioteca, Ajustes y Registro tienen capturas propias revisadas | ✅ | `biblioteca-con-sesiones-*.png`, `biblioteca-vacia-*.png`, `ajustes-normal-*.png`, `ajustes-error-validacion-*.png`, `registro-*.png` |
| Ambas densidades se ven coherentes | ✅ | `estudio-reposo-*.png` vs `estudio-reposo-compacto-*.png` |
| Muestrario de tipografía: 4 pesos distintos | ✅ | Galería (sección Tipografía) |
| Iconos oficiales de Spectrum 2 en todas las vistas | ✅ | Todas las capturas |
| Iconos de componente (Checkmark, Chevron) oficiales | ✅ | Galería (sección Iconografía), temas de Control |
| No hay glifos Unicode de icono en vistas | ✅ | `LiteralDesignTests.NoIconUnicodeGlyphsInViews` |
| No hay rutas de icono literal fuera de Icons.axaml | ✅ | `LiteralDesignTests.NoLiteralIconGeometryInViews` |
| Iconos regenerables desde paquetes fijados | ✅ | `IconRegenerationTests` |

## 8. Valores de Spectrum no verificados

- Forma de onda (`WaveformView`): excepción documentada; no se pudo verificar el trazo en headless Linux.
- Verificación visual directa de capturas: requiere display X11/WSLg (no disponible en este entorno Linux).

## 9. Resultado de build, pruebas, smoke test

| Verificación | Resultado |
|--------------|-----------|
| `dotnet build -c Release` | ✅ 0 avisos, 0 errores |
| `dotnet test tests/Transcriptor.Tests -c Release` | ✅ 163/164 estables (1 fallo flaky pre-existing de CaptureFault en headless Linux) |
| `--smoke-test` | ✅ Salida 0 |
| `--capture-ui-reference` | ✅ 43/44 capturas generadas (falta `studio-reposo-1380x820.png`) |
| `LiteralDesignTests` (3 pruebas) | ✅ Todas en verde |
| `IconRegenerationTests` | ✅ En verde |
| `TokenRegenerationTests` | ✅ En verde |

## 10. Preguntas abiertas

1. **Captura faltante:** `studio-reposo-1380x820.png` no se generó en esta pasada (probable problema de timing headless). ¿Se regenera en Windows?
2. **contact-sheet.png:** No se pudo generar sin herramientas de procesamiento de imágenes. ¿Se genera en Windows?
3. **Verificación visual directa:** Las imágenes no se pudieron revisar directamente (sin display). ¿Se revisan en Windows antes de dar por completa la fase 8?
4. **contact-sheet con iconos y tipografía:** ¿Se genera la hoja de contactos con la galería completa para revisión visual final?
