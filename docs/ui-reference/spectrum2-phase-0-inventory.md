# Inventario UI previo a Spectrum 2

Inventario de `ui/MainWindow.axaml` y `ui/Theming/Components.axaml` antes de la
adecuación. Los números indican ocurrencias en esos archivos.

## Controles de `MainWindow.axaml`

| Control | Ocurrencias |
|---|---:|
| Border | 44 |
| Button | 46 |
| CheckBox | 3 |
| ComboBox (incluye plantillas) | 9 |
| DataTemplate | 4 |
| Grid (incluye definiciones de filas) | 41 |
| ListBox (incluye plantillas) | 4 |
| PathIcon | 18 |
| ProgressBar | 1 |
| ScrollViewer | 4 |
| SelectableTextBlock | 1 |
| Slider | 1 |
| StackPanel | 35 |
| TextBlock | 83 |
| TextBox | 10 |
| WaveformView | 2 |

## Clases usadas en la vista

`accent`, `badge-accent`, `badge-ok`, `badge-warn`, `btn-bevel`,
`btn-primary`, `btn-secondary`, `captureAction`, `captureAvailable`,
`captureCard`, `captureHeader`, `captureState`, `captureStateDot`,
`captureTelemetry`, `card`, `close`, `danger`, `h2`, `led-screen`,
`libraryFooter`, `libraryHeader`, `metric`, `minimize`, `muted`, `record`,
`sessionAction`, `sessionBadge`, `sessionCard`, `sessionIcon`, `sessionList`,
`spectrumNavigation`, `spectrumSubheader`, `spectrumTab`, `success`,
`transcriptFooter`, `transcriptList`, `transcriptSegment`, `transcriptText`,
`transcriptTime`, `transport`, `warning`, `windowAction`.

## Selectores de `Components.axaml`

- Globales: `Button`, `Button:disabled`, `TextBox`, `ComboBox`, `CheckBox`,
  `ScrollBar:vertical`, `ScrollBar:horizontal`, `ProgressBar`.
- Navegación: `spectrumNavigation`, `spectrumTab`, `spectrumTab:pointerover`,
  `spectrumTab.active`, `spectrumSubheader`.
- Ventana: `windowAction`, `windowAction.minimize`, `windowAction.close`,
  `windowAction:pointerover`, `windowAction.close:pointerover`.
- Captura: `captureCard`, `captureHeader`, `captureStateDot`,
  `captureStateDot.recording`, `captureState`, `captureState.recording`,
  `captureAvailable`, `captureAvailable.unavailable`, `transport`,
  `transport.record`, `captureAction`, `captureTelemetry`, `record-active`.
- Biblioteca: `sessionList`, `sessionCard`, sus estados `:pointerover` y
  `:selected`, `sessionIcon`, `sessionBadge` y sus variantes `success`,
  `warning`, `accent`, `muted`, `sessionAction`.
- Transcripción y paneles: `transcriptSegment` y sus estados `:pointerover`,
  `active`, `match`; `transcriptList`, `transcriptTime`, `transcriptText`,
  `transcriptHeader`, `transcriptFooter`, `libraryHeader`, `libraryFooter`,
  `card`, `h2`, `metric`.
- Estilos heredados a retirar al sustituirlos: `btn-bevel`, `led-screen`,
  `badge-ok`, `badge-warn`, `badge-neutral`, `badge-accent`, `btn-primary`,
  `btn-secondary`, `btn-icon`, `accent`, `danger`.
- Interacción y controles: `Button:focus`, `TextBox:focus`, `ComboBox:focus`.

## Capturas antes

El comando `dotnet run --project desktop-dev/Transcriptor.Dev.csproj -c Release
-- --capture-ui-reference` se ejecutó antes de los cambios. La ventana limita
actualmente su ancho a 720 DIP, por lo que las diez imágenes generadas miden
720×680 o 720×820 en vez de 1000×680 y 1380×820. Se conservan en este directorio
como línea base real; se deberá revisar esa restricción al regenerar las
capturas al final de las fases.
