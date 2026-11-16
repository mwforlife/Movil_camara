# Audio, latencia y cámara virtual — 16-11-2026

**Actualización posterior:** el usuario confirmó audio correcto e instaló C++/Windows SDK. Ya hay proveedor nativo y administrador de cámara virtual implementados y comprobados localmente. Su registro elevado y enumeración real en OBS siguen pendientes. Consultar [estado actual y prueba](CAMARA_VIRTUAL.md); los diagnósticos de herramientas ausentes y proveedor no implementado siguientes son históricos.

## Estado real

El usuario confirmó que OBS recibe video, que el multiview le gusta y que el zoom funciona. Reportó retraso en OBS y pidió micrófono, proponiendo cámaras virtuales para usar Dispositivo de captura de video.

Esta revisión añade micrófono opcional y una salida local A/V con reloj explícito. **No registra cámaras ni micrófonos virtuales de Windows.** La integración final como dispositivos requiere un proveedor nativo todavía pendiente.

## Audio y salida actual para probar

1. Recompilar y actualizar Android y Windows. El APK completo necesita regenerar View Binding y recursos nuevos.
2. En Android marcar **Enviar micrófono del teléfono (opcional)** antes de iniciar. Autorizar el micrófono cuando Android lo solicite. Si se rechaza el permiso se desmarca la opción y continúa sólo video.
3. Iniciar cámara. El indicador muestra Micrófono activo o un error. Al detener, cerrar o enviar Android al fondo, se detienen cámara y micrófono.
4. En Windows pulsar **Copiar enlace OBS** de nuevo. Los enlaces nuevos llevan `&format=matroska`. En OBS usar Fuente multimedia, Archivo local desmarcado, pegar el enlace nuevo y cambiar Formato de entrada a **matroska**.
5. Establecer **Buffering de la red = 0 MB** para esta conexión loopback. El código oficial de OBS establece 2 MB por defecto y admite 0–16 MB. Reducir la cola del emisor no elimina un búfer configurado en OBS.
6. El audio llega en la misma fuente y aparece en el mezclador. Sin la opción activada se emite silencio; no se inicia grabación de micrófono por conectar OBS. Para evitar capturar la misma conversación desde varios teléfonos, elegir explícitamente cuáles envían micrófono.

Los enlaces antiguos sin format continúan ofreciendo MJPEG sin audio. No se cambian silenciosamente las escenas de OBS del usuario. El cambio de formato requiere reemplazar el enlace/configuración de la fuente.

### Implementación

- AudioRecord: PCM de 16 bits little-endian, 48 kHz mono, paquetes de 20 ms (1920 bytes), alrededor de 0,768 Mbps sin cabeceras.
- Cola Android de hasta cinco paquetes; captura y transporte separados, descartando audio antiguo ante congestión. Canal TLS independiente con el mismo UUID, secreto y certificado fijado del vínculo.
- Receptor valida protocolo, formato, longitud exacta y timestamps crecientes. No se graba PCM a disco ni se reproduce automáticamente en altavoces del PC.
- Matroska en vivo con MJPEG (`V_MJPEG`) y PCM (`A_PCM/INT/LIT`), clusters emitidos inmediatamente y timestamps de salida en milisegundos. Las marcas evitan depender de una frecuencia asumida por el demultiplexor MJPEG.
- Codificación de salida despierta al recibir cuadros en lugar de esperar un temporizador adicional de 33 ms. Se mantiene un cuadro pendiente, audio reciente acotado y se desconecta un lector bloqueado antes de acumular segundos. La cola H.264 Android baja de ocho a tres unidades de acceso, conservando recuperación por IDR; no se descartan deltas para seguir usando referencias inválidas.
- Sigue existiendo conversión H.264 → RGB → JPEG en el PC: no es la ruta final más eficiente. Es una mejora del transporte actual mientras se implementa el dispositivo virtual.
- El reloj del contenedor es común para A/V, pero no hay calibración del desfase físico entre captura de micrófono y cámara ni garantía de sincronía labial. Medir con palmada/estímulo común y comprobar sesiones prolongadas; la latencia real en OBS sigue pendiente.

## Cámara virtual: diagnóstico y siguiente implementación

Ejecutar `tools/CheckVirtualCamera.ps1` sólo consulta requisitos; no modifica registros ni instala dispositivos.

Resultado en este equipo:

| Requisito | Resultado |
|---|---|
| Windows build | 26200, proceso x64 |
| Exportación MFCreateVirtualCamera | Disponible |
| Cabecera Windows SDK mfvirtualcamera.h | No encontrada |
| Compilador MSVC C++ x64 | No encontrado por vswhere |
| Proveedor nativo de WT MultiCam | No implementado |
| Dispositivos creados en esta sesión | Ninguno |

Preparación necesaria: en **Visual Studio Installer → Modificar**, agregar **Desarrollo para el escritorio con C++** y **Windows 11 SDK**. No se instaló software ni se solicitó elevación desde este entorno.

Entrega nativa propuesta:

1. Proyecto DLL COM x64 con una fuente Media Foundation real (`IMFMediaSource` / `IMFMediaStream`), sin imágenes de prueba como salida final.
2. Puente de cuadros del receptor a la fuente, con última imagen disponible y timestamps QPC, evitando MJPEG HTTP para el dispositivo. Resolver acceso IPC desde el servicio Frame Server y ciclo de vida de buffers.
3. Administrador Windows con `MFCreateVirtualCamera`: una instancia estable por UUID de teléfono, nombres como WT MultiCam — Xiaomi y WT MultiCam — Motorola. Registro, retiro y reconexión controlados; el programa de OBS continúa independiente del multiview.
4. Instalación/desinstalación del componente y registro COM, con los permisos requeridos. No anunciar disponibilidad como Dispositivo de captura hasta comprobar enumeración y video reales en OBS.
5. Prueba de ambos dispositivos simultáneos, giros, cámara frontal, desconexiones, reinicios, CPU y latencia comparada.
6. La exposición de audio como dispositivo de entrada es otra integración: crear una cámara no registra un endpoint de micrófono. El envío PCM actual puede reutilizarse, pero todavía no hay micrófono virtual ni controlador de audio.

## Verificaciones locales

- Pruebas de audio: conservación PCM/timestamp, rechazo previo de longitud inválida y truncamiento.
- Prueba real con las DLL FFmpeg 62 instaladas con OBS: contenedor generado con imágenes y tono sintéticos, leído como 25 paquetes de video y 50 PCM, con timestamps crecientes y tamaño PCM correcto. No equivale a probar un teléfono ni el reproductor de OBS.
- Compilación Kotlin de captura, transporte, controles y audio y compilación de recursos por aapt2. APK Gradle completo, permisos y hardware siguen pendientes.

Referencias oficiales:
- https://learn.microsoft.com/en-us/windows/win32/api/mfvirtualcamera/nf-mfvirtualcamera-mfcreatevirtualcamera
- https://github.com/microsoft/Windows-Camera/tree/master/Samples/VirtualCamera
- https://github.com/obsproject/obs-studio/blob/master/plugins/obs-ffmpeg/obs-ffmpeg-source.c
- https://developer.android.com/reference/android/media/AudioRecord
- https://www.matroska.org/technical/codec_specs.html
