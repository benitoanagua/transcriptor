# Migración de Transcriptor: WPF a Avalonia 12

## Estado

La migración funcional de la interfaz Avalonia está implementada en paralelo al escritorio WPF. Los bloques de captura, biblioteca, reproducción, transcripción, operaciones de ciclo de vida y Mini-Mode están integrados en main. WPF se conserva como referencia y como aplicación heredada mientras se completa la validación del paquete y las pruebas funcionales en Windows. La compilación de CI no equivale a validar dispositivos WASAPI ni una instalación real de Whisper.

## Decisiones de arquitectura

- Mantener el escritorio WPF intacto mientras conviven ambos proyectos.
- Usar .NET 10 y Avalonia 12.1.0 con versiones alineadas de los paquetes Avalonia.
- Mantener rec/ y transcribe/ como dependencias compartidas; la UI no reimplementa captura, cifrado ni transcripción.
- Compartir los archivos de lógica de aplicación que no dependen de WPF mediante enlaces de compilación desde desktop/.
- Mantener code-behind durante la primera fase para reducir el riesgo de regresiones. Extraer ViewModels con CommunityToolkit.Mvvm después de conseguir paridad.
- No habilitar Native AOT ni afirmar compatibilidad Linux: WASAPI, DPAPI, ACL NTFS y Media Foundation siguen siendo dependencias Windows.

## Puertas de validación

1. T0: **completado por CI**. .NET 10 compila WPF, Avalonia, rec/, transcribe/ y setup/; ejecuta la suite xUnit.
2. T1: **pendiente de validación manual completa**. El arranque de la ventana se comprobará en CI con smoke test; arrastre, redimensionado, temas, alto contraste, diálogos y enumeración de dispositivos requieren equipo Windows interactivo.
3. T2: **implementado**. Biblioteca local, búsqueda, filtros, selección y estados de transcripción.
4. T3: **implementado**. Reproducción NAudio independiente por pista, transporte, temporales descifrados y limpieza.
5. T4: **implementado**. Captura dual con consentimiento, pausa, cierre coordinado, transcripción cancelable, migración, eliminación y retención manual.
6. T5: **implementado**. Mini-Mode, persistencia de geometría, estados, reproducción y coordinación con la ventana principal.
7. T6: **en validación**. El workflow Windows Release compilará y publicará ambas interfaces, ejecutará un smoke test de arranque Avalonia y verificará archivos ZIP y SHA-256. La prueba real de WASAPI/Whisper sigue siendo manual.
8. T7: **siguiente bloque**. Extraer ViewModels solo después de que T6 esté verde y se complete el smoke test funcional en Windows.

## Invariantes que no se pueden degradar

- Mantener el consentimiento explícito antes de capturar.
- Mantener ACL, validación de rutas, rechazo de reparse points y cifrado autenticado.
- Mantener el lease exclusivo de sesión para transcripción, migración y eliminación.
- No permitir operaciones destructivas sobre sesiones mientras se transcribe.
- El cierre de la ventana espera a que la captura termine de cerrar ambas pistas.
- Los archivos temporales en claro deben permanecer privados y limpiarse en finally.
- Callbacks de audio/procesos deben volver al hilo UI mediante el dispatcher de Avalonia.
- Los handlers asíncronos deben protegerse contra reentrada.

## Comandos de validación

    dotnet restore desktop-avalonia/Transcriptor.Desktop.Avalonia.csproj
    dotnet build desktop-avalonia/Transcriptor.Desktop.Avalonia.csproj -c Release
    dotnet build desktop/Transcriptor.Desktop.csproj -c Release
    dotnet test transcribe/tests/Transcriptor.Transcription.Tests.csproj -c Release

## Limitaciones conocidas

La aplicación Avalonia todavía comparte code-behind y no se declara reemplazo definitivo de WPF hasta completar la validación de distribución y el smoke test funcional en hardware Windows. La CI puede validar el arranque de la ventana y el empaquetado, pero no prueba micrófono, loopback, DPAPI en perfiles reales ni Whisper configurado por el usuario.
