# WT MultiCam — WilkensTech Corp

**Para continuar el desarrollo:** leer [plan/PLAN.md](plan/PLAN.md), revisar [TODO](plan/TODO.md) y registrar resultados en [PRUEBAS](plan/PRUEBAS.md).

Proyecto de cámara Android para producción multicámara desde Windows.

Estado: interfaces Android (Kotlin + XML) y Windows (WPF, .NET 8), descubrimiento LAN, búsqueda por IP, emparejamiento TLS por QR/código y USB automático sin QR para PCs autorizados mediante ADB. Incluye H.264 por TLS con perfiles reportados por cámara/codificador hasta 1920×1080 a 60 FPS, JPEG de compatibilidad, y selección de resolución/FPS, sin audio, vista previa por teléfono y ventanas independientes para probar Captura de ventana en OBS. La cámara virtual y el complemento OBS nativo siguen pendientes. Consulta [la prueba de video](docs/VIDEO_PRIMERA_PRUEBA.md). Consulta [conexiones y pruebas](docs/CONEXIONES.md).

## Proyectos

Nombre del producto: **WT MultiCam**, de WilkensTech. Los identificadores internos y nombres de solución existentes se conservan.

### Organización Android

- `app/src/main/res/layout/activity_main.xml`: diseño de la pantalla, editable desde el diseñador de Android Studio.
- `app/src/main/res/values/strings.xml`: textos de la aplicación.
- `app/src/main/res/values/brand_colors.xml` y `text_styles.xml`: identidad visual compartida.
- `app/src/main/res/drawable/`: fondos e iconos.
- `MainActivity.kt`: ciclo de vida, adaptación a barras del sistema y eventos mediante View Binding; no construye controles visuales en código.

Al implementar transmisión, separar captura, transporte y estado en clases dedicadas; evitar concentrar la lógica multimedia en la Activity.

- Android: proyecto Gradle en la raíz, módulo `app/`. Abrir esta carpeta en Android Studio.
- Windows: `Escritorio/MovilCamara/MovilCamara.sln`. Abrir en Visual Studio.
- Identidad: emblema y logo copiados desde `wilkenstech/images/logo/`. Las aplicaciones usan copias locales y no dependen del sitio para cargar recursos.

El panel muestra dispositivos detectados, sesiones emparejadas e imagen recibida de las cámaras que el usuario inicie explícitamente. Android conserva el mínimo API 24 generado por el usuario; el soporte efectivo se definirá con pruebas. El proyecto usa Kotlin integrado en AGP 9.

El objetivo es conectar varios teléfonos al mismo PC y ofrecer cada uno como fuente independiente para streaming, con controles remotos y diagnóstico centralizados.

Consulta [el plan de producto y desarrollo](docs/PLAN_PRODUCTO.md).

Consulta [USB automático y apertura de fuente](docs/USB_AUTOMATICO.md) para los cambios de conexión por cable y la ventana en espera.

Consulta [H.264, perfiles y prueba 1080p60](docs/H264_1080P60.md) para el estado y límites actuales.
