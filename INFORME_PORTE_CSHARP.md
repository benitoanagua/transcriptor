# Informe — Porte del tooling de Transcriptor a C# / .NET 10

## 1. Lista de archivos

### Creados (12)

**Tooling (librería + CLI):**
- `tools/Transcriptor.DesignData/Transcriptor.DesignData.csproj` — librería net10.0, solo BCL + `System.Formats.Tar`.
- `tools/Transcriptor.DesignData/Pins.cs` — pines de `@adobe/spectrum-tokens@15.6.0`, `@adobe/spectrum-css-workflow-icons@5.0.0`, `@spectrum-css/ui-icons@1.1.2` (nombre, versión, URL, SHA-512 base64).
- `tools/Transcriptor.DesignData/DesignDataCache.cs` — caché idempotente en `artifacts/design-data/<paquete>@<versión>/package/`; descarga con `HttpClient`, verifica SHA-512, extrae `.tgz` con `System.Formats.Tar`.
- `tools/Transcriptor.DesignData/TokenGenerator.cs` — genera `Tokens.axaml` byte a byte (colores `dark`, numéricos `desktop`, `list-view-max-height` y `settings-form-max-width` derivados, `format float` con `.0`, pesos de fuente, `code-font-family`, familias de fuente).
- `tools/Transcriptor.DesignData/IconGenerator.cs` — genera `Icons.axaml` byte a byte (28 iconos lista blanca, `fill-rule` → `F1`/`F0`, aborta en SVG con elementos no-`path` o `transform`).
- `tools/Transcriptor.DesignData/DesignDataApi.cs` — API de proceso (no red salvo `EnsureCache`, rutas de salida versionadas).
- `tools/Transcriptor.Tools/Transcriptor.Tools.csproj` — consola net10.0 sobre la librería.
- `tools/Transcriptor.Tools/Program.cs` — subcomandos `fetch`, `tokens`, `icons`, `inventory`, `package`, `smoke`. `inventory` usa `dotnet list ... package --include-transitive --format json`; `package` copia `README.md`/`docs`/`LICENSE`, valida archivos obligatorios, crea el ZIP con `ZipFile`, sidecar SHA-256, verificación de entradas y guardia anti-fugas de `desktop-dev`; `smoke` arranca el `.exe --smoke-test` y falla si exit code != 0.

**Pruebas (nuevas, en proceso, sin red):**
- `tests/Transcriptor.Tests/PortableProjectsStayOsNeutralTests.cs` — lee `.csproj` como XML; falla si `ui`/`transcription`/`desktop-dev` target `-windows` o referencia `platform-windows`, y si `desktop-avalonia` referencia `desktop-dev`.
- `tests/Transcriptor.Tests/ToolingIsDotNetOnlyTests.cs` — falla si se versionan `*.py`/`*.sh`/`*.ps1`/`*.js`/`*.mjs`/`*.ts`/`package.json`, o si pruebas/herramientas arrancan `python`/`python3`/`node`/`npm`/`npx`/`bash`/`sh`/`pwsh`.
- `tests/Transcriptor.Tests/ResourceResolutionTests.cs` — recorre `*.axaml`, extrae `{StaticResource x}`/`{DynamicResource x}` y falla si `x` no está definido como `x:Key` en algún diccionario.
- `tests/Transcriptor.Tests/FontIntegrityTests.cs` — SHA-256 de cada TTF de `ui/Assets/Fonts/`.

### Borrados (2)
- `tools/generate-spectrum-tokens.py`
- `tools/generate-spectrum-icons.py`

### Modificados (18)
- `Transcriptor.slnx` — añade ambos proyectos en carpeta `/tools/`.
- `Directory.Packages.props` — añade `System.Formats.Tar 9.0.0` (versión centralizada).
- `.gitignore` — quita entrada «Python tooling caches» (`__pycache__/`).
- `.github/workflows/dotnet.yml` — añade `actions/cache` sobre `artifacts/design-data` (clave `hashFiles('Pins.cs')`), paso `dotnet run --project tools/Transcriptor.Tools -- fetch` antes de build/test, elimina el paso `grep`.
- `.github/workflows/release-windows.yml` — pasos `inventory`, `smoke`, `package` ahora invocan `dotnet run --project tools/Transcriptor.Tools -- ...` (sin `pwsh` salvo los que ya envuelven `dotnet`).
- `CONTRIBUTING.md` — documenta flujo `fetch`/`tokens`/`icons`.
- `docs/ARCHITECTURE.md`, `docs/especificacion/trazabilidad.md`, `docs/THIRD-PARTY-NOTICES.md`, `docs/ui-reference/revision-visual.md` — menciones de `tools/generate-*.py` y `npm view` sustituidas por comando C# / `Pins.cs`.
- `tests/Transcriptor.Tests/TokenRegenerationTests.cs` e `IconRegenerationTests.cs` — llaman a la librería en proceso, leen de la caché, fallan con mensaje accionable (`dotnet run ... -- fetch`) si falta; sin `Process.Start`.
- `tests/Transcriptor.Tests/Transcriptor.Tests.csproj` — referencia `Transcriptor.DesignData` y usings.
- `ui-spectrum/Theming/ControlThemes.axaml`, `ui/MainWindow.axaml`, `ui/MainWindow.axaml.cs`, `ui/Theming/Components.axaml`, `ui/Theming/Icons.axaml`, `ui/Theming/Tokens.axaml` — estado de la pasada visual previa (claves `icon-*`, cromo D5, bordes suaves, `icon-ViewList` añadido, 7 tokens `drop-shadow-*`).

