# Contribuir

## Requisitos y validación

Se requiere el SDK de .NET 10 indicado en `global.json`. Todo el tooling (generadores, pruebas, verificaciones, pasos de CI) es C# sobre .NET 10; no se usa Python, Node.js, npm ni shells.

```powershell
dotnet build Transcriptor.slnx -c Release
dotnet test tests/Transcriptor.Tests/Transcriptor.Tests.csproj -c Release
dotnet test tests/Transcriptor.Tests.Windows/Transcriptor.Tests.Windows.csproj -c Release
```

En Linux/WSLg, trabaja solo con los proyectos portables; WASAPI, DPAPI, ACL NTFS y las pruebas específicas de Windows se validan en Windows:

```bash
dotnet build transcription/Transcriptor.Transcription.csproj -c Release
dotnet build ui/Transcriptor.UI.csproj -c Release
dotnet build desktop-dev/Transcriptor.Dev.csproj -c Release
dotnet test tests/Transcriptor.Tests/Transcriptor.Tests.csproj -c Release
dotnet run --project desktop-dev/Transcriptor.Dev.csproj
```

Para iterar en la interfaz de desarrollo: `dotnet watch --project desktop-dev/Transcriptor.Dev.csproj`.

### Datos de diseño de Spectrum 2 (Tokens e Iconos)

Los ficheros versionados `ui/Theming/Tokens.axaml` y `ui/Theming/Icons.axaml` se generan desde los paquetes oficiales fijados en `tools/Transcriptor.DesignData/Pins.cs` (`@adobe/spectrum-tokens@15.6.0`, `@fluentui/svg-icons@1.1.345` y `@spectrum-css/ui-icons@1.1.2`), descargados del registro de npm y verificados con SHA-512:

1. `dotnet run --project tools/Transcriptor.Tools -- fetch` — descarga (o reutiliza la caché verificada en `artifacts/design-data/`) y comprueba el SHA-512 de cada paquete.
2. `dotnet run --project tools/Transcriptor.Tools -- tokens` — regenera `ui/Theming/Tokens.axaml` desde la caché.
3. `dotnet run --project tools/Transcriptor.Tools -- icons` — regenera `ui/Theming/Icons.axaml` desde la caché.

Las pruebas `TokenRegenerationTests` e `IconRegenerationTests` comparan la salida del generador con los archivos versionados y fallan si la caché falta (mensaje con el comando `fetch` anterior). Las pruebas nunca usan la red.

## Convenciones

- Usa namespaces `Transcriptor.Tests` y `Transcriptor.Tests.Windows` para sus respectivas suites.
- Mantén nullable habilitado, sigue `.editorconfig` y deja sin warnings los builds Release.
- No añadas referencias desde `desktop-avalonia` a `desktop-dev`; los fakes y protecciones de desarrollo no deben llegar a producción.
- No subas grabaciones, transcripciones, contenedores `.trn`, ni datos o carpetas de sesiones. Estos archivos pueden contener información sensible.
