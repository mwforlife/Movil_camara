# Salida directa a OBS — 16-11-2026

**Revisión vigente:** enlaces nuevos usan `&format=matroska`, video con timestamps y audio PCM opcional. En OBS establecer Formato de entrada **matroska** y Buffering de la red **0 MB**. Los enlaces antiguos conservan MJPEG sin audio. Ver [audio, latencia y cámara virtual](AUDIO_LATENCIA_Y_CAMARA_VIRTUAL.md); las instrucciones MJPEG siguientes corresponden a compatibilidad/historial. La cámara virtual como dispositivo continúa pendiente.

La salida actual usa una **Fuente multimedia por teléfono**, sin Captura de ventana, monitor ni HWND. No necesita abrir VideoOutputWindow. No instala webcam virtual ni complemento propio: usa el lector multimedia incluido en OBS.

## Uso

1. Cerrar el receptor anterior y recompilar Windows. Actualizar Android para retirar los perfiles 60 FPS.
2. Conectar un teléfono por USB o Wi-Fi e iniciar un perfil de hasta 1080p30.
3. En WT MultiCam pulsar **Copiar enlace OBS** para ese teléfono.
4. En OBS añadir **Fuente multimedia**, asignarle un nombre y desmarcar **Archivo local**.
5. Pegar el enlace en **Entrada**; escribir **mpjpeg** en **Formato de entrada**.
6. Mantener WT MultiCam abierto. La salida funciona sin abrir ni capturar sus ventanas; se puede minimizar el receptor.
7. Para otro teléfono, copiar su enlace y crear otra fuente. No reutilizar el enlace de la primera cámara.

Conservar el enlace guardado en la escena. Es estable mientras se conserve el UUID de Android y la clave local del receptor. Si se detiene la cámara, se presenta negro; al volver a enviar, se recupera en la misma conexión. Si se reinicia el receptor, OBS debe reconectar la fuente; comprobar el comportamiento real y usar Reiniciar reproducción/reactivar fuente si su versión no lo hace automáticamente.

## Implementación y límites

- `ObsDirectServer` escucha exclusivamente en `127.0.0.1:47780`. Cada UUID tiene ruta y token HMAC independientes. La clave aleatoria se conserva en `%LOCALAPPDATA%\WilkensTech\WTMultiCam\obs-output.key`; no compartir enlaces ni incluirlos en diagnósticos.
- Los cuadros provienen de `ConnectionHub.FrameReceived`, no de la imagen renderizada por WPF. El codificador de salida trabaja fuera del hilo UI y sólo procesa cámaras con lectores conectados. Mantiene un cuadro pendiente por cámara, sin cola acumulativa.
- Primera implementación: HTTP multipart MJPEG, hasta 30 FPS, con máximo 16 lectores simultáneos. La última imagen se sustituye por negro tras tres segundos sin cuadros o cuando se notifica detener.
- JPEG sin giro/espejo se reutiliza. H.264 se decodifica como antes y la salida local se convierte a JPEG (calidad 85), aplicando orientación y espejo. Esto agrega trabajo de CPU y compresión con pérdida respecto de una ruta H.264 directa o textura compartida; medir latencia/calidad y CPU antes de ampliar el número de teléfonos.
- El visor de WT MultiCam sigue disponible en el panel para control, pero no participa en la salida a OBS. Minimizar o cubrir la ventana no debe cortar la salida; verificar físicamente.
- No hay audio, webcam de Windows ni garantía de compatibilidad con otros programas. Un complemento OBS propio/texturas compartidas y cámara virtual quedan como fases posteriores.
- Si el puerto está ocupado o no se puede iniciar la salida, la tarjeta muestra **No disponible**. El receptor de teléfonos puede seguir funcionando; no se hace una sustitución automática por captura de pantalla.

## Validación

Estado local: WPF compila sin errores/advertencias; 25 comprobaciones de protocolo/persistencia y cuatro del conversor de imágenes aprobadas. La prueba HTTP real fue bloqueada por Winsock 10013; OBS aún no está validado con esta salida.

- Compilación WPF y pruebas locales del protocolo/enlaces se registran en `plan/PRUEBAS.md`.
- Comprobación HTTP sin OBS: `dotnet run --project tools/ConnectionChecks/ConnectionChecks.csproj -- --obs-output`. Necesita puerto 47780 libre y permiso para sockets locales. Comprueba dos fuentes, aislamiento, retirada del cuadro al detener y rechazo de claves erróneas.
- Pendiente real en OBS: imagen USB/Wi-Fi, vertical/horizontal y frontal, minimizar receptor, detener/reiniciar cámara, reiniciar receptor, dos teléfonos y cambios de escena. Medir FPS, latencia y CPU; no dar por validada la integración sólo porque compile.

Referencias oficiales:
- https://obsproject.com/kb/sources-guide
- https://ffmpeg.org/ffmpeg-formats.html#mpjpeg
