# Seguridad y privacidad

## Estado implementado

Transcriptor almacena las grabaciones localmente. Las nuevas pistas se escriben directamente en contenedores cifrados; las transcripciones terminadas también se cifran. Las claves de datos se protegen mediante Windows DPAPI en ámbito `CurrentUser`.

La captura, transcripción, reproducción, biblioteca y migración están integradas en la aplicación. Las sesiones heredadas en claro siguen siendo compatibles y solo se migran cuando el usuario lo solicita. La protección de carpetas usa ACL NTFS y rechaza enlaces o puntos de reanálisis en las rutas que protege.

Esto reduce la exposición de los datos almacenados, pero no convierte el equipo en un entorno seguro frente a un atacante con control del usuario o del sistema.

## Objetivos y límites

El diseño busca:

- Cifrar el contenido de audio y transcripciones almacenado en disco.
- Detectar modificaciones, truncamientos, reordenamientos y sustituciones de bloques cifrados.
- Vincular las claves al perfil Windows que las creó.
- Mantener la transcripción local y evitar enviar audio a servicios remotos.
- Restringir el acceso a carpetas de sesión frente a otras cuentas locales ordinarias.
- Mantener compatibilidad con sesiones heredadas hasta que el usuario decida migrarlas.

No protege frente a:

- El usuario autorizado mientras su sesión está desbloqueada.
- Malware ejecutado con la identidad del usuario autorizado.
- Administradores locales o SYSTEM.
- Acceso físico que permita comprometer el dispositivo o extraer datos de memoria.
- Copias externas, respaldos o exportaciones que no estén protegidos por separado.
- Exposición de metadatos como nombres, fechas, duración aproximada y estructura de carpetas.

DPAPI `CurrentUser` vincula la recuperación al perfil de Windows. La pérdida del perfil, credenciales o capacidad de descifrado puede volver irrecuperables las grabaciones. No hay una clave maestra de recuperación administrada por Transcriptor.

## Formato cifrado

El contenedor de archivos usa:

- Identificador `TRNCRYPT` y versión de formato.
- Bloques de hasta 64 KiB.
- Una clave de datos aleatoria e independiente de 256 bits por archivo.
- Protección de la clave mediante DPAPI `CurrentUser`.
- AES-256-GCM con tags de autenticación de 16 bytes.
- Un prefijo de nonce aleatorio por archivo y un contador monotónico para derivar el nonce de cada bloque.
- Autenticación de la cabecera y de cada bloque, incluyendo contexto, contador y longitud.
- Un terminador explícito; se rechazan datos adicionales después del terminador.

El formato de pista de audio `TRNAUD01` almacena la descripción del formato PCM/IEEE-float y las muestras dentro del contenido cifrado. El contenedor externo protege ese contenido.

Los cambios incompatibles al formato requieren versionado y pruebas de compatibilidad; no se debe reinterpretar silenciosamente un archivo existente.

## Captura, transcripción y temporales

- La captura escribe directamente `yo.trn` y `cliente.trn`. No debe reintroducirse un WAV en claro durante la captura.
- La transcripción descifra y autentica cada pista antes de entregarla como WAV temporal a NAudio/Media Foundation y Whisper.cpp.
- Whisper.cpp se ejecuta localmente como proceso nativo. La aplicación no usa shell para construir la línea de comandos.
- La reproducción y el visor descifran bajo demanda en temporales dentro de la carpeta de sesión protegida.
- Los temporales se eliminan mediante borrado normal de mejor esfuerzo. Una interrupción, un cierre forzado o un fallo del sistema puede dejar archivos en claro.
- El borrado normal no es borrado seguro ni forense.
- Los errores de DPAPI, autenticación o escritura deben fallar cerrados: no se debe degradar silenciosamente a guardar nuevas grabaciones en claro.

## ACL y rutas

Las carpetas de sesión reciben una DACL protegida que concede control total al usuario actual, SYSTEM y administradores locales. Antes de aplicar la ACL se valida la ruta y se inspecciona el contenido para rechazar enlaces y puntos de reanálisis.

Las ACL no sustituyen al cifrado y no protegen frente a las identidades que explícitamente conservan acceso. Las comprobaciones de rutas reducen el riesgo de redirección mediante enlaces, pero no eliminan todas las condiciones de carrera posibles del sistema de archivos.

## Sesiones heredadas y migración

Las sesiones antiguas pueden contener WAV, SRT y TXT en claro. No se migran automáticamente.

La migración por sesión:

1. Comprueba la ruta y rechaza enlaces o puntos de reanálisis.
2. Aplica la ACL de sesión.
3. Crea los contenedores cifrados para los archivos reconocidos.
4. Descifra y verifica autenticidad y contenido frente a los originales.
5. Solo después de verificar todos los archivos reconocidos presentes elimina los originales en claro.

La operación es reanudable: si se interrumpe, los contenedores ya publicados se verifican al volver a ejecutarla y los originales se conservan hasta que el conjunto queda verificado. El audio compatible es WAV PCM/IEEE-float; los formatos no compatibles permanecen intactos. La eliminación de originales no es borrado forense.

## Pruebas

La suite xUnit cubre contenedores, operaciones de cifrado, escritura incremental, pistas de audio, combinación SRT, ACL, privacidad, migración y almacenamiento de configuración. CI compila y ejecuta las pruebas en Windows.

La suite automatizada no sustituye pruebas manuales en equipos Windows reales para WASAPI, dispositivos de audio, Media Foundation, reproducción, Whisper.cpp, recuperación tras cortes de energía ni comportamiento del perfil DPAPI. No se debe afirmar que esos escenarios físicos han sido validados solo porque CI esté verde.

## Uso responsable

Antes de grabar, informa a todas las personas involucradas y confirma que tienes autorización. Define plazos de conservación, controla las copias de seguridad y elimina sesiones cuando ya no sean necesarias. Las obligaciones legales dependen de la jurisdicción y del contexto de uso.
