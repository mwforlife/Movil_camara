# Corrección de vista previa y orientación

## Problema observado

El usuario confirmó recepción de video en Windows. En Android, las capturas mostraban la vista previa girada y deformada, tanto en vertical como en horizontal.

La transformación anterior trataba la textura como si fuera un buffer YUV sin orientar. Aplicaba de nuevo la orientación del sensor y un espejo frontal adicional. TextureView ya recibe compensación de sensor y espejo frontal del sistema; su transformación adicional debe corregir la rotación de pantalla y la escala.

## Cambios

- PreviewGeometry separa las matemáticas del visor de las coordenadas crudas de ImageReader.
- Se elimina la repetición de rotación del sensor y espejo frontal en la vista previa.
- Se revierte el estiramiento inicial de TextureView y se aplica una escala uniforme para encajar todo el cuadro.
- Se centra la imagen con bandas negras sólo donde la proporción del visor lo requiere.
- DisplayManager notifica cambios de pantalla, incluidos giros de 180° que no cambian las dimensiones.
- Los cambios de tamaño actualizan la transformación después de la distribución del layout.
- El encabezado muestra orientación y dimensiones visibles (p. ej., vertical 720×1280 u horizontal 1280×720).
- CameraScreenLayout mide los tres bloques definidos en XML: encabezado, visor y controles.
- En una ventana vertical, el encabezado queda arriba y los controles abajo. En una ventana ancha, los controles pasan a una columna lateral.
- Se conserva la misma superficie de captura al cambiar la distribución. No se reinicia la conexión por rotar el teléfono.
- El cálculo de rotación de frames enviados al PC permanece separado de la transformación de TextureView.

## Verificación

Compilación independiente de clases Kotlin contra Android SDK 36 y Kotlin 2.2.10: correcta. Compilación de recursos con aapt2: correcta.

tools/PreviewChecks/PreviewGeometryChecks.kt comprueba 160 combinaciones de buffers 16:9 y 4:3, sensores a 0/90/180/270°, pantallas a 0/90/180/270° y ventanas verticales, horizontales, cuadradas y estrechas.

Las comprobaciones verifican escala idéntica por píxel en ambos ejes, cuadro completo dentro del visor, centrado, proporción final, giro de 180° y ausencia de doble giro del sensor en la orientación natural.

Estas pruebas no sustituyen la comprobación con una cámara física ni una compilación completa del APK.

## Prueba en el teléfono

1. Recompilar y actualizar Android; no hace falta recompilar Windows por esta corrección.
2. Conectar e iniciar la cámara.
3. Apuntar a una cuadrícula, un marco de puerta o un objeto circular.
4. Comprobar que los cuadrados no se conviertan en rectángulos y los círculos no se conviertan en óvalos.
5. Girar el teléfono a horizontal hacia ambos lados: la imagen debe mantenerse derecha y los controles pasar al lateral.
6. Volver a vertical y cambiar entre cámara frontal y trasera.
7. Comprobar que el encuadre mantiene su proporción y que la cámara frontal conserva su comportamiento de espejo.
8. Comparar con el video recibido en Windows. El visor debe representar la misma orientación del encuadre; las bandas negras dependen del tamaño de cada ventana.

La rotación observada sigue la orientación que Android asigna a la pantalla. Si el sistema tiene bloqueada la rotación, la aplicación respeta esa orientación.

## Referencias oficiales

- https://developer.android.com/media/camera/camera2/camera-preview
- https://developer.android.com/codelabs/android-camera2-preview
- https://developer.android.com/reference/android/hardware/camera2/params/OutputConfiguration
