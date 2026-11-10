# Changelog

Todos los cambios relevantes de este proyecto se documentan aquí.

El formato sigue [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Cambiado
- Fidelidad con Stitch (768 px): barra de título única con marca, pestañas de texto y controles de ventana; subencabezado con ruta a la izquierda y estado de cifrado a la derecha; Estudio solo de captura (estado y referencia, cronómetro, transporte en una fila, autorización con barra de acento, canales con pico y RMS); Ajustes en tarjetas por apartado; Registro con panel monoespaciado. La ventana abre a 768 px de ancho (mínimo 520 px).
- Corregido: el indicador de cifrado se superponía al icono del subencabezado; Biblioteca, Ajustes y Registro ocupaban una fila Auto y no hacían scroll (la barra de Guardar quedaba fuera de la ventana); el texto de autorización y el estado de prueba de niveles se recortaban.
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
