# Validación adversarial en Windows con hardware real

Esta lista complementa GitHub Actions. Los tests automatizados simulan Whisper y el normalizador, y prueban la compuerta de cierre sin dispositivos; no pueden demostrar que un controlador WASAPI concreto se comporte bien.

## Preparación

- Windows 10/11 con micrófono y dispositivo de salida activos.
- whisper-cli.exe y un modelo GGML ya configurados.
- Una carpeta de grabaciones de prueba, distinta de las grabaciones reales.
- Confirmación de autorización de las personas que participen en la llamada.

## Captura dual

1. Iniciar una grabación de 60 segundos hablando por el micrófono y reproduciendo audio por la salida seleccionada.
2. Detenerla normalmente. Confirmar que aparecen yo.trn y cliente.trn, y que ambas pistas se reproducen.
3. Repetir con micrófono en silencio y audio de sistema activo; después con micrófono activo y salida en silencio. Confirmar que las dos pistas se cierran sin bloquear la interfaz.
4. Repetir iniciar/detener rápidamente diez veces. No deben quedar escritores abiertos ni archivos de sesión mezclados.
5. Durante una grabación, desconectar o deshabilitar el micrófono. Confirmar aviso de error, detención de ambas pistas y marcador .recording-incomplete. Repetir deshabilitando la salida de audio.
6. Mientras graba, intentar cambiar los dispositivos o iniciar una segunda grabación. La interfaz debe impedirlo.
7. Cerrar la aplicación durante una grabación. La ventana debe esperar el cierre coordinado y no anunciar una sesión completa si hubo error.

## Whisper y recuperación

1. Transcribir una sesión válida y confirmar que se generan los cuatro artefactos .trn, que se autentican y que no quedan SRT/TXT en claro.
2. Iniciar otra transcripción y pulsar Cancelar transcripción durante el procesamiento. Confirmar estado CANCELADO, terminación del proceso y ausencia de archivos *.normalized.wav.
3. Reintentar la misma sesión. Debe regenerar los derivados sin reutilizar resultados parciales.
4. Abrir una segunda instancia de Transcriptor y pedirle transcribir la misma sesión. La segunda instancia debe omitirla mientras el primer proceso mantenga el lease.
5. Mientras una sesión está siendo transcrita, intentar migrarla o eliminarla desde otra instancia. La operación debe rechazarse sin modificar los archivos.
6. Cerrar la ventana durante una transcripción. Debe cancelar Whisper, esperar su salida y luego cerrar.
7. Probar una sesión dañada o truncada. No debe mostrarse como transcripción completa ni producir un resultado combinado válido.

## Evidencia a conservar

Para cada escenario fallido, guardar:
- Mensajes de la pestaña Actividad.
- Estado final de la sesión y presencia/ausencia de .recording-incomplete.
- Nombres y tamaños de los archivos resultantes, sin compartir audio ni transcripciones sensibles.
- Modelo Whisper, versión de whisper-cli, dispositivos y controladores usados.

No uses llamadas reales ni datos personales en esta validación. Las pruebas de desconexión de dispositivos deben hacerse solo en sesiones de ensayo.