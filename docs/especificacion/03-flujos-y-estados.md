# Transcriptor: flujos y estados

Fecha: 2026-11-05

Cómo se comporta el sistema (estados, reglas y flujos F-01 a F-08) y cómo se presenta cada flujo en pantalla (FI-01 a FI-05). Los casos de uso están en `02-casos-de-uso.md` y las pantallas en `04-ux-ui-spectrum.md`.

## Estados de la sesión

| Estado | Se entra por | Se sale por |
|---|---|---|
| Grabando | Iniciar captura | Detener (a «Completa») o fallo (a «Incompleta») |
| Incompleta | Fallo de captura o escritura | Eliminar; no se transcribe automáticamente |
| Completa y pendiente | Detener con ambas pistas | Transcribir; migrar si está en claro; eliminar |
| Transcribiendo | Transcribir (lease exclusivo) | Éxito (a «Transcrita»), cancelación o error (a «Pendiente») |
| Transcrita | Resultados cifrados y verificados | Retranscribir desde las pistas; eliminar; exportar |
| En claro (heredada) | Sesión anterior al cifrado | Migrar (a «Cifrada») |
| Migrando | Migrar (lease exclusivo) | Éxito, o error con originales conservados y reanudación posterior |
| Eliminada | Eliminar o retención confirmadas | Estado final |

## Reglas de negocio

| ID | Regla |
|---|---|
| RN-01 | No se graba sin autorización confirmada |
| RN-02 | Una sesión incompleta nunca se presenta como completa ni se transcribe automáticamente |
| RN-03 | Las pistas son la fuente de verdad; transcripciones, SRT y TXT son derivados regenerables |
| RN-04 | Un original en claro solo se elimina cuando su copia cifrada está verificada |
| RN-05 | Una sola operación por sesión a la vez (lease), también entre instancias |
| RN-06 | Mientras se transcribe se bloquean la grabación y las acciones destructivas de la biblioteca |
| RN-07 | La retención con valor 0 está desactivada; el borrado ocurre solo tras «Aplicar retención» y confirmación |
| RN-08 | El borrado ordinario no equivale a borrado seguro |

## Flujos del sistema

**F-01 Primer uso (CU-01)**

1. La persona extrae el ZIP autocontenido y ejecuta `Transcriptor.Desktop.exe`.
2. La aplicación carga las preferencias o crea las predeterminadas (carpeta de grabaciones y rutas de Whisper desde `WHISPER_BIN` y `WHISPER_MODEL`).
3. Si falta Whisper o el modelo, el estado lo indica y se ofrece ir a Ajustes o ejecutar el configurador, que descarga y verifica `ggml-small.bin`.
4. En Ajustes la persona revisa las rutas y pulsa «Probar Whisper».
5. Con todo válido queda habilitada la transcripción; la grabación no depende de Whisper.

**F-02 Grabación (CU-02, CU-03, CU-16)**

1. El sistema enumera dispositivos y comprueba que existan micrófono y, si está disponible, salida para loopback.
2. La persona confirma la autorización y pulsa grabar; el sistema verifica el espacio libre frente al umbral.
3. Crea la carpeta `AAAA-MM-DD/HHMMSS_referencia`, aplica ACL y abre `yo.trn` y `cliente.trn` como contenedores cifrados.
4. Durante la captura mide nivel, pico, RMS, clipping y silencio, y actualiza el temporizador.
5. Al detener, cierra ambas pistas de forma coordinada, finaliza los contenedores y registra la sesión en la biblioteca.
6. Si la auto-transcripción está activa y la sesión es completa, continúa con F-04.

**F-03 Fallo de captura (CU-04)**

1. Un dispositivo se desconecta o falla una escritura.
2. El sistema detiene ambas pistas, escribe `.recording-incomplete` y muestra el aviso con la causa.
3. La sesión aparece como «Incompleta» y queda excluida de la transcripción automática.
4. La persona puede conservarla para inspección o eliminarla.

**F-04 Transcripción (CU-05, CU-06)**

1. La persona elige una sesión o «Transcribir pendientes».
2. El sistema adquiere el lease de la sesión; si otra instancia lo tiene, la omite.
3. Valida la ruta (sin enlaces ni puntos de reanálisis) y aplica ACL.
4. Descifra las pistas a WAV temporales autenticados y las normaliza a 16 kHz mono 16 bits.
5. Ejecuta `whisper-cli` por pista, sin shell, y combina los SRT.
6. Cifra SRT y TXT, verifica autenticidad y SHA-256, y solo entonces elimina los archivos en claro.
7. Libera el lease y actualiza el estado a «Transcrita».
8. **Cancelación:** termina el proceso de Whisper (incluido su árbol), limpia temporales y deja la sesión pendiente.
9. **Error:** conserva las pistas, informa y permite reintentar desde cero.

**F-05 Migración a cifrado (CU-12)**

1. La persona selecciona una sesión en claro y confirma.
2. El sistema adquiere el lease y crea contenedores cifrados de cada archivo reconocido.
3. Verifica autenticidad y contenido frente a los originales.
4. Si todo coincide, elimina los originales; si algo falla, los conserva y la operación puede reanudarse.

