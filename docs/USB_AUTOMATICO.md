# USB automático sin QR y apertura de fuente

## USB

La conexión USB ahora se inicia automáticamente cuando Windows encuentra un teléfono físico ADB autorizado y WT MultiCam está abierta en ese teléfono.

Requisitos: cable con transferencia de datos, Android SDK Platform-Tools (ADB), depuración USB habilitada y autorización del PC en la ventana del sistema Android. No hace falta estar en la misma Wi-Fi.

- Un PC ya recordado por la app Android se empareja por USB sin QR.
- Un PC nuevo pide “Autorizar y recordar” en Android una sola vez; no necesita un QR.
- La autorización ADB del sistema es independiente de la autorización de WT MultiCam.
- El programa conserva su identidad TLS en el almacén de certificados personales del usuario Windows. Esto permite que Android reconozca al mismo receptor después de reiniciar el programa.
- Android recuerda las huellas de los PCs autorizados explícitamente por QR, por comparación de código o por la confirmación USB.
- El primer uso tras esta actualización puede pedir confirmar el PC porque las versiones anteriores creaban una identidad temporal.
- La cámara continúa requiriendo “Iniciar cámara y enviar al PC”. Conectar el cable no activa la cámara.
- “Olvidar PCs USB” en Android revoca la lista de identidades recordadas. Desconectar manualmente o rechazar una autorización evita que ese PC vuelva a conectarse durante la instancia actual de la app; reiniciar Android WT MultiCam permite solicitar otra vez.
- El anclaje de red USB sigue siendo otra forma de transportar la conexión, pero la detección y conexión automática descrita aquí usa ADB.

Windows consulta los dispositivos cada pocos segundos, prepara reverse TCP 47779 y usa un forward temporal de TCP 47778 para enviar una invitación ligada al UUID del teléfono. El forward temporal se retira después de la operación. La invitación USB sólo se acepta desde loopback con cable físicamente conectado y se completa mediante TLS validando la identidad del PC. El receptor sólo acepta la autorización USB asociada a una invitación pendiente y desde el túnel loopback. Las invitaciones LAN mantienen su confirmación de código normal.

El estado USB se muestra en el panel. Si ADB no está instalado, el teléfono no está autorizado o la app no responde, se indica allí. El reverse permanece durante el uso y se puede retirar manualmente con adb -s NUMERO_SERIE reverse --remove tcp:47779.

Pendiente de validar con cable y teléfono reales, incluyendo el estado USB que reporta cada fabricante. No se promete USB plug-and-play sin ADB.

## Abrir fuente

El botón ya no depende del emparejamiento. Puedes abrir la ventana de salida de un dispositivo detectado y dejarla preparada:

- Sin emparejar: muestra que debes pulsar “Emparejar por QR” en el panel.
- Emparejado sin imagen: indica que debes iniciar la cámara desde Android.
- Con imagen: se ocultan las instrucciones y se muestra el video.
- Si se pierde el teléfono, una ventana abierta se mantiene en espera y reutiliza la misma identidad al reconectar.
- Si la ventana estaba minimizada, “Abrir fuente” la restaura.
- Se sustituyó el botón visible de solicitud por “Emparejar por QR”, priorizando el método que ya funciona.
- Los botones deshabilitados ahora se ven atenuados.

Abrir una ventana no autoriza ni inicia la cámara del teléfono.

## Comprobaciones

WPF compila, las clases Kotlin de conexión/captura/envío compilan por separado y los recursos Android compilan. Las comprobaciones sin sockets cubren el filtrado de dispositivos ADB físicos autorizados y los casos previos de video y protocolo. Se comprobó la apertura de una ventana sin emparejar y su restauración desde minimizada mediante el evento real del botón WPF.

El flujo USB físico y la persistencia real del almacén del usuario no se ejecutaron en el entorno de implementación. Los tests de protocolo usan identidades efímeras para no modificar el almacén de certificados. Hay que recompilar e instalar ambas aplicaciones para probar con el teléfono.
