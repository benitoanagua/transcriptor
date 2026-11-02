# Seguridad de datos locales

## Estado actual

La aplicación guarda WAV y SRT en claro. Las ACL NTFS protegen las carpetas de sesión frente a otras cuentas locales ordinarias, pero no cifran los archivos. El archivo de configuración no contiene claves. La ejecución de Whisper es local, aunque el motor necesita acceder a audio y produce SRT temporales o intermedios en disco.

Este documento define el contrato para incorporar cifrado en reposo. **No afirma que el cifrado esté implementado.**

## Objetivos y límites

- Proteger el contenido de audio y transcripciones frente a lectura del disco por otra cuenta sin acceso al perfil Windows autorizado.
- Mantener el procesamiento sin servicios remotos y sin almacenar contraseñas o claves maestras en `settings.json`.
- Detectar modificaciones, truncamientos, reordenamientos y sustituciones de bloques cifrados.
- Permitir que la aplicación enumere sesiones y gestione retención sin descifrar el contenido.
- Mantener compatibilidad explícita con sesiones heredadas en claro hasta que el usuario decida migrarlas.

No se promete protección frente al usuario autorizado mientras su sesión está desbloqueada, malware ejecutado bajo esa identidad, administradores/SYSTEM, acceso físico con capacidad de comprometer el dispositivo, volcados de memoria ni copias externas sin cifrar. DPAPI ligado al usuario también implica que perder el perfil/credenciales o migrar a otro usuario puede impedir recuperar los datos.

## Diseño criptográfico requerido

1. Cada archivo lógico recibe una clave de datos aleatoria e independiente de 256 bits (DEK).
2. La DEK se protege con Windows DPAPI en ámbito `CurrentUser`; nunca se guarda en claro ni se deriva de una contraseña de usuario.
3. El contenido se cifra con AES-256-GCM en un contenedor versionado y autenticado. El contenedor incluye identificador de formato, versión, tamaño de bloque, nonce base, DEK protegida y metadatos mínimos necesarios.
4. El cifrado se realiza por bloques independientes. Cada bloque usa un nonce único derivado de un prefijo aleatorio del archivo y un contador monotónico; el contador y el contexto del archivo se autentican como AAD. Rechazar contadores repetidos, fuera de orden, bloques incompletos y cualquier fallo de autenticación.
5. El formato debe permitir lectura secuencial y escritura incremental. No reutilizar nonces ni sobrescribir bloques cifrados con la misma clave/nonce.
6. Los nombres de archivo, fechas, duración aproximada y estructura de carpetas pueden seguir revelando metadatos; documentarlo como riesgo residual.

## Integración obligatoria

- La captura debe escribir directamente al contenedor cifrado desde el inicio de la sesión. No basta con cifrar los WAV después de detener: un cierre inesperado dejaría audio en claro.
- La transcripción debe descifrar a un área temporal privada con ACL restrictiva, invocar Whisper localmente y cifrar inmediatamente los SRT resultantes. Los temporales deben eliminarse en un bloque `finally`; el borrado es de mejor esfuerzo y no equivale a borrado seguro.
- La reproducción y el visor deben consumir contenido descifrado bajo demanda. Los archivos temporales de reproducción deben tener ACL y ciclo de vida acotado.
- Las operaciones de listado, borrado y retención deben reconocer el contenedor cifrado sin intentar descifrarlo. No seguir enlaces/reparse points.
- No escribir material secreto, texto de transcripción ni rutas sensibles en logs.
- Si DPAPI, autenticación de un bloque o escritura cifrada falla, detener la operación y no degradar silenciosamente a almacenamiento en claro.

## Migración de sesiones existentes

- Distinguir explícitamente sesiones heredadas en claro y sesiones cifradas.
- La migración debe ser opt-in, por sesión, reanudable e idempotente.
- Escribir primero un contenedor temporal, verificarlo descifrando y autenticando todos sus bloques, y solo entonces publicar el archivo cifrado mediante reemplazo atómico.
- No borrar el original hasta verificar el contenedor publicado. Informar claramente que el borrado posterior no es un borrado forense.
- No realizar migraciones automáticas ni borrar originales al actualizar la aplicación.

## Pruebas de aceptación

- Round-trip de archivos vacíos, pequeños, grandes y de múltiples bloques.
- Alteración de cabecera, nonce, DEK protegida, AAD, ciphertext, tag, orden y truncamiento: todo debe fallar cerrado.
- No reutilización de nonce entre archivos y entre bloques.
- Escritura interrumpida: el contenedor incompleto nunca se presenta como sesión válida ni se transcribe.
- DPAPI no disponible o perfil distinto: error explícito, sin crear archivos en claro.
- Captura, parada normal, cierre anormal, transcripción, reproducción, borrado y retención sobre sesiones cifradas.
- Migración interrumpida y reanudación sin pérdida del original.
- Mantener pruebas de compatibilidad para sesiones heredadas y para Windows sin privilegio de crear enlaces simbólicos.

## Secuencia de entrega

1. Implementar y probar el contenedor autenticado y la protección DPAPI de claves.
2. Integrarlo en captura desde el primer bloque de audio y en cierre/recuperación.
3. Integrarlo en transcripción, reproducción y visor mediante temporales privados.
4. Integrar biblioteca, borrado y retención; añadir migración opt-in.
5. Actualizar README y UX con el modelo de recuperación, límites y advertencias.

No habilitar una opción de cifrado parcial que pueda dejar nuevas grabaciones en claro sin indicarlo.