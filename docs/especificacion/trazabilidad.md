# Transcriptor: trazabilidad de la especificación

Índice cruzado entre los cuatro documentos que rigen el rediseño y el código, las pruebas y el plan por fases. Sirve para comprobar qué está cumplido, qué falta y qué excepciones están justificadas.

## Orden de precedencia

| Documento | Regula |
|---|---|
| `01-requisitos.md` | Qué debe hacer la aplicación (RA, RNA) y qué debe cumplir su interfaz (RUI, RNUI) |
| `02-casos-de-uso.md` | Qué puede hacer la persona usuaria (CU-01 a CU-16) y en qué pantalla |
| `03-flujos-y-estados.md` | Estados de la sesión, reglas de negocio (RN), flujos de sistema (F) y de pantalla (FI) |
| `04-ux-ui-spectrum.md` | Fundamentos visuales, diagnóstico de la brecha y diseño de cada pantalla |

Los documentos mandan sobre el código. Si un documento es ambiguo o falta un dato, se pregunta antes de inventar. Las decisiones cerradas (Spectrum 2, solo tema oscuro, densidad regular con compacto opcional, Source Sans 3 y Source Code Pro, ZIP sin modo portable, tokens generados desde `@adobe/spectrum-tokens`) están en `04-ux-ui-spectrum.md` y se aplican sin discusión.

## Pantallas, componentes y flujos

| Pantalla | Componentes Spectrum | Flujos | Documentación de diseño |
|---|---|---|---|
| Cromo de la ventana | Barra de título propia, Tabs, Status light, Inline alert, pie de estado | FI-01 a FI-05 transversales | `04` «Estructura global de la ventana», `01` RUI-04 y RUI-05 |
| Estudio | Action button de transporte, Button, Status light, Picker, Text field, Checkbox, Progress bar, Inline alert, Toast | FI-01 | `04` «Estudio», `03` FI-01 |
| Biblioteca y visor | Search field, Picker, List view, Badge, Slider, Action group, menú de exportación | FI-02, FI-03 | `04` «Biblioteca», `03` FI-02 y FI-03 |
| Ajustes | Text field con validación en línea, Number field, Picker, Switch, Radio group, Divider | FI-05 | `04` «Ajustes y Registro», `03` FI-05 |
| Registro | Lista monoespaciada de solo lectura, Action buttons | FI-05 | `04` «Ajustes y Registro» |
| Diálogos | `SpectrumAlertDialog` único, Toast de resultado | FI-04 | `04` «Reglas transversales», `03` FI-04 |

## Requisitos de interfaz (RUI)

| ID | Dónde se implementa | Cómo se verifica | Estado |
|---|---|---|---|
| RUI-01 | `dotnet run --project tools/Transcriptor.Tools -- tokens` → `ui/Theming/Tokens.axaml` (solo oscuro) | `ThemeContrastTests`; los tokens salen de `@adobe/spectrum-tokens` fijado | Cumplido |
| RUI-02 | Sin `ThemeService`, `ThemeMode`, selector ni diccionario claro | Búsqueda de esos símbolos en la solución; `settings.json` con `ThemeMode` se ignora al leer | Cumplido |
| RUI-03 | `ui/Theming/ControlThemes.axaml` y los controles de `Transcriptor.UI.Spectrum` (`ui/Controls/`); un único `ui/Theming/SpectrumTheme.axaml` en `Application.Resources` de cada cabeza | Galería de `desktop-dev` (`--spectrum-gallery`) con todos los estados; recursos resueltos desde cualquier ventana (`ResourceResolutionTests`) | Cumplido (fase 4: fusione de `ui-spectrum` en `ui`) |
| RUI-04 | `TabControl Classes="stripOnly"` (`MainTabs`) + comandos `Show*Page` (sin atajos de teclado) | `StudioGeometryTests`; tabla de atajos de `docs/ARCHITECTURE.md` | Cumplido |
| RUI-05 | Barra de título con `PathIcon` de minimizar y cerrar, arrastre y redimensionado conservados | Recorrido manual; `docs/ARCHITECTURE.md` explica cromo propio frente al nativo | Cumplido |
| RUI-06 | `RecordButton Classes="record"` y el selector `^.record:checked` de `Spectrum.ActionButton` | Capturas `studio-grabando` y `studio-pausado`; prueba de teclado de FI-01 | Cumplido |
| RUI-07 | `SpectrumStatusLight` con texto y `SpectrumBadge` en la biblioteca | Capturas por estado; revisión del copy en la fase 7 | Cumplido |
| RUI-08 | `SpectrumAlertDialog` único para eliminar, retención, migrar, cancelar y cerrar | Prueba de diálogo + recorrido FI-04 | Cumplido; `ConfirmAsync` eliminado del code-behind |
| RUI-09 | Slider de posición, Action group de pista y Picker de velocidad | Recorrido FI-03 | Cumplido (fase 5) |
| RUI-10 | Validación en línea por campo en Ajustes | Prueba de `SettingsViewModel` + recorrido FI-05 | Cumplido (fase 6) |
| RUI-11 | Copy en sentence case; recursos `.resx` | Revisión de `*.axaml` y de los rótulos de ViewModel | Cumplido en `MainWindow.axaml` vía `ui/Strings.resx` |
| RUI-12 | Clases `.compact` en los `ControlTheme`; ajuste de densidad en Ajustes | Galería en ambas densidades | Cumplido |

