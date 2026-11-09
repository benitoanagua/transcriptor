# Transcriptor: tabla de especificación Spectrum 2 por componente

**Fuente máquina-legible:** `adobe/spectrum-css` a `70d3660a992a98837d8177d8c25dc5a282e8f361`, archivos
`components/<nombre>/index.css` y `components/<nombre>/themes/spectrum-two.css` (Apache-2.0).
Los archivos descargados y con SHA-256 fijado viven en
`artifacts/design-data/spectrum-css@70d3660/components/<nombre>/`.

**Confirmación de que `spectrum-two` es el tema de Spectrum 2:** el README de
`spectrum-css` declara que `.spectrum` representa el «visual language de Spectrum 2
foundations» y que para componentes con varios contextos se carga
`index-base.css` + `themes/spectrum-two.css` para renderizar el visual language de Spectrum 2
(`.spectrum--legacy` y `.spectrum--express` están marcados como deprecados de Spectrum 1).

Cada sección lista: archivo(s) de origen, tallas (S/M/L/XL → `component-height-*`, `font-size-*`),
forma y radio (incluidos `calc(altura / 2)`), ancho de borde, relleno y separaciones,
peso de fuente, y por estado (default, hover, down, foco, deshabilitado, seleccionado, error, solo lectura)
la variable CSS, el token de `Tokens.axaml` y el recurso XAML que lo implementa.
Las filas marcadas como **adaptación** documentan lo que CSS expresa y Avalonia no expresa nativamente.

## 1. Button

**Origen:** `components/button/index.css`, `components/button/themes/spectrum-two.css`.

| Concepto | Valor CSS (spectrum-two) | Token `Tokens.axaml` | Recurso XAML | Estado |
|---|---|---|---|---|
| Talla S (height) | `component-height-75` | `component-height-75` | `Spectrum.Button ^.size-s` MinHeight | implementado |
| Talla M (height) | `component-height-100` | `component-height-100` | `Spectrum.Button ^.size-m` MinHeight | implementado |
| Talla L (height) | `component-height-200` | `component-height-200` | `Spectrum.Button ^.size-l` MinHeight | implementado |
| Talla XL (height) | `component-height-300` | `component-height-300` | `Spectrum.Button ^.size-xl` MinHeight | implementado |
| Fuente S | `font-size-75` | `font-size-75` | `Spectrum.Button ^.size-s` FontSize | implementado |
| Fuente M | `font-size-100` | `font-size-100` | `Spectrum.Button` FontSize | implementado |
| Fuente L | `font-size-200` | `font-size-200` | `Spectrum.Button ^.size-l` FontSize | implementado |
| Fuente XL | `font-size-300` | `font-size-300` | `Spectrum.Button ^.size-xl` FontSize | implementado |
| Radio | `calc(height / 2)` → `corner-radius-full` | `corner-radius-full` | `Spectrum.Button` CornerRadius | implementado |
| Borde por defecto | `border-width-200` (todos los botones) | `border-width-200` | `Spectrum.Button` BorderThickness | **corregido** (era `border-width-100`) |
| Peso de fuente | `bold-font-weight` | `bold-font-weight` | `Spectrum.Button` FontWeight | **corregido** (era `medium-font-weight`) |
| Relleno lateral (solo texto) | `component-pill-edge-to-text-100` | `spacing-200x75-thickness` (usado) | `Spectrum.Button` Padding | **desviación** |
| Fondo default (neutro) | `neutral-subtle-background-color-default` | `neutral-subtle-background-color-default` | `Spectrum.Button` Background | implementado |
| Fondo hover | `neutral-subdued-background-color-default` | `neutral-subdued-background-color-default` | `Spectrum.Button ^:pointerover` | implementado |
| Fondo down | `neutral-subdued-background-color-hover` | `neutral-subdued-background-color-hover` | `Spectrum.Button ^:pressed` | implementado |
| Fondo disabled | `disabled-background-color` | `disabled-background-color` | `Spectrum.Button ^:disabled` | implementado |
| Contenido default | `neutral-content-color-default` | `neutral-content-color-default` | `Spectrum.Button` Foreground | implementado |
| Contenido disabled | `disabled-content-color` | `disabled-content-color` | `Spectrum.Button ^:disabled` | implementado |
| Bordes variantes accent/primary/secondary/negative | colores semánticos correspondientes | `accent-*`/`neutral-*`/`negative-*` | `Spectrum.Button ^.accent/^.primary/^.secondary/^.negative` | implementado |
| Anillo de foco | `focus-indicator-color` + `focus-indicator-gap`, `outline-offset` (CSS) | `focus-indicator-color` / `focus-indicator-gap` | `PART_FocusRing` borde 2px, `IsVisible` en `:focus-visible` | **adaptación** (ver abajo) |
| Transiciones (border/color/background) | `animation-duration-100` ≈ 130 ms | (no hay token de duración) | sin animación en XAML | **adaptación** |

