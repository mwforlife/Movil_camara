# Fluidez, vista previa Wi-Fi y cierre de ventana

El usuario confirmó que la distorsión por Wi-Fi aparecía sólo en el teléfono, mientras Windows recibía bien.

## Vista local Wi-Fi

El visor Wi-Fi ahora dibuja el mismo formato de frame JPEG y metadatos de orientación que utiliza Windows, en un Canvas con escala uniforme. No depende del transform de TextureView del fabricante. En Wi-Fi la sesión Camera2 usa sólo la salida ImageReader. La textura nativa deja de ser un destino de captura; así no se bloquea la cámara por una superficie invisible. USB conserva sus dos salidas.

La decodificación ocurre en un hilo dedicado con una sola imagen pendiente. La rotación y el espejo se aplican al dibujar, sin crear otra imagen rotada por cada frame. Los cuadros antiguos se descartan. El camino de vista previa USB confirmado por el usuario conserva su visor nativo.

La corrección debe verificarse en el teléfono: no se ha comprobado físicamente en este entorno.

## FPS

- Se elimina el descarte que imponía un máximo de unos 10 FPS en Android.
- Se solicita un rango de captura de 30 FPS si el dispositivo lo admite.
- La copia YUV ahora copia filas completas y usa arrays para el chroma intercalado; evita una llamada a ByteBuffer.get por cada píxel.
- Se reutilizan el buffer NV21, los buffers de filas y el buffer de compresión.
- Windows actualiza a intervalos de 33 ms, en lugar de 100 ms.
- Perfil USB: hasta 1280×720 según tamaños soportados.
- Perfil Wi-Fi fluido: hasta 960×540; puede elegir un tamaño menor disponible en el dispositivo.
- La calidad JPEG se ajusta entre límites según el tiempo de escritura y los FPS enviados. Una red lenta reduce calidad antes de seguir acumulando bytes.
- Se mantiene una cola de un frame, sin acumular una reproducción retrasada.

30 FPS es el objetivo de esta etapa, no un resultado medido ni garantizado. Siguen pendientes las mediciones físicas de CPU, temperatura, FPS y latencia. JPEG continúa siendo un formato de prototipo; video comprimido por hardware sigue siendo el siguiente salto de eficiencia.

## Ventana de video

- Barra visible con nombre del teléfono, Girar 90°, Modo OBS y Cerrar.
- Cerrar cierra la ventana; la cámara se detiene desde Android.
- Modo OBS oculta la barra. Mover el puntero sobre la imagen permite acceder a un cierre flotante.
- Escape cierra la ventana; clic derecho permite mostrar controles o restablecer el giro.
- El giro manual corrige el receptor de esa cámara, tanto en miniatura como en ventana, a partir del siguiente frame. No cambia el visor del teléfono.

## Comprobaciones

WPF compila. Las clases Kotlin y los recursos Android compilan por separado. Las pruebas cubren 160 combinaciones de geometría y conversión NV21 con planos contiguos, padding, chroma intercalado, última fila sin padding completo y preservación de posiciones de buffers. Se comprobó que el botón Cerrar sea visible y cierre la ventana.

Pendiente de comprobar: APK completo y visor Wi-Fi con el teléfono real, FPS antes/después, USB, cambios de cámara y captura en OBS.
