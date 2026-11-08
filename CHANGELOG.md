# Changelog

Todos los cambios relevantes de este proyecto se documentan aquí.

El formato sigue [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

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