## 2. Resultado de la prueba de paridad

**SÍ.** El generador C# reproduce **byte a byte** ambos archivos versionados:

- `ui/Theming/Tokens.axaml`: coincidencia exacta (tras corregir `220` vs `220.0` para igualar `str(float)` de Python).
- `ui/Theming/Icons.axaml`: coincidencia exacta (28 entradas, orden alfabético, `F1` por defecto).

Criterio de aceptación cumplido: `git diff ui/Theming/Tokens.axaml ui/Theming/Icons.axaml` vacío tras regenerar. Solo entonces se borraron los `.py`.

## 3. Pasos de CI sin validar (requieren runner de Windows)

Marcados como **pendiente en runner de Windows**:

- `release-windows.yml` entero: `inventory` (3 proyectos), `package` (copiado + validación obligatoria + ZIP + sidecar + guardia anti-fugas), `smoke <exe>`; la verificación de bytes dentro del ZIP y el hash del archivo no se pueden ejecutar en Linux.
- Job `desktop-windows` de `dotnet.yml` (build de `desktop-avalonia`, `platform-windows`, `rec`, `setup` y suite `Transcriptor.Tests.Windows`).
- `StudioGeometryTests` (ventana real bajo `Avalonia.Headless`): cuelga en este entorno sin display X11/WSLg; es preexistente en HEAD (verificado con worktree de `d437158`), no causado por el porte.

## 4. Paquetes NuGet añadidos

Uno solo, con versión centralizada en `Directory.Packages.props`:

- `System.Formats.Tar 9.0.0` — **justificación:** .NET 10 BCL no incluye API para leer archivos tar (solo gzip). Es indispensable para extraer los `.tgz` del registro de npm. No hay alternativa en la biblioteca base.

No se añadió paquete de imágenes para el contact-sheet (Paso 6 del trabajo visual): Avalonia 12.1.0 ya trae `Avalonia.Media.Imaging`; el contact-sheet se deja pendiente en Windows porque requiere ver imágenes y no hay display en este entorno.

## 5. Preguntas abiertas

1. **contact-sheet.png:** no se generó en Linux (falta display y herramienta de bitmap). ¿Se genera en Windows con Avalonia `RenderTargetBitmap`/`WriteableBitmap` (Paso 6 del trabajo visual)?
2. **Verificación visual directa:** ninguna captura PNG ha sido revisada directamente (sin display). `docs/ui-reference/revision-visual.md` está reescrito marcando todo como «no verificado».
3. **Captura faltante:** `studio-reposo-1380x820.png` no se generó (timing headless). ¿Se regenera en Windows?
4. **CI en Windows:** `release-windows.yml` y el job `desktop-windows` de `dotnet.yml` no se validaron aquí.
5. **Trabajo visual D1-D5 (sombras, radios, bordes, cromo duplicado):** los cambios base están en el working tree (claves de icono, cromo D5, bordes suaves, 7 tokens `drop-shadow-*` emitidos por el generador), pero la aplicación sistemática (solo flotantes con sombra, radios por componente, sin bordes `gray-visual-color`, ventana propia solo si extiende área cliente) requiere verificación visual en Windows y no se ha completado.

## 6. Criterios de aceptación del porte (adenda 1) — estado en Linux

| Criterio | Estado |
|---|---|
| `git ls-files` sin `.py`/`.sh`/`.ps1`/`.js`/`package.json` | ✅ Ninguno |
| Clon limpio: `fetch` + `dotnet test` verde, sin red en pruebas | ✅ Verificado (169/169) |
| `tokens`/`icons` regeneran sin `git diff` | ✅ Byte a byte |
| Paridad, `ToolingIsDotNetOnlyTests`, `FontIntegrityTests` verdes | ✅ |
| `dotnet build -c Release` sin avisos | ✅ 0 avisos, 0 errores |
| `--smoke-test` código 0 | ✅ (cabeza de desarrollo) |
