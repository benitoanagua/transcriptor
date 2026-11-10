# Changelog

Todos los cambios relevantes de este proyecto se documentan aquí.

El formato sigue [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Cambiado
- Fidelidad con Stitch (768 px): barra de título única con marca, pestañas de texto y controles de ventana; subencabezado con ruta a la izquierda y estado de cifrado a la derecha; Estudio solo de captura (estado y referencia, cronómetro, transporte en una fila, autorización con barra de acento, canales con pico y RMS); Ajustes en tarjetas por apartado; Registro con panel monoespaciado. La ventana abre a 768 px de ancho (mínimo 520 px).
- Corregido: el indicador de cifrado se superponía al icono del subencabezado; Biblioteca, Ajustes y Registro ocupaban una fila Auto y no hacían scroll (la barra de Guardar quedaba fuera de la ventana); el texto de autorización y el estado de prueba de niveles se recortaban.
- Biblioteca: sin sesión seleccionada, el panel de detalle se reduce al título y la ayuda (antes mostraba once controles deshabilitados) y la lista de sesiones crece hasta `list-view-max-height-expanded` (420 px, token derivado); con una sesión seleccionada vuelve al tope normal.
- Biblioteca: «Mezcla» y «Velocidad» solo se muestran si la plataforma los soporta; si no, queda únicamente la nota de que no están disponibles.
- Los iconos dentro de botones heredan el `Foreground` del botón (acento, negativo, contorno) en lugar de un gris fijo.
- Biblioteca: cada sesión muestra una sola vez el tamaño de las pistas (antes aparecía en la cabecera y en el pie de la fila).
- Texto sans de 12 px pasa a 13 px mediante el token derivado `font-size-body-small`; las etiquetas siguen en 12 px (`font-size-75`).
- Iconografía alineada con el diseño de Google Stitch: Material Symbols (`@material-symbols/svg-400@0.48.3`) sustituye a Fluent; las claves `icon-*` se conservan. Se corrigen duplicados (`icon-AudioWave` era el micrófono; `icon-Transcript` y `icon-FileText` eran el mismo documento; `icon-Export` era un avión de papel) y se añaden `icon-Stop` (sustituye a los `Rectangle`) e `icon-Terminal` (pestaña Registro). Transporte y estado usan la variante rellena.
- El tema de `PathIcon` pinta sobre un lienzo fijo (`icon-viewbox-size`) en lugar de ajustar la geometría a sus límites, de modo que los iconos conservan su proporción óptica. La marca de la casilla pasa de trazo a relleno.
- Tipografía alineada con Stitch: JetBrains Mono sustituye a Source Code Pro (solo cronómetro, rutas, métricas y registro); nuevo peso SemiBold (600) para botones, pestañas, títulos y etiquetas (`semi-bold-font-weight`); la ventana fija Source Sans 3 como fuente por defecto; el texto sans de 11 px sube a 12 px.
- UI alineada con el diseño Stitch: se eliminan los atajos Ctrl (y sus etiquetas) y las pestañas pasan a etiquetas cortas (Estudio, Biblioteca, Ajustes, Registro).
- Jerarquía por pantalla con una sola acción principal: Estudio (Grabar, botón rojo de ancho completo con etiqueta), Biblioteca (Reproducir/Pausar), Ajustes (Guardar en barra fija inferior) y Registro (Copiar).
- Estudio: se elimina el acordeón «Entrada» que duplicaba las ondas; cada canal (micrófono, sistema) agrupa dispositivo, onda, pico y RMS.

### Añadido
- Interfaz Studio con captura, biblioteca y transcripción en un único espacio de trabajo, temas y capturas headless de referencia.
- Solución `Transcriptor.slnx` y selección reproducible del SDK .NET 10 mediante `global.json`.
- Propiedades comunes, versiones NuGet centralizadas y reglas de análisis/estilo para C#.
- Actualizaciones semanales de NuGet y GitHub Actions mediante Dependabot.
- Guía de contribución y política de reporte privado de vulnerabilidades.

### Cambiado
- La interfaz se adecúa a Adobe Spectrum 2 (solo tema oscuro): tokens generados desde `@adobe/spectrum-tokens@15.6.0` (SHA-512 verificado), acento azul `#4069FD`, variante negativa roja `#DF3422`, componentes themed y tipografía Source Sans 3 / Source Code Pro.
- Namespaces unificados y nombres descriptivos en las pruebas de Whisper, sin alterar sus aserciones.
- CI y release usan acciones fijadas por SHA, SDK de `global.json` y caché NuGet.
- README y documentación de publicación describen la solución y los jobs CI actuales.
