# Transcriptor: UX/UI con Adobe Spectrum

Fecha: 2026-11-05

Cómo debe verse y comportarse la interfaz para seguir Adobe Spectrum de forma estricta: fundamentos visuales, brecha con el estado actual y diseño de cada pantalla. Los requisitos RUI y RNUI están en `01-requisitos.md`, los flujos FI en `03-flujos-y-estados.md` y el orden de trabajo, con su estado, en `trazabilidad.md`.

## Fundamentos visuales

Spectrum no tiene implementación oficial para Avalonia, así que «estricto» significa reconstruir los componentes como `ControlTheme` propios que reproduzcan las especificaciones, alimentados por los tokens oficiales.

| Decisión | Valor |
|---|---|
| Versión de referencia | Spectrum 2 |
| Tema | Solo oscuro; sin selector y sin tema claro |
| Tipografía | Source Sans 3 (OFL; pesos 400/500/600/700) para la interfaz y JetBrains Mono (OFL) solo para cronómetro, rutas, métricas y registro, como en el diseño de Google Stitch |
| Densidad | Regular por defecto, con modo compacto opcional (tamaños S) |
| Tokens | Generados desde `@adobe/spectrum-tokens` a `Tokens.axaml`; nunca escritos a mano |
| Estética propia | Sin biseles, pantallas «LED», gradientes, sombras personalizadas ni mayúsculas forzadas |

## Principios de conformidad

1. **Un componente de la app es un componente Spectrum.** Si no existe equivalente oficial se documenta como excepción justificada; la forma de onda y el visor de transcripción son los candidatos.
2. **Cero valores literales de diseño en las vistas.** Colores, tamaños, radios, espaciados y tipografías vienen de tokens.
3. **Estados completos.** Cada componente implementa default, hover, down, focus-visible, disabled, selected y, cuando aplica, error y solo lectura.
4. **Accesibilidad al nivel de Spectrum.** Contraste WCAG 2.1 AA, operación completa por teclado, nombres accesibles y objetivos de tamaño suficiente.
5. **Copy según la guía de Spectrum.** Sentence case, verbos claros en botones, errores que explican qué hacer.
6. **Sin decoración propia.** Lo que no está en Spectrum no se añade.

## Diagnóstico del estado actual

Estado tras las fases 0-7. Lo que queda pendiente se documenta como excepción justificada o se deja para la decisión del mantenedor (G10).

| Área | Estado tras fases 0-7 | Brecha restante | Acción / excepción |
|---|---|---|---|
| Tema | Oscuro único; tokens desde `@adobe/spectrum-tokens@15.6.0` (SHA-512 verificado); sin `FluentTheme`, sin selector y sin `ThemeService` | Ninguna | Ninguna |
| Tipografía | Source Sans 3 / JetBrains Mono; cuerpo `font-size-100` (14 px) con su `line-height-font-size-*` | Métricas distintas a Adobe Clean Spectrum | Calibrar interlineado en galería (excepción G5) |
| Etiquetas | Sentence case en `MainWindow.axaml` vía `ui/Strings.resx` | Copy de ViewModels y botones sueltos no migrado a `.resx` | Mover a `.resx` (fase futura) |
| Botones | `Spectrum.Button`: Accent/Primary/Secondary/Negative × fill/outline, tallas S-M-L-XL, `corner-radius-100` (4 px) | Ninguna | Ninguna |
| Barra de título | `PathIcon` de minimizar y cerrar, sin glifos de texto | Ninguna | Ninguna |
| Navegación | `Spectrum.Tabs`/`Spectrum.Tab` con indicador, foco y teclado propios | Ninguna | Ninguna |
| Insignias | `SpectrumBadge` con variantes semánticas (positiva, negativa, aviso, acento) | Ninguna | Ninguna |
| Glifos en texto | Reemplazados por `PathIcon` nombrados (`IconPlay`, `IconStop`, `IconChevronUp`, `IconChevronDown`) | Ninguna | Ninguna |
| Iconos | `@material-symbols/svg-400@0.48.3` (Material Symbols, peso 400; relleno para transporte y estado) sustituye a Fluent, que a su vez sustituyó a `@adobe/spectrum-css-workflow-icons@5.0.0`. Lienzo fijo de 960 (`icon-viewbox-size`) en el tema de `PathIcon` | El diseño de Google Stitch usa Material Symbols; Fluent (20 px, trazo fino) desentonaba con Source Sans 3 SemiBold |  Tamaño 20 px para pestañas, transporte y acciones de sesión; 16 px solo donde haya restricción real de espacio; color de contraste alto (`neutral-content-color-default` / `icon-color-primary-default`) |
| Formularios | Picker, Text field, Search field, Checkbox, Switch y Divider Spectrum con etiqueta, ayuda y error | `Number field` no existe en Spectrum ni en Avalonia | Excepción documentada (retención/espacio libre como `TextBox`) |
| Diálogos | `SpectrumAlertDialog`; `ConfirmAsync` eliminado del code-behind | Ninguna | Ninguna |
| Estado y avisos | Inline alert, Toast y Progress bar con etiqueta y variantes semánticas | Ninguna | Ninguna |
| Tokens | Semánticos por capas (global → alias → componente) regenerados desde el paquete; prueba de regeneración automática | 4 usos de `gray-200` sin equivalente semántico | Excepción pendiente (no hay token equivalente en el paquete) |
| Forma de onda | Control propio `WaveformView` (excepción justificada, principio 1) | Sin equivalente oficial en Spectrum | Se mantiene como excepción |
| Foco | `:focus-visible` con anillo de 2 px en todos los temas | El anillo aplica `focus-indicator-gap` de forma indirecta | Referenciar el token explícitamente (pendiente) |
| Textos fijos | `.resx` en `MainWindow.axaml` | Copy restante fuera de `.resx` | Mover a `.resx` (fase futura) |
| Ancho de ventana | `MaxWidth` 720 (G10) | Capturas a 1000×680 y 1380×820 renderizan a 720 px | Opciones de decisión pendientes (G10) |

