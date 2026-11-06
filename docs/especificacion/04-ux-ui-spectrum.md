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

Hallazgos en `ui/MainWindow.axaml`, `Theming/Tokens.axaml` y `Theming/Components.axaml`.

| Área | Estado actual | Brecha con Spectrum | Acción |
|---|---|---|---|
| Tema | `ThemeService`, ajuste `ThemeMode`, selector «Sistema/Claro/Oscuro» en la barra y diccionario `Light` en `Tokens.axaml` | Ya no se ofrece cambio de tema | Eliminar todo y fijar el tema oscuro |
| Tipografía | Inter, JetBrains Mono y Chakra Petch; tamaños 11 y 12 px | Fuente y escala no Spectrum; 11–12 px como cuerpo | Source Sans 3, Source Code Pro y escala tipográfica Spectrum |
| Etiquetas | `CAPTURA DUAL`, `PEAK MIC`, `CIFRADO`, `TRANSCRIBIR ESTA SESIÓN` | Spectrum usa sentence case | Reescribir el copy con mayúscula inicial |
| Botones | `btn-bevel`, `transport`, `btn-primary`, `btn-secondary` (28 px) | Sin bisel; variantes accent, primary, secondary y negative; alturas S, M, L y XL | `SpectrumButton` con variantes oficiales |
| Barra de título | Botones con glifos de texto «—» y «×» | Iconos de sistema, no texto | Iconos vectoriales |
| Navegación | `Button.spectrumTab` con clase `active` | Debe ser el componente Tabs con indicador, foco y teclado propios | `SpectrumTabs` con flechas, Inicio/Fin y nombres accesibles |
| Insignias | `CIFRADA`, `EN CLARO`, `PENDIENTE`, `INCOMPLETA` como `Border` con colores propios | Badge y Status light con variantes semánticas | `SpectrumBadge` y `SpectrumStatusLight` |
| Glifos en texto | `▶ MIC`, `■ DETENER`, `↑`, `↓` | Iconos del set, no caracteres Unicode | Iconos vectoriales con etiqueta accesible |
| Formularios | `ComboBox`, `TextBox`, `CheckBox` de Fluent con estilos parciales | Picker, Text field, Search field, Checkbox y Switch Spectrum, con etiqueta, ayuda y error | Plantillas propias |
| Diálogos | `ConfirmAsync` crea una ventana en code-behind | Alert dialog con jerarquía de botones definida | `SpectrumAlertDialog` reutilizable |
| Estado y avisos | Banner de desarrollo, badge de fallo y textos sueltos | Inline alert, Toast y Progress con etiqueta | Componentes de feedback unificados |
| Tokens | Unos 100 recursos con alias duplicados (`Surface`, `SurfaceCard`, `SurfaceStrip` con el mismo valor) | Tokens semánticos por capas (global, alias, componente) | Regenerar `Tokens.axaml` desde los tokens oficiales |
| Forma de onda | `WaveformView` y `led-screen` con colores propios | La visualización de datos tiene su propia paleta | Colores de data visualization de Spectrum; contenedor sin estética LED |
| Foco | Estilo genérico `:focus` en Button, TextBox y ComboBox | Indicador de 2 px con desplazamiento, visible por teclado | `:focus-visible` en todos los controles |
| Textos fijos | Español embebido en XAML | Necesario revisar el copy de forma sistemática | Mover a recursos `.resx` |

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
