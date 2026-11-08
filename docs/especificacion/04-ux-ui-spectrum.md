# Transcriptor: UX/UI con Adobe Spectrum

Fecha: 2026-11-05

Cómo debe verse y comportarse la interfaz para seguir Adobe Spectrum de forma estricta: fundamentos visuales, brecha con el estado actual y diseño de cada pantalla. Los requisitos RUI y RNUI están en `01-requisitos.md`, los flujos FI en `03-flujos-y-estados.md` y el orden de trabajo, con su estado, en `trazabilidad.md`.

## Fundamentos visuales

Spectrum no tiene implementación oficial para Avalonia, así que «estricto» significa reconstruir los componentes como `ControlTheme` propios que reproduzcan las especificaciones, alimentados por los tokens oficiales.

| Decisión | Valor |
|---|---|
| Versión de referencia | Spectrum 2 |
| Tema | Solo oscuro; sin selector y sin tema claro |
| Tipografía | Source Sans 3 (OFL) para la interfaz y Source Code Pro para rutas y códigos |
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
| Tipografía | Source Sans 3 / Source Code Pro; cuerpo `font-size-100` (14 px) con su `line-height-font-size-*` | Métricas distintas a Adobe Clean Spectrum | Calibrar interlineado en galería (excepción G5) |
| Etiquetas | Sentence case en `MainWindow.axaml` vía `ui/Strings.resx` | Copy de ViewModels y botones sueltos no migrado a `.resx` | Mover a `.resx` (fase futura) |
| Botones | `Spectrum.Button`: Accent/Primary/Secondary/Negative × fill/outline, tallas S-M-L-XL, `corner-radius-100` (4 px) | Ninguna | Ninguna |
| Barra de título | `PathIcon` de minimizar y cerrar, sin glifos de texto | Ninguna | Ninguna |
| Navegación | `Spectrum.Tabs`/`Spectrum.Tab` con indicador, foco y teclado propios | Ninguna | Ninguna |
| Insignias | `SpectrumBadge` con variantes semánticas (positiva, negativa, aviso, acento) | Ninguna | Ninguna |
| Glifos en texto | Reemplazados por `PathIcon` nombrados (`IconPlay`, `IconStop`, `IconChevronUp`, `IconChevronDown`) | Ninguna | Ninguna |
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

- **Estado y transporte:** estado en texto con Status light, temporizador con cifras tabulares (sin Chakra Petch) y los botones Grabar, Pausar, Detener y la acción primaria **Transcribir**.
- **Captura dual:** dos filas idénticas (Micrófono, Sistema), cada una con Picker, forma de onda y métricas «Pico» y «RMS» como pares etiqueta–valor. Los avisos de clipping y silencio usan Status light o Inline alert con el botón **Reiniciar indicador**.
- **Sesión:** Text field «Referencia», Checkbox de autorización con el texto completo y las acciones **Actualizar dispositivos** y **Probar niveles**.
- **Estados a diseñar:** reposo, probando niveles, grabando, pausado, detenido con fallo, sin dispositivo de sistema (mensaje explicativo en lugar de la forma de onda) y Whisper no configurado.

## Biblioteca

- **Encabezado:** contador, Search field, Picker de filtro (Todas, Dos pistas, Solo micrófono, Solo sistema, Con transcripción, Incompletas) y Picker de orden.
- **Lista:** cada fila con título, fecha, duración y hasta dos insignias de estado (no cinco); acciones de fila como Action buttons con tooltip.
- **Detalle y visor:** encabezado con acciones (Transcribir, Exportar, Copiar, Eliminar), barra de reproducción (Slider, Action group y Picker de velocidad) y lista de segmentos con tiempo y texto.
- **Estados vacíos:** sin sesiones («Aún no hay grabaciones» con la acción **Ir a Estudio**), sin resultados de búsqueda («Sin coincidencias» con **Borrar filtros**) y sesión sin transcripción («Esta sesión no se ha transcrito» con **Transcribir**).

## Ajustes y Registro

- **Ajustes:** formulario en una columna con grupos (Grabación, Transcripción, Apariencia, Seguridad) separados por Divider. Cada campo tiene etiqueta siempre visible, ayuda y error; el marcador de posición no sustituye a la etiqueta.
- **Registro:** lista monoespaciada de solo lectura con **Copiar** y **Vaciar** (este último con confirmación); un filtro por nivel queda como mejora futura.

## Reglas transversales de UX

- Orden de botones en diálogos: secundaria a la izquierda, primaria a la derecha.
- Una sola acción primaria por vista.
- El estado nunca se comunica solo con color: siempre texto o icono.
- Atajos: `Ctrl+R` grabar, `Ctrl+P` pausar, `Ctrl+F` buscar, `F5` actualizar, `Espacio` reproducir o pausar; visibles en tooltips y listados en Ajustes.
- Mensajes de error con causa y siguiente paso.