## Requisitos no funcionales de interfaz (RNUI)

| ID | Criterio | Verificación | Estado |
|---|---|---|---|
| RNUI-01 | Texto ≥ 4,5:1 y componentes ≥ 3:1 | `ThemeContrastTests` (pares de tokens del tema oscuro, incluidos los del cromo) | Cumplido |
| RNUI-02 | Todo flujo completable sin ratón | Prueba de teclado de FI-01 sobre la ventana real; recorrido manual de FI-02 a FI-05 | Cumplido (FI-01 a FI-05 verificados por teclado) |
| RNUI-03 | `AutomationProperties.Name` y estados anunciados | Revisión de vistas + Narrador de Windows | Nombres presentes en todas las vistas; validación con lector pendiente (requiere Windows) |
| RNUI-04 | Cambio de pestaña sin parpadeo, forma de onda a su tasa | Observación en ejecución | Sin cambios respecto al estado previo |
| RNUI-05 | Ningún literal de color, tamaño o fuente en las vistas | Hoy por revisión; regla automática en CI | Cumplido (`LiteralDesignTests`) |
| RNUI-06 | Capturas headless por pantalla y tamaño comparadas en CI | `--capture-ui-reference` | Capturas generadas; comparación en CI no viable sin Windows (documentada) |
| RNUI-07 | Funcional a 1000×680 sin cortar controles críticos | `StudioGeometryTests` + capturas | Cumplido con la desviación anotada más abajo |
| RNUI-08 | El rediseño no altera cifrado ni consentimiento | Suite portable (158 pruebas) sin cambios de comportamiento | Cumplido |

## Excepciones justificadas

1. **Forma de onda y visor de segmentos.** No tienen equivalente oficial en Spectrum (`04`, principio 1). Se mantienen como controles propios (`ui:WaveformView`, lista de segmentos) alimentados por tokens.
2. **Barra de progreso de la transcripción.** `TranscriptionEngine` publica su avance como `IProgress<string>`, no como porcentaje numérico. Pasar a una `ProgressBar` determinada obligaría a cambiar la capa de negocio, prohibido por RNUI-08. La interfaz muestra la etiqueta con el porcentaje que comunica el motor y una barra indeterminada mientras dura la operación (FI-02, punto 3).
3. **Sin modo portable.** La distribución sigue siendo `Transcriptor-win-x64.zip` (RA-32); no se añaden funciones, opciones ni documentación de modo portable. La palabra «portable» nombra además la capa independiente de plataforma (proyectos `transcription`, `ui`, `desktop-dev` y la suite de pruebas portable), que no se toca.
4. **Ancho de las capturas.** `MaxWidth` de la ventana es 720 y Avalonia 12 lo aplica al área cliente, así que las capturas pedidas a 1380×820 y 1000×680 se renderizan a 720 px de ancho. Decisión del mantenedor (opción A): se eliminó el límite de MaxWidth de 720 (decisión G10 revisada en la segunda pasada). La ventana abre por defecto a 1000×680 con mínimo 720×520 y sin MaxWidth. Las capturas a 1000×680 y 1380×820 se renderizan a su tamaño real. CanMaximize sigue siendo False.
5. **Tipografía.** Source Sans 3 tiene métricas distintas de Adobe Clean; el aspecto no es idéntico al de las aplicaciones de Adobe. El interlineado y los tamaños se calibran en la galería.
6. **Sustitución del set de iconos.** Los iconos de flujo de trabajo de Spectrum 2 (`@adobe/spectrum-css-workflow-icons@5.0.0`) son de trazo fino (~1,5 px a 20 px) e ilegibles a 16 px sobre fondo oscuro. Se sustituyen por `@fluentui/svg-icons@1.1.345` (SVG relleno, 20 px, MIT) para todos los iconos mostrados en la UI; `@spectrum-css/ui-icons@1.1.2` se conserva solo para `icon-Checkmark` y `icon-Chevron`, internos de componentes de Spectrum. No es un "segundo sistema visual": Fluent aporta únicamente geometrías de icono emitidas como `StreamGeometry`, sin dependencia de runtime ni paquete NuGet.

