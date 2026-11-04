# Validación funcional en Windows

Esta guía recoge comprobaciones manuales que requieren dispositivos y una instalación real. Las pruebas automatizadas simulan Whisper y el normalizador, y comprueban la coordinación del cierre sin hardware; no sustituyen las pruebas de controladores WASAPI.

## Preparación

- Windows 10/11 con micrófono y dispositivo de salida activos.
- whisper-cli.exe y un modelo GGML configurados.
- Una carpeta de grabaciones de prueba, distinta de las grabaciones reales.
- Autorización de todas las personas que participen en la llamada.

## Captura dual

1. Grabar durante 60 segundos hablando por el micrófono y reproduciendo audio por la salida seleccionada.
2. Detener normalmente. Confirmar que aparecen yo.trn y cliente.trn y que ambas pistas se reproducen.
3. Repetir con una pista en silencio cada vez; confirmar que ambas pistas se cierran sin bloquear la interfaz.
4. Repetir iniciar/detener rápidamente diez veces. No deben quedar escritores abiertos ni archivos de sesión mezclados.
5. Durante una grabación, desconectar el micrófono y después la salida de audio en ensayos separados. Confirmar el aviso de error, el cierre de ambas pistas y el marcador .recording-incomplete.
6. Mientras graba, intentar cambiar dispositivos o iniciar otra grabación. La interfaz debe impedirlo.
7. Cerrar la aplicación durante una captura. Debe esperar el cierre coordinado y no presentar como completa una sesión fallida.

## Whisper y recuperación

1. Transcribir una sesión válida. Confirmar cuatro artefactos cifrados autenticables y ausencia de SRT/TXT en claro.
2. Cancelar otra transcripción durante el procesamiento. Confirmar la terminación de Whisper y la limpieza de WAV temporales normalizados.
3. Reintentar la misma sesión. Debe regenerar derivados sin reutilizar resultados parciales.
4. Desde una segunda instancia, intentar transcribir la misma sesión. La segunda instancia debe omitirla mientras exista un lease.
5. Mientras una sesión se transcribe, intentar migrarla o eliminarla desde otra instancia. La operación debe rechazarse sin modificar archivos.
6. Cerrar la ventana durante una transcripción. Debe cancelar Whisper, esperar su salida y luego cerrar.
7. Probar una sesión dañada o truncada. No debe mostrarse como completa ni producir un resultado combinado válido.

## Evidencia

Para cada escenario fallido, anotar el estado final, presencia de marcadores y nombres/tamaños de archivos resultantes, sin compartir audio ni transcripciones sensibles. Registrar versión de Whisper, modelo, dispositivos y controladores usados.

No utilizar llamadas reales ni datos personales. Las pruebas de desconexión deben hacerse solo en sesiones de ensayo.
