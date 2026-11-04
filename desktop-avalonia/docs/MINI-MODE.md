# Mini-Mode de Avalonia

La ventana Mini-Mode mantiene a la vista el estado de Transcriptor mientras la ventana principal permanece oculta.

## Apertura y preferencias

- El botón **MINI MODE** abre la ventana compacta manualmente.
- **Mostrar Mini-Mode automáticamente al grabar** controla su apertura al iniciar una captura. Está activado por defecto.
- El pin alterna **siempre visible**.
- La posición y el tamaño se guardan en la configuración local.
- **↗** vuelve a la ventana principal. **×** cierra Mini-Mode y también vuelve a la ventana principal; no detiene una captura activa.

## Durante la grabación

- Se muestran el tiempo transcurrido y los niveles de MIC y CLIENTE.
- **Pausar/Reanudar** afecta a la sesión dual.
- **Detener** solicita confirmación antes de cerrar ambas pistas.
- Atajos: **Espacio** pausa/reanuda; **Ctrl+M** expande la ventana; **Ctrl+Shift+S** solicita detener la captura.

## Durante la transcripción y en reposo

- La transcripción muestra el estado y permite cancelarla.
- En reposo, la ventana expone reproducción de MIC/CLIENTE, búsqueda temporal y detención.
- La reproducción conserva los mecanismos compartidos de lease, descifrado temporal y limpieza de recursos.

La compilación de CI valida la integración; la interacción real con audio, escalado y monitores debe probarse en Windows.
