# Publicar una versión de Transcriptor

Las versiones para Windows x64 se generan desde GitHub Actions al publicar una etiqueta cuyo nombre comience por `v`. También puedes ejecutar el workflow manualmente (`workflow_dispatch`) desde una rama para compilar, probar, validar y descargar los ZIP como artefactos temporales, sin crear una GitHub Release. Solo los eventos de etiquetas `v*` publican una Release.

## Próxima versión candidata

La etiqueta existente `v0.1.0-rc.1` apunta al commit histórico `124fa19052c782187f08b27d60561c296c8aabdf`, anterior a las últimas mejoras de `main`, y su Release no contiene los cuatro artefactos definidos por el flujo actual. No muevas ni reutilices esa etiqueta: las etiquetas publicadas son referencias inmutables de una versión.

Después de fusionar el PR de endurecimiento del empaquetado, crea la siguiente candidata desde el `main` actualizado:

```powershell
git switch main
git pull origin main
git tag -a v0.1.0-rc.2 -m "Transcriptor 0.1.0 release candidate 2"
git push origin v0.1.0-rc.2
```

La etiqueta `-rc.2` hace que GitHub marque la publicación como *pre-release*. No publiques una versión estable hasta completar la validación manual en equipos Windows reales descrita en [HARDWARE-VALIDATION.md](HARDWARE-VALIDATION.md).

## Validar el empaquetado sin publicar

En GitHub, abre **Actions → Windows Release → Run workflow**, selecciona la rama que quieres validar y ejecuta el flujo. La ejecución manual compila y prueba los proyectos, genera ambos ZIP y sus checksums, y los carga como el artefacto `Transcriptor-windows-validation` durante 14 días. No crea etiquetas ni publica una Release. La publicación queda reservada a ejecuciones disparadas por etiquetas `v*`.

## Qué hace el workflow

1. Compila grabador CLI, transcriptor, configurador y aplicación WPF en Windows.
2. Ejecuta la suite automatizada de pruebas.
3. Publica la aplicación WPF, el configurador y el grabador CLI como binarios autocontenidos para `win-x64`.
4. Incluye README, modelo de seguridad, licencia del proyecto y avisos de terceros en el paquete.
5. Genera inventarios JSON de dependencias directas y transitivas para la aplicación de escritorio, la CLI de grabación y el motor de transcripción; valida que cada archivo sea JSON legible.
6. Verifica que los ejecutables y la documentación obligatoria estén presentes antes de empaquetar.
7. Genera el paquete estándar y el paquete portable, y calcula un SHA-256 independiente para cada ZIP.
8. Verifica la creación de los dos ZIP y del marcador/directorio de datos portable.
9. Publica los cuatro artefactos en GitHub Releases. Si falla una compilación, una prueba o una validación de archivos, no se publica la Release.


## Artefactos esperados

Cada Release creada por el workflow debe incluir exactamente estos cuatro archivos:

- `Transcriptor-win-x64.zip`: aplicación WPF, configurador, CLI de grabación y documentación (README, SECURITY, LICENSE y THIRD-PARTY-NOTICES) e inventarios JSON en `dependency-inventory/`.
- `Transcriptor-win-x64.zip.sha256`: checksum SHA-256 del paquete estándar.
- `Transcriptor-win-x64-portable.zip`: mismo conjunto de herramientas y documentación, con `portable.flag` y `data/` para mantener configuración y grabaciones junto a la aplicación.
- `Transcriptor-win-x64-portable.zip.sha256`: checksum SHA-256 del paquete portable.

Después de publicar, comprueba que los cuatro assets aparecen en la Release y valida el checksum antes de extraer el ZIP. En PowerShell:

```powershell
$expected = (Get-Content .\\Transcriptor-win-x64.zip.sha256).Split(' ')[0]
$actual = (Get-FileHash .\\Transcriptor-win-x64.zip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $expected) { throw "SHA-256 no coincide" }
```

Repite la comprobación con el ZIP portable y su archivo `.sha256`.

## Componentes que el usuario configura aparte

Los paquetes incluyen la aplicación, el configurador y la CLI, pero no incluyen `whisper-cli.exe` ni el modelo GGML. El usuario debe obtener una versión compatible de whisper.cpp y configurar sus rutas. Esto evita redistribuir binarios/modelos de terceros sin fijar y revisar sus licencias y versiones.

La publicación es autocontenida e incluye componentes del runtime de .NET 10 para Windows. Consulta [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) y conserva los avisos/licencias suministrados por la distribución de .NET. Los archivos `dependency-inventory/*.packages.json` registran las dependencias directas y transitivas de cada proyecto publicado; son inventarios de paquetes NuGet, no un SBOM completo de todos los componentes del sistema operativo o del runtime.

## Validación manual pendiente

CI no puede demostrar el funcionamiento de los controladores WASAPI, los dispositivos reales, Media Foundation, reproducción, DPAPI en perfiles reales ni los escenarios de varios monitores/DPI y Narrator. Ejecuta y registra los casos de [HARDWARE-VALIDATION.md](HARDWARE-VALIDATION.md) antes de declarar la versión estable.

## Descarga

Las versiones publicadas aparecen en https://github.com/benitoanagua/transcriptor/releases.
