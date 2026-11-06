# Transcriptor: casos de uso

Fecha: 2026-11-05

Qué puede hacer la persona usuaria con la aplicación y dónde ocurre en la interfaz. Los requisitos están en `01-requisitos.md` y el comportamiento paso a paso en `03-flujos-y-estados.md`. Actor principal en todos los casos: la persona usuaria, salvo indicación.

## Casos de uso de la aplicación

| ID | Caso de uso | Precondición | Resultado y alternativas |
|---|---|---|---|
| CU-01 | Configurar el entorno | Whisper y modelo disponibles | Rutas válidas guardadas; «Probar Whisper» confirma el ejecutable. Alternativa: ejecutar el configurador (`setup`) |
| CU-02 | Probar niveles | Dispositivos detectados | Medición de ambas fuentes sin crear sesión ni guardar audio |
| CU-03 | Grabar una llamada | Autorización confirmada; carpeta con espacio | Sesión con dos pistas cifradas. Alternativas: pausar, reanudar, poco espacio |
| CU-04 | Gestionar un fallo de captura | Grabación en curso | Ambas pistas cerradas, sesión incompleta marcada, aviso claro; no se transcribe sola |
| CU-05 | Transcribir una sesión o las pendientes | Sesión completa; Whisper configurado | Resultados cifrados y verificados; sesión «Transcrita». Alternativa: Whisper no configurado |
| CU-06 | Cancelar o reintentar una transcripción | Transcripción en curso o fallida | Whisper terminado, temporales limpiados; reintento desde las pistas |
| CU-07 | Consultar y buscar en la biblioteca | Sesiones existentes | Lista filtrada y ordenada; estados vacíos explicados |
| CU-08 | Revisar una transcripción con audio | Sesión transcrita | Segmentos sincronizados con la reproducción; búsqueda interna |
| CU-09 | Exportar o copiar | Sesión transcrita | Archivo SRT, TXT o MD, o texto en el portapapeles |
| CU-10 | Eliminar una sesión | Sesión sin lease activo | Sesión borrada tras confirmar (no es borrado forense) |
| CU-11 | Aplicar retención | Retención mayor que 0 | Sesiones más antiguas que el plazo borradas tras confirmar |
| CU-12 | Migrar una sesión antigua a cifrado | Sesión en claro compatible | Copias cifradas verificadas y originales eliminados; reanudable |
| CU-13 | Diagnosticar con el registro | — | Registro visible, copiable y vaciable |
| CU-14 | Ajustar preferencias | — | Idioma, auto-transcripción, densidad y carpetas aplicados y persistidos |
| CU-15 | Cerrar la aplicación con operaciones en curso | Captura o transcripción activa | Advertencia; cierre coordinado de captura y cancelación de Whisper antes de salir |
| CU-16 | Grabar por consola | Autorización por teclado (`S`) | Sesión cifrada con ACL, igual que desde la aplicación |

## Trazabilidad a la interfaz

Cada caso de uso se presenta en una pantalla y con componentes Spectrum concretos.

| Caso de uso | Pantalla | Componentes Spectrum principales | Flujo de interacción |
|---|---|---|---|
| CU-01 Configurar el entorno | Ajustes | Text field, botón de examinar, Inline alert, Toast | FI-05 |
| CU-02 Probar niveles | Estudio | Action button, Progress, Status light | FI-01 |
| CU-03 Grabar una llamada | Estudio | Checkbox, Action buttons de transporte, Status light | FI-01 |
| CU-04 Gestionar un fallo de captura | Estudio, Biblioteca | Inline alert negativo, Badge | FI-01 |
| CU-05 Transcribir | Estudio, Biblioteca | Button primario, Progress bar, Toast | FI-02 |
| CU-06 Cancelar o reintentar | Estudio, Biblioteca | Alert dialog, Inline alert | FI-02 |
| CU-07 Consultar y buscar | Biblioteca | Search field, Picker, List view, estados vacíos | FI-03 |
| CU-08 Revisar con audio | Biblioteca | Slider, Action group, Picker de velocidad, lista de segmentos | FI-03 |
| CU-09 Exportar o copiar | Biblioteca | Menu, Toast | FI-03 |
| CU-10 Eliminar una sesión | Biblioteca | Alert dialog destructivo, Toast | FI-04 |
| CU-11 Aplicar retención | Ajustes | Number field, Alert dialog destructivo | FI-04 |
| CU-12 Migrar a cifrado | Biblioteca | Alert dialog, Progress, Toast | FI-04 |
| CU-13 Diagnosticar con el registro | Registro | Lista monoespaciada, Action buttons | FI-05 |
| CU-14 Ajustar preferencias | Ajustes | Picker, Switch, Radio group | FI-05 |
| CU-15 Cerrar con operaciones en curso | Ventana | Alert dialog | FI-04 |
| CU-16 Grabar por consola | Consola | No aplica a Spectrum | F-02 |
