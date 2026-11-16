# Alta velocidad, icono y vínculos persistentes

> Estado posterior: el usuario aplazó 60 FPS tras el error de superficie 1202x2005; la ruta de alta velocidad se retiró del selector/captura. La versión actual ofrece hasta 30 FPS. La sección de alta velocidad siguiente conserva el historial del intento, no describe una función habilitada. Icono y persistencia se mantienen.

Implementación: 16-11-2026. Validación física de esta versión: pendiente.

## Alta velocidad

El diagnóstico del Xiaomi 25080RABDG / Android 16 indica cámara 0 normal hasta 30 FPS, superficies 1080p con duración mínima de 33 333 333 ns, encoder AVC compatible con 1080p60 y sesión de alta velocidad con rangos [30,120] y [120,120].

La app conserva los perfiles normales. Cuando no existe un perfil normal de 60 FPS, ofrece H.264 a 60 con alta velocidad si Android es 10 o posterior, la cámara anuncia esa capacidad y un rango fijo compatible para ese tamaño, el visor admite el tamaño y un encoder hardware admite Surface y salida 60. Se elige la menor frecuencia fija múltiplo de 60 entre 120 y 240.

En el Xiaomi se espera «1920 × 1080 · 60 FPS · H.264 · alta velocidad (sensor 120)». La sesión usa `createConstrainedHighSpeedCaptureSession`, dos superficies del mismo tamaño y `createHighSpeedRequestList` / `setRepeatingBurst` con [120,120]. El encoder mantiene `KEY_FRAME_RATE=60` y `KEY_MAX_FPS_TO_ENCODER=60`: limita la entrada Surface antes de generar H.264, conservando timestamps en tiempo real. No se descartan paquetes delta para convertir 120 a 60. No es grabación en cámara lenta.

El visor puede actualizar a una frecuencia inferior al video. El sensor a 120 puede aumentar consumo y temperatura. El perfil anunciado y la compilación no prueban una salida sostenida de 60 FPS; medir Android y Windows. Si falla la apertura o hay temperatura/latencia excesiva, detener y volver a 1080p30. No se fuerza un rango que no esté anunciado.

Referencias oficiales:
- https://developer.android.com/reference/android/hardware/camera2/CameraConstrainedHighSpeedCaptureSession
- https://developer.android.com/reference/android/media/MediaFormat#KEY_MAX_FPS_TO_ENCODER

## Icono

El manifiesto usa `@mipmap/wt_launcher` para icono normal y redondo. Android 8+ utiliza un icono adaptativo con el emblema local de WilkensTech y fondo de marca; Android 7 recibe la versión de respaldo. Se conserva el PNG original y se deja margen para las máscaras del launcher. No usa el robot Android de la plantilla.

## Configuración recordada

Android guarda la última cámara y el último perfil usado al iniciar video, separados por identidad del PC, USB/Wi-Fi y cámara. Sólo restaura perfiles que continúan en la lista actual; si desaparecen, vuelve al perfil preferido disponible (1080p30 H.264 cuando existe).

## Vínculo y reconexión

Actualizar Android y Windows. Tras esta actualización, emparejar una vez por QR o autorizar USB: las versiones anteriores no guardaban el secreto de sesión en ambos extremos.

- Android guarda hasta ocho destinos recientes, con secretos cifrados AES-GCM y clave no exportable de Android Keystore. Los vínculos y la identidad del teléfono se excluyen del backup/traspaso para evitar clonar dispositivos.
- Windows guarda hasta 128 secretos por UUID de teléfono en `%LOCALAPPDATA%\WilkensTech\WTMultiCam\connections.json`, cifrados RSA-OAEP con la identidad persistente del receptor en el almacén de certificados del usuario. Un archivo de otro PC/certificado o corrupto no autoriza dispositivos.
- Con ambas aplicaciones abiertas, Android intenta un destino guardado por ciclo de heartbeat. USB necesita ADB autorizado y sus túneles; la automatización del receptor sigue preparándolos.
- Si cambia la IP del PC, los anuncios de descubrimiento aportan direcciones candidatas. Cada intento debe superar TLS con la huella ya autorizada y autenticar el secreto; el descubrimiento por sí solo no autoriza PCs.
- Un PC autorizado inicialmente por USB puede recuperarse por Wi-Fi al ser descubierto, usando el mismo vínculo. No hay captura automática: se pulsa Iniciar cámara para transmitir.
- Desconectar suspende los reintentos durante la vida de esa instancia de la app. Un nuevo emparejamiento o reiniciar la app permite volver a conectar. Olvidar PCs vinculados borra secretos y confianza en el teléfono y suspende los reintentos; es necesario autorizar de nuevo.
- Cambiar o perder el certificado del receptor, borrar datos o reinstalar sin conservarlos requiere un nuevo emparejamiento. No hay reconexión por Internet ni ejecución permanente en segundo plano.

## Prueba física siguiente

1. Cerrar el receptor antes de recompilarlo en Visual Studio; actualizar ambos programas sin borrar los datos de Android.
2. Emparejar una vez; elegir 1080p60 H.264 de alta velocidad y probar por USB cinco minutos. Anotar FPS enviados/presentados, retraso y temperatura. Repetir por Wi-Fi.
3. Detener/reiniciar en vertical y horizontal; comprobar que no reaparece la distorsión.
4. Seleccionar otro perfil, iniciar y detener. Cerrar y abrir Android y Windows: verificar vínculo y perfil recordados sin QR.
5. Cortar y recuperar Wi-Fi; desconectar/reconectar USB; probar cambio de IP del PC y pasar de USB autorizado a Wi-Fi.
6. Comprobar que Desconectar y Olvidar no reconectan contra la voluntad del usuario y que nunca se inicia la cámara automáticamente.
7. Verificar el icono en el launcher y probar dos teléfonos por separado antes de una sesión simultánea.
