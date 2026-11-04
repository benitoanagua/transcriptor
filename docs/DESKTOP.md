# Aplicación de escritorio

Transcriptor usa Avalonia 12 sobre .NET 10 para su interfaz de Windows.

La ventana principal permite configurar la carpeta de grabaciones, seleccionar micrófono y salida, confirmar autorización, capturar ambas pistas, consultar la biblioteca, buscar transcripciones, reproducir pistas, transcribir sesiones pendientes y gestionar el ciclo de vida de las grabaciones.

La ventana Mini-Mode muestra el estado de grabación o transcripción y, en reposo, ofrece controles de reproducción. Sus preferencias de apertura, posición, tamaño y permanencia superior se guardan localmente.

La aplicación depende de WASAPI, NAudio, DPAPI, ACL NTFS y Media Foundation en Windows. Las operaciones de captura y cifrado permanecen en los proyectos compartidos de grabación y transcripción.