## Estructura global de la ventana

- **Barra de título:** icono, «Transcriptor», minimizar y cerrar con iconos. Sin selector de tema.
- **Tabs:** Estudio · Biblioteca · Ajustes · Registro.
- **Subencabezado:** ruta de la sesión actual (monoespaciada, truncada con elipsis y tooltip con la ruta completa) y Status light «Cifrado activo» (positiva) o «Sin cifrar» (aviso).
- **Banner de desarrollo:** Inline alert de tipo aviso, descartable.
- **Pie de estado:** estado de Whisper y progreso, solo texto de cuerpo pequeño.

## Estudio

A ≥ 960 px de ancho, la zona de captura conserva desplazamiento (RNUI-07) y se organiza en una tarjeta ancha con, de arriba a abajo:

- **Cabecera de captura:** estado con Status light (positivo/notice/negativo/info según estado de captura) + etiqueta de texto, y cronómetro grande a la derecha (cifras tabulares, sin centisegundos a menos que el motor los produzca).
- **Transporte:** la acción de grabar como protagonista (ToggleButton variante **Negative**, la más grande, roja solo mientras graba), seguida de Pausar, Detener y la acción primaria **Transcribir** (Button accent). Separados por divisor fino.
- **Autorización:** checkbox de autorización con el texto completo.
- **Dos filas de canal (Micrófono, Sistema):** cada una con icono, Picker de dispositivo, medidor horizontal fino (forma de onda/nivel) y lectura de Pico y RMS alineada (etiqueta–valor, monoespaciada). Avisos de clipping y silencio con Status light + botón **Reiniciar indicador**. Acciones **Actualizar dispositivos** y **Probar niveles**.
- Sesión: Text field «Referencia».

A < 960 px se colapsa a una columna. `Espacio` reproduce o pausa (nunca graba).

## Biblioteca

Maestro–detalle a ≥ 960 px; lista a la izquierda, detalle a la derecha. A < 960 px, una columna (detalle ocupa todo el ancho tras seleccionar).

- **Encabezado de lista:** contador de sesiones con **Badge** con el número de sesiones, Search field, Picker de filtro (Todas, Dos pistas, Solo micrófono, Solo sistema, Con transcripción, Incompletas) y Picker de orden.
- **Lista:** `SpectrumListView`; cada fila con título y hasta dos insignias de estado (no cinco). Acciones de fila (reproducir micrófono, transcribir, eliminar) como action buttons con tooltip.
- **Detalle:** encabezado con título y acciones; reproductor con Slider de posición, Action group de pista (Mic, Sistema, Mezcla) y Picker de velocidad; transcripción con búsqueda y exportación (SRT/TXT/MD).
- **Estados vacíos:** sin sesiones, sin resultados de búsqueda, sesión sin transcripción.

## Ajustes y Registro

- **Ajustes:** grupos plegables (**Accordion** de Spectrum) con icono, título y un Badge de estado a la derecha («Configurado», «Incompleto», según la validación ya existente). Contenido en una columna con ancho máximo del token `settings-form-max-width`. Barra inferior fija con el botón principal Accent (**Guardar**) y el secundario; solo acciones que ya existan.
- **Registro:** lista monoespaciada de solo lectura con **Copiar** y **Vaciar** (este último con confirmación); un filtro por nivel queda como mejora futura.

## Reglas transversales de UX

- Orden de botones en diálogos: secundaria a la izquierda, primaria a derecha.
- Una sola acción primaria por vista.
- El estado nunca se comunica solo con color: siempre texto o icono.
- Sin atajos Ctrl ni etiquetas de atajo en la interfaz. Se conservan `F5` actualizar, `Espacio` reproducir o pausar y `Escape` descartar avisos.
- Jerarquía: cada pantalla tiene una sola acción principal (Estudio: Grabar; Biblioteca: Reproducir/Pausar; Ajustes: Guardar; Registro: Copiar); el resto son secundarias o terciarias.
- Mensajes de error con causa y siguiente paso.
- Separación por superficie y espacio (nada de bordes duros de contenedor), una sola alerta de tope visible a la vez, jerarquía tipográfica con los roles definidos, todo el espaciado desde `spacing-*`/`card-edge-to-content-*`.
