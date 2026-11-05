# Contribuir

## Requisitos y validación

Se requiere el SDK de .NET 10 indicado en `global.json`. Para compilar la solución completa y ejecutar las pruebas Windows, usa Windows:

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

## Convenciones

- Usa namespaces `Transcriptor.Tests` y `Transcriptor.Tests.Windows` para sus respectivas suites.
- Mantén nullable habilitado, sigue `.editorconfig` y deja sin warnings los builds Release.
- No añadas referencias desde `desktop-avalonia` a `desktop-dev`; los fakes y protecciones de desarrollo no deben llegar a producción.
- No subas grabaciones, transcripciones, contenedores `.trn`, ni datos o carpetas de sesiones. Estos archivos pueden contener información sensible.