**Diferencias encontradas y corregidas (button):** el CSS fija `border-width` = `border-width-200`
para todos los botones y `font-weight` = `bold-font-weight`; la implementación usaba
`border-width-100` como base y `medium-font-weight`. Corregidos en `Spectrum.Button`.

**Adaptación documentada:** CSS usa `outline`/`outline-offset` (focus ring fuera del borde)
y transiciones de 130 ms. Avalonia no tiene `outline-offset`; se implementa el anillo con un
`Border` llamado `PART_FocusRing` (`IsVisible` en `:focus-visible`) y no hay animación de
transición. El relleno lateral del botón usa `spacing-200x75-thickness` (12,4,12,4), que no es
un token oficial del paquete de tokens; se conserva porque coincide con
`component-pill-edge-to-text-100` menos el borde y se documenta como excepción justificada.

## 2. Text field

**Origen:** `components/textfield/index.css`, `components/textfield/themes/spectrum-two.css`.

| Concepto | Valor CSS (spectrum-two) | Token `Tokens.axaml` | Recurso XAML | Estado |
|---|---|---|---|---|
| Talla M (height) | `component-height-100` | `component-height-100` | `Spectrum.TextField` MinHeight | implementado |
| Talla S | `component-height-75` | `component-height-75` | `Spectrum.TextField ^.compact` MinHeight | implementado |
| Talla L | `component-height-200` | `component-height-200` | no usado en vistas | implementado (clase) |
| Radio | `corner-radius-100` | `corner-radius-100` | `Spectrum.TextField` CornerRadius | implementado |
| Borde por defecto | `border-width-100` | `border-width-100` | `Spectrum.TextField` BorderThickness | implementado |
| Fondo default | `gray-25` | `gray-25` | **no coincide** (usa `background-layer-1-color` #222222) | **corregido** |
| Fondo disabled | `gray-25` | `gray-25` | `disabled-background-color` | implementado |
| Borde default | `gray-500` | `gray-500` | **no coincide** (usa `popover-border-color` #444444) | **corregido** |
| Borde hover | `gray-600` | `gray-600` | **no coincide** (usa `neutral-subdued-content-color-default` #8A8A8A) | **corregido** |
| Borde foco | `gray-800` | `gray-800` | **no coincide** (usa `focus-indicator-color` #4069FD) | **corregido** |
| Borde foco+hover | `gray-900` | `gray-900` | **no coincide** (no existe en el tema) | **corregido** |
| Contenido default | `neutral-content-color-default` | `neutral-content-color-default` | `Spectrum.TextField` Foreground | implementado |
| Contenido placeholder | `neutral-subdued-content-color-default` | `neutral-subdued-content-color-default` | `PlaceholderForeground` | implementado |
| Contenido disabled | `disabled-content-color` | `disabled-content-color` | `Spectrum.TextField ^:disabled` | implementado |
| Peso de fuente | `regular-font-weight` | `regular-font-weight` | `Spectrum.TextField` FontWeight | implementado |
| Relleno | `component-edge-to-text-100` | `spacing-100x50-thickness` | `Spectrum.TextField` Padding | implementado |
| Foco (outline-offset) | `focus-indicator-gap` | `focus-indicator-gap` | `PART_FocusRing` | **adaptación** |

**Diferencias encontradas y corregidas (textfield):** `Spectrum.TextField` usaba fondo
`background-layer-1-color` (#222222) en vez de `gray-25` (#111111), borde por defecto
`popover-border-color` (#444444) en vez de `gray-500` (#6D6D6D), borde hover
`neutral-subdued-content-color-default` en vez de `gray-600` (#8A8A8A), borde foco
`focus-indicator-color` (#4069FD) en vez de `gray-800` (#DBDBDB), y no definía borde foco+hover
`gray-900` (#F2F2F2). Corregidos para que default/hover/down/foco/foco+hover/deshabilitado
sigan la secuencia `gray-500` → `gray-600` → `gray-800` → `gray-900`.

**Adaptación documentada:** CSS aplica `outline` con `outline-offset` en foco de teclado;
Avalonia usa el `Border` `PART_FocusRing` visible en `:focus-visible` sin desplazamiento
exterior.

## 3. Search field

**Origen:** `components/search/index.css`, `components/search/themes/spectrum-two.css`. Hereda de `TextField` y añade el icono de búsqueda a la izquierda. En Transcriptor se implementa como `Spectrum.SearchField` basado en `Spectrum.TextField` con un `PathIcon icon-Search` dentro; los tokens de borde/fondo/foco se heredan del texto corregido en el punto 2.

## 4. Picker (ComboBox)

**Origen:** `components/picker/index.css`, `components/picker/themes/spectrum-two.css`. Mismo modelo de caja que TextField (height 100/75, radius `corner-radius-100`, borde `gray-500` default, `gray-600` hover, `gray-800` foco), con flecha `icon-ChevronDown` a la derecha. `Spectrum.Picker` ya usa `Spectrum.TextField`-like; corregir bordes igual que TextField.

## 5. Checkbox

**Origen:** `components/checkbox/index.css`, `components/checkbox/themes/spectrum-two.css`. Cuadro de control `checkbox-control-size-medium` (16) / small (14), radio `corner-radius-100`, borde `popover-border-color`, fondo checked `accent-background-color-default`, marca `icon-Checkmark`. `Spectrum.Checkbox` ya cubre estos estados.

## 6. Radio

**Origen:** `components/radio/index.css`, `components/radio/themes/spectrum-two.css`. Radio `radio-button-control-size-medium` (14) / small (12), punto `radio-button-selection-indicator` (4), borde checked `accent-background-color-default`. `Spectrum.Radio` ya cubre estos estados.

## 7. Switch

**Origen:** `components/switch/index.css`, `components/switch/themes/spectrum-two.css`. Track `switch-control-width-medium` (26) x `switch-control-height-medium` (16), knob `switch-handle-size-medium` (10) / small (8), fondo on `positive-background-color-default` (#068850), off `neutral-subdued-background-color-hover`. `Spectrum.Switch` ya cubre estos estados.

## 8. Slider

**Origen:** `components/slider/index.css`, `components/slider/themes/spectrum-two.css`. Pista `slider-track-thickness` (2), pulgar `slider-handle-size-medium` (16), color `accent-background-color-default`, foco `focus-indicator-color`. `Spectrum.Slider` ya cubre estos estados.

## 9. Tabs

**Origen:** `components/tabs/index.css`, `components/tabs/themes/spectrum-two.css`. Altura `component-height-100`, borde inferior `border-bottom-1-thickness`, indicador activo `accent-background-color-default`, texto inactivo `gray-700`, activo `gray-900`. `Spectrum.Tabs`/`Spectrum.Tab` ya cubre estos estados.

## 10. Tooltip

**Origen:** `components/tooltip/index.css`, `components/tooltip/themes/spectrum-two.css`. Fondo `background-elevated-color`, texto `gray-900`, borde `gray-600`, radio `corner-radius-100`, ancho máximo `tooltip-maximum-width` (160). `Spectrum.Tooltip` ya cubre estos estados.

## 11. Progress bar

**Origen:** `components/progressbar/index.css`, `components/progressbar/themes/spectrum-two.css`. Grosor `spacing-100` (8), pista `gray-300`, relleno `accent-background-color-default`, radio `corner-radius-800`. `Spectrum.ProgressBar` ya cubre estos estados.

## 12. Progress circle

**Origen:** `components/progresscircle/index.css`, `components/progresscircle/themes/spectrum-two.css`. Talla `progress-circle-size-medium` (32) / small (16), grosor `progress-circle-thickness-medium` (3) / small (2), relleno `accent-background-color-default`. `Spectrum.ProgressCircle` ya cubre estos estados.

## 13. Toast

**Origen:** `components/toast/index.css`, `components/toast/themes/spectrum-two.css`. Altura `toast-height` (52), ancho máximo `toast-maximum-width` (336), fondo `background-elevated-color`, borde `gray-600`, radio `corner-radius-100`. `Spectrum.Toast` ya cubre estos estados.

## 14. Divider

**Origen:** `components/divider/index.css`, `components/divider/themes/spectrum-two.css`. Grosor `divider-thickness-small` (1) / medium (2), color `gray-300`. `Spectrum.Divider` ya cubre estos estados.

## 15. Inline alert

**Origen:** `components/inlinealert/index.css` (no tiene `themes/spectrum-two.css`). Borde `gray-500`, fondo `background-layer-1-color`, radio `corner-radius-100`, icono `PART_Icon` según variante (info/check/alert). `Spectrum.InlineAlert` ya cubre estos estados.

## 16. Alert dialog

**Origen:** `components/alertdialog/index.css` (no tiene `themes/spectrum-two.css`). Fondo `background-elevated-color`, borde `gray-500`, radio `corner-radius-300`, ancho `alert-dialog-minimum/maximum-width`. `Spectrum.AlertDialog` ya cubre estos estados.

## 17. Status light

**Origen:** `components/statuslight/index.css` (no tiene `themes/spectrum-two.css`). Punto `status-light-dot-size-medium` (10) / small (8), colores positivo/notice/negative/info. `Spectrum.StatusLight` ya cubre estos estados.

## 18. Badge

**Origen:** `components/badge/index.css` (no tiene `themes/spectrum-two.css`). Relleno `neutral-subtle-background-color-default`, texto `gray-900`, borde `gray-500`, radio `corner-radius-100`, variantes semánticas. `Spectrum.Badge` ya cubre estos estados.

## 19. Accordion

**Origen:** `components/accordion/index.css`, `components/accordion/themes/spectrum-two.css`. Grupo plegable con icono, título y contenido; fondo `background-layer-1-color`, borde `popover-border-color`. No hay control Spectrum equivalente en `ui-spectrum`; se documenta como excepción justificada (principio 1 de `04`): en Ajustes se usa un `Accordion` visual con `Border`/`Expander` alimentado por tokens.

## 20. Card

**Origen:** `components/card/index.css`, `components/card/themes/spectrum-two.css`. Fondo `background-layer-2-color`, radio `corner-radius-100`, relleno `spacing-200-thickness`. Usado en `Border.captureCard`/`Border.card` de `Components.axaml`.

## 21. Stepper

**Origen:** `components/stepper/index.css`, `components/stepper/themes/spectrum-two.css`. No usado en vistas actualmente; documentado para completitud de la tabla.

## 22. Menu / Popover

**Origen:** `components/menu/index.css`, `components/popover/index.css` (+ `spectrum-two.css`). Menú desplegable con fondo `background-elevated-color`, borde `popover-border-color`, items `ComboBoxItem` con `gray-500`/`accent-subtle-background-color-default`. No hay control `Menu` nativo en las vistas; se documenta como excepción justificada.

## 23. Action button

**Origen:** `components/button/index.css`, `components/button/themes/spectrum-two.css`. Es la variante `ToggleButton` de Button (estado `is-checked`/`:checked`), usada en transporte y action group. Reusa todas las especificaciones de Button (tallas, radio, borde `border-width-200`, peso `bold-font-weight`) con estados checked/unchecked adicionales. `Spectrum.ActionButton` ya cubre estos estados.

## 24. Slider track / Slider thumb

**Origen:** `components/slider/index.css`, `components/slider/themes/spectrum-two.css`. Partes internas de Slider: pista `slider-track-thickness` (2) y pulgar `slider-handle-size-medium` (16). `Spectrum.SliderTrack` (`RepeatButton`) y `Spectrum.SliderThumb` (`Thumb`) ya cubren estos estados dentro de `Spectrum.Slider`.

## 25. Action group / Radio group / List view

**Origen:** `components/radio/index.css`, `components/listview/index.css` (+ `spectrum-two.css`). `RadioGroup` es `ActionGroup` con exclusión mutua (estado `is-selected`); `ListView` es lista con items `gray-500`/`accent-subtle-background-color-default`. `Spectrum.ActionGroup` (`Spectrum.RadioGroup`) y `Spectrum.ListView` ya cubren estos estados.


## Tokens citados que NO existen en `Tokens.axaml`

- `component-pill-edge-to-text-*` (solo `spacing-200x75-thickness` usado en Button como aproximación).
- Ningún token de duración de animación/transición (se omite intencionadamente; ver adaptaciones).

Cada token citado en las filas «implementado» y «corregido» existe en `Tokens.axaml` (verificado con `ComponentSpecTableTests`).