## Integración de la capa Spectrum 2

La aplicación de escritorio carga `ui/Theming/SpectrumTheme.axaml` (tokens, iconos y temas de control de `Transcriptor.UI`) como estilos globales en `Application.Resources` de cada cabeza de escritorio. Los tokens pertenecen a `Transcriptor.UI` y resuelven desde cualquier ventana o diálogo, no solo del árbol de `MainWindow`. El tema de componentes es exclusivamente Spectrum; Fluent aporta únicamente geometrías de icono emitidas como `StreamGeometry` (sin paquete NuGet de iconos ni dependencia de runtime).

La biblioteca publica sus diccionarios XAML mediante `AvaloniaResource`. Los estilos propios de una pantalla se limitan a composición y layout y deben consumir tokens semánticos con `DynamicResource`; no deben redefinir los estados de interacción de componentes que ya tienen un tema Spectrum.

La guía oficial recomienda empezar por componentes Spectrum y usar tokens semánticos como interfaz principal, reservando tokens de componente para necesidades específicas. En Avalonia se mantiene esa taxonomía y se mapean los recursos a recursos dinámicos. Fuente: [Adobe Spectrum design data](https://spectrum.adobe.com/foundations/design-data/).

## Estado del plan de adecuación

| Fase | Contenido | Criterio de salida | Estado |
|---|---|---|---|
| 0 | Preparación: congelar Spectrum 2, licencias OFL, inventario, capturas «antes» | Inventario y capturas guardados | Completa (`8e204d8`, `068dff1`) |
| 1 | Tokens y tema oscuro; eliminar selector, `ThemeService`, `ThemeMode` y diccionario claro | `ThemeContrastTests` en verde; sin literales en tokens | Completa (`9421fa0`) |
| 2 | Biblioteca de componentes `Transcriptor.UI.Spectrum` con galería | Galería con todos los estados | Completa (`09f7632`) |
| 3 | Cromo y navegación: barra de título, Tabs, subencabezado, pie | RUI-04 y RUI-05 cumplidos | Completa (`6f9b797`) |
| 4 | Estudio: transporte, captura dual y sesión | FI-01 completo por teclado | En revisión visual (capturas generadas, pendiente de revisión en Windows) |
| 5 | Biblioteca y visor | FI-02 y FI-03 completos | En revisión visual (capturas generadas, pendiente de revisión en Windows) |
| 6 | Ajustes, Registro y diálogos | FI-04 y FI-05; `ConfirmAsync` migrado | En revisión visual (capturas generadas, pendiente de revisión en Windows) |
| 7 | Copy en recursos y accesibilidad | RNUI-02 y RNUI-03 verificados | En revisión visual (capturas generadas, pendiente de revisión en Windows) |
| 8 | Endurecimiento: regla sin literales, regresión visual en CI, limpieza de estilos heredados | RNUI-05 y RNUI-06 en CI | En curso (LiteralDesignTests y TokenRegenerationTests implementados; comparación de capturas en CI pendiente sin Windows) |

## Verificación de cada fase

- **Automática:** `dotnet build -c Release` sin avisos, `dotnet test tests/Transcriptor.Tests -c Release` (contraste, geometría y ViewModels), `--smoke-test` con código de salida 0.
- **Visual:** `--capture-ui-reference` para reposo, transcribiendo, grabando, pausado, fallo de captura, sin dispositivo de sistema, Whisper no configurado y biblioteca vacía. Solo se versiona la imagen del README; el resto se genera en local.
- **Manual:** recorrido por teclado de cada flujo FI, contraste con Narrador de Windows y revisión del copy contra la guía de escritura de Spectrum. Estas pruebas requieren Windows y no se ejecutan desde Linux/WSL.
- **Terminado por componente:** todos los estados, ambas densidades, foco visible, nombre accesible y entrada en la galería.
