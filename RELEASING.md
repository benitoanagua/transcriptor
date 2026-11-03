# Publicar una versión de Transcriptor

Las versiones para Windows x64 se generan automáticamente desde GitHub Actions al publicar una etiqueta cuyo nombre comience por `v`.

## Crear la primera versión de validación

Desde un clon actualizado de `main`:

```powershell
git switch main
git pull origin main
git tag -a v0.1.0-rc.1 -m "Transcriptor 0.1.0 release candidate 1"
git push origin v0.1.0-rc.1
```

La etiqueta `-rc.1` hace que GitHub marque la publicación como *pre-release*. Cuando la validación de hardware real esté completa, se podrá publicar una versión estable, por ejemplo `v0.1.0`.

## Qué hace el workflow

1. Compila grabador, transcriptor, configurador y aplicación WPF en Windows.
2. Ejecuta la suite automatizada de pruebas.
3. Publica la aplicación WPF y el configurador como binarios autocontenidos para `win-x64`.
4. Empaqueta los binarios y la documentación en `Transcriptor-win-x64.zip`.
5. Calcula SHA-256 del ZIP y publica ambos archivos en GitHub Releases.

Si falla la compilación o cualquier prueba, el job se detiene antes de crear la Release.

## Componentes que el usuario configura aparte

El ZIP incluye la aplicación y el configurador, pero no incluye `whisper-cli.exe` ni el modelo GGML. El usuario debe obtener una versión compatible de whisper.cpp y configurar sus rutas. Esto evita redistribuir binarios/modelos de terceros sin fijar y revisar sus licencias y versiones.

## Descarga

Las versiones publicadas aparecen en https://github.com/benitoanagua/transcriptor/releases.
