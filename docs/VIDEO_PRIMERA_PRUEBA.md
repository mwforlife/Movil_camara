# Primera recepción de video — WT MultiCam

## Probar Android → Windows

1. Compila ambas aplicaciones actualizadas e instala Android en el teléfono.
2. Ejecuta WT MultiCam en Windows y abre “Agregar teléfono”.
3. Elige la IP del PC que alcance el teléfono y empareja escaneando el QR desde Android.
4. En Android pulsa “Iniciar cámara y enviar al PC” y concede permiso de cámara.
5. Debe aparecer una vista previa en el teléfono. Windows debe mostrar la imagen en la tarjeta de ese dispositivo.
6. Prueba “Cambiar cámara”, gira el teléfono y verifica orientación y espejo de la cámara frontal.
7. En Android pulsa “Detener”: debe cerrarse la captura y limpiarse la imagen del PC, conservando el emparejamiento.
8. Repite con un segundo teléfono y un QR nuevo. Cada teléfono debe tener su tarjeta e imagen independientes.

Esta entrega prioriza el QR que ya funciona. Las solicitudes de conexión desde Windows siguen presentes, pero no son necesarias para probar el video.

## Llevar la imagen a OBS

1. En la tarjeta del teléfono emparejado pulsa “Abrir fuente”.
2. Se abre una ventana de imagen con el título “WT MultiCam - nombre del teléfono - identificador”.
3. En OBS agrega una fuente “Captura de ventana” y selecciona esa ventana.
4. Repite para cada teléfono; puedes mover y combinar las fuentes en las escenas.
5. Mantén las ventanas de imagen abiertas y sin minimizar. Al cerrar una vista se cierra sólo esa ventana; la captura del teléfono continúa.
6. La ventana admite arrastre, doble clic para maximizar/restaurar, Escape o menú contextual para cerrar.

No hay todavía cámara virtual del sistema, complemento OBS nativo, micrófono virtual ni salida de audio. La captura de ventana es el puente inicial y debe validarse con OBS en el PC del usuario.

## Perfil de esta etapa

- Camera2 en Android, cámara trasera o frontal, con vista previa local.
- Se elige un tamaño soportado tanto por la vista previa como por la salida YUV, hasta 1280×720. Puede elegirse una resolución menor.
- Captura convertida a JPEG con calidad 70 y frecuencia objetivo máxima de 10 imágenes por segundo.
- Es una primera transmisión de imagen en movimiento, no el perfil final de producción 1080p30/60.
- Flujo binario autenticado dentro de TLS 1.2/1.3, usando el mismo certificado y credencial del emparejamiento.
- Una imagen pendiente por emisor y una por dispositivo en el receptor; se descartan imágenes antiguas.
- Sin grabación, sin audio y sin sincronización entre teléfonos.
- Android mantiene la pantalla encendida durante la transmisión y detiene cámara/envío al salir de la app.
- Los cambios de orientación se gestionan sin recrear la Activity; el PC recibe rotación y espejo por imagen.
- Se reintenta el canal de video ante un corte mientras la captura permanezca iniciada y la sesión siga válida. Si el PC se reinicia, vuelve a emparejar por QR.
- USB puede usar el túnel TLS ya preparado por ADB o anclaje USB. El transporte de video no necesita puertos nuevos.

## Protocolo de video v1

El teléfono abre una conexión TLS al puerto TCP 47779, valida la huella del PC y envía una línea JSON con action=video, id, name, secret, videoProtocol=1 y codec=jpeg.

El PC verifica la credencial existente y responde una línea JSON de aceptación con videoProtocol=1. El teléfono espera esa respuesta antes de enviar bytes de imagen.

Cada frame contiene, en orden:
1. Longitud del JPEG, entero de 32 bits big-endian.
2. Rotación: 0, 90, 180 o 270, entero de 32 bits big-endian.
3. Espejo horizontal: 0 o 1, entero de 32 bits big-endian.
4. Bytes del JPEG.

El PC limita cada frame a 1 MiB, valida cabecera, dimensiones JPEG y resolución antes de decodificar. Sólo admite una conexión de video activa por identidad; varios dispositivos mantienen flujos separados. Un timeout de aproximadamente 8 segundos sin frame cierra el receptor de ese flujo. La miniatura se limpia tras unos 3 segundos sin imagen.

Las estadísticas de FPS de Windows indican imágenes mostradas por la interfaz, no latencia de extremo a extremo ni FPS originales del sensor.

## Verificación realizada

- WPF compila sin errores ni advertencias.
- CameraController, VideoSender, PinnedTls y ConnectionManager compilan por separado con Kotlin 2.2.10 contra Android SDK 36.
- Los recursos XML/drawables Android compilan con aapt2.
- Las comprobaciones sin sockets validan lectura de un JPEG real de prueba, orientación/espejo del frame y rechazo de tamaños, flags, rotaciones, payload truncado y dimensiones fuera del perfil.
- El proyecto de comprobaciones incluye una prueba de dos emisores de video TLS y rechazo de video no autenticado; queda pendiente de ejecutar en un entorno que permita TCP local.
- Pendientes físicos: APK completo actualizado, acceso a cámaras reales, rendimiento/temperatura, rotación, dos teléfonos simultáneos, reconexión y captura en OBS. El entorno de implementación impide ejecutar Gradle y conexiones TCP locales.

La siguiente etapa de rendimiento debe reemplazar el transporte JPEG por video comprimido con aceleración de hardware, medir latencia y establecer perfiles de producción.
