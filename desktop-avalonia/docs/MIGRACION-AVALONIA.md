# Migración de Transcriptor: WPF a Avalonia 12

## Estado

Esta rama incorpora la base de arranque Avalonia en paralelo al escritorio WPF. No reemplaza WPF ni declara paridad funcional. La interfaz inicial identifica explícitamente las funciones aún no migradas.

## Decisiones de arquitectura

- Mantener el escritorio WPF intacto mientras conviven ambos proyectos.
- Usar .NET 10 y Avalonia 12.1.0 con versiones alineadas de los paquetes Avalonia.
- Mantener rec/ y transcribe/ como dependencias compartidas; la UI no reimplementa captura, cifrado ni transcripción.
- Compartir los archivos de lógica de aplicación que no dependen de WPF mediante enlaces de compilación desde desktop/.
- Mantener code-behind durante la primera fase para reducir el riesgo de regresiones. Extraer ViewModels con CommunityToolkit.Mvvm después de conseguir paridad.
- No habilitar Native AOT ni afirmar compatibilidad Linux: WASAPI, DPAPI, ACL NTFS y Media Foundation siguen siendo dependencias Windows.

## Puertas de validación

1. T0: restaurar y compilar Avalonia Release; compilar WPF, rec/, transcribe/ y setup/; ejecutar toda la suite xUnit.
2. T1: validar ventana, arrastre/redimensionado, temas, alto contraste, diálogos y enumeración de dispositivos en Windows.
3. T2: portar biblioteca, búsqueda, selección de sesión y visor de transcripciones.
4. T3: portar reproducción NAudio, controles de transporte, temporales descifrados y limpieza.
5. T4: portar captura, consentimiento, pausa, cierre diferido, transcripción/cancelación, migración, eliminación y retención.
6. T5: portar Mini-Mode, persistencia de geometría, estados y coordinación con la ventana principal.
7. T6: publicar Windows x64 y añadir smoke test real de arranque antes de empaquetar.
8. T7: extraer ViewModels, solo después de que T0-T6 estén verdes.

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

La primera ventana es únicamente una base de arranque y diseño. No ofrece grabación, reproducción, biblioteca ni transcripción. No se debe distribuir como reemplazo de WPF. El primer build de CI debe confirmar las APIs y el compilador XAML de la versión exacta de Avalonia.