**F-06 Retención y eliminación (CU-10, CU-11)**

1. La persona define los días de retención (0 a 3650) y los guarda.
2. Al pulsar «Aplicar retención», el sistema calcula las sesiones que superan el plazo y las muestra en la confirmación.
3. Si confirma, elimina esas sesiones salvo las que tengan lease activo; si cancela, no cambia nada.
4. La eliminación individual sigue los mismos pasos con una sola sesión.

**F-07 Cierre de la aplicación (CU-15)**

1. La persona cierra la ventana con una captura o transcripción en curso.
2. El sistema pide confirmación indicando qué se interrumpirá.
3. Si confirma, espera el cierre coordinado de las pistas o la salida de Whisper, libera recursos y cierra; una captura fallida no se presenta como completa.

**F-08 Preferencias (CU-13, CU-14)**

1. La persona cambia ajustes; el sistema valida cada campo.
2. Al guardar, escribe `settings.json`; la densidad se aplica al instante.
3. El registro de sesión recoge los eventos relevantes, con tope de 2000 líneas.

## Flujos de interacción en pantalla

**FI-01 Grabación (CU-02, CU-03, CU-04; F-02, F-03)**

1. La persona abre **Estudio**. Se muestran estado «Listo», temporizador y selectores de micrófono y sistema.
2. Opcional: pulsa **Probar niveles**. Las formas de onda reaccionan y aparece «Medición sin guardar audio». Pulsa **Detener prueba**.
3. Escribe una **referencia** (opcional) y marca la casilla de **autorización**. Mientras no esté marcada, **Grabar** permanece deshabilitado y un texto de ayuda explica por qué.
4. Pulsa **Grabar** (`Ctrl+R`). El estado cambia a «Grabando» con Status light roja y texto; selectores y referencia quedan bloqueados.
5. Opcional: **Pausar** (`Ctrl+P`) y reanudar.
6. Pulsa **Detener**. Aparece un Toast «Sesión guardada» con la acción **Ver en biblioteca**.
7. **Fallo:** un Inline alert negativo «Se detuvo la captura» muestra la causa; la sesión queda marcada como incompleta.
8. **Auto-transcripción:** si está activa y la sesión es completa, se inicia FI-02.

**FI-02 Transcripción (CU-05, CU-06; F-04)**

1. Desde Biblioteca o Estudio, la persona selecciona una sesión y pulsa **Transcribir** (o **Transcribir pendientes**).
2. Si Whisper no está configurado, un Inline alert informativo ofrece **Ir a Ajustes** y el flujo se detiene.
3. Durante el proceso se muestra una Progress bar con etiqueta y porcentaje; grabar y las acciones destructivas quedan deshabilitadas con tooltip que explica el motivo.
4. **Cancelar** abre un Alert dialog de confirmación; si se confirma, se detiene el proceso.
5. Al terminar: Toast «Transcripción lista» y la insignia de la sesión cambia a «Transcrita».
6. **Error:** Inline alert negativo con **Reintentar** y **Ver registro**.

**FI-03 Revisión (CU-07, CU-08, CU-09)**

1. En **Biblioteca**, la persona busca (`Ctrl+F`), filtra por estado y ordena.
2. Selecciona una sesión: se muestra su detalle con insignias de estado y las acciones disponibles.
3. En el visor, pulsa un segmento o usa **Reproducir** (Mic, Sistema, Mezcla). `Espacio` pausa o reanuda. El segmento activo se resalta y el desplazamiento automático sigue la reproducción.
4. **Buscar en la transcripción** resalta coincidencias, con **Anterior** y **Siguiente**.
5. **Exportar** abre un menú con SRT, TXT y MD; **Copiar** envía el texto al portapapeles y muestra un Toast.

**FI-04 Acciones destructivas y cierre (CU-10, CU-11, CU-12, CU-15; F-05, F-06, F-07)**

1. La persona elige **Eliminar sesión**, **Aplicar retención**, **Migrar a cifrado** o cierra la ventana con una operación en curso.
2. Aparece un Alert dialog con título específico («Eliminar sesión»), la consecuencia («Se borrarán las pistas y la transcripción. No se puede deshacer») y dos botones: **Cancelar** y la acción nombrada con verbo («Eliminar»), en variante negativa cuando es irreversible.
3. `Esc` cancela; el foco inicial va a **Cancelar** en acciones irreversibles.
4. Tras confirmar, un Toast informa del resultado.

**FI-05 Configuración y registro (CU-01, CU-13, CU-14; F-01, F-08)**

1. **Ajustes** agrupa: Grabación (carpeta, retención, aviso de poco espacio), Transcripción (ejecutable, modelo, idioma, auto-transcripción), Apariencia (densidad) y Seguridad (estado del cifrado, migración).
2. Cada campo valida al perder el foco y muestra el error en línea.
3. «Hay cambios sin guardar» se presenta como Inline alert con **Guardar** y **Descartar**; **Probar Whisper** muestra su resultado en un Toast o Inline alert.
4. La densidad se aplica al instante.
5. **Registro** muestra la lista de solo lectura con **Copiar** y **Vaciar** (este último con confirmación).
