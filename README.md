# WT MultiCam — WilkensTech Corp

**Instalador Windows (18-11-2026):** paquete beta x64 en [Instaladores/Windows](Instaladores/Windows/WTMultiCam-Setup-1.0.0-beta.1-win-x64.exe), con .NET/WPF incluido, ADB, proveedor nativo, asistente personalizado y accesos directos. No requiere descarga .NET aparte. [Guía y compilación](Instaladores/README.md). Compilación y carga local comprobadas; falta instalación/actualización/desinstalación en Windows 11 limpio. Android fuera del alcance final de esta entrega: el usuario ya generó APK.

**Corrección de diseño móvil:** vista previa local real 16:9 antes de transmitir; fila fija con iconos Micrófono, Cámara y Controles durante el envío, con Detener debajo. El micrófono puede autorizarse desde la transmisión. Recursos/Kotlin compilados; recompilar Android y validar en dispositivo. Ver [diseño vigente](docs/DISENO_MOVIL.md).

**Nuevo diseño Android (18-11-2026):** conexión, preparación y transmisión con identidad azul/celeste, controles desplegables y parada roja separada. Recursos y Kotlin comprobados; recompilar APK en Android Studio y validar en el teléfono. Ver [recorrido y validación](docs/DISENO_MOVIL.md). Los cortes Wi-Fi siguen pendientes.

**Wi-Fi (18-11, posterior a confirmación USB):** Android incorpora adaptación del bitrate H.264 ante presión de envío sólo por red; conserva resolución/FPS y reduce detalle cuando es necesario. Requiere recompilar Android. APK y mejora física pendientes; USB fue confirmado más fluido con el receptor `build/fluency-desktop`. Ver [plan vigente](plan/PLAN.md).

**Fluidez, revisión posterior (18-11-2026):** para saltos con conexión USB mantenida, recuperación H.264 conserva referencias y evita convertir a RGB salidas atrasadas. Cola acotada de ocho paquetes sustituye tres. Receptor en `build/fluency-desktop`, sólo Windows; mejora física pendiente de confirmar. No implica aceleración GPU.

**Estabilidad de video (18-11-2026):** ante retraso y desconexiones reportados en USB y Wi-Fi, el receptor desacopla lectura H.264 y decodificación mediante cola acotada con recuperación por IDR. Esta revisión requiere actualizar sólo Windows. Validación física pendiente; probar `build/stability-desktop/MovilCamara.exe`. El estado vigente y los pasos de diagnóstico están en [plan](plan/PLAN.md).

**Sincronización manual (18-11-2026):** retardo de audio por teléfono de 0 a 1000 ms, pasos de 20 ms, restablecimiento y guía de palmada en el multiview. Actualizar sólo Windows; valores temporales mientras el receptor está abierto. Si el audio llega tarde, retrasar el video en OBS. Compilación y pruebas locales correctas; falta calibración física. Ver [guía](docs/SINCRONIZACION_AUDIO.md). Cortes/fluidez siguen pendientes; estado vigente en [plan](plan/PLAN.md).

**Controles de audio en multiview (17-11-2026):** medidor de salida, volumen 0-100 %, silencio y micrófono remoto por teléfono. Requiere actualizar ambas apps para iniciar/detener captura desde Windows; autorización Android por sesión. El usuario confirmó recepción de la fuente independiente y reportó retraso/cortes, cuya investigación se aplaza por su instrucción. Ver [uso y verificación](docs/CONTROLES_AUDIO_MULTIVIEW.md).

**Audio independiente (17-11-2026):** botón Copiar audio OBS por teléfono; fuente Matroska sólo PCM para acompañar la cámara virtual sin otra transmisión de imágenes. Recompilar sólo Windows y configurar la nueva Fuente multimedia. Compilación y formato comprobados; prueba HTTP bloqueada por el entorno y validación física de la fuente nueva pendiente. Ver [instrucciones](docs/AUDIO_INDEPENDIENTE.md) y [plan actualizado](plan/PLAN.md). Próximo desarrollo: controles de audio en el multiview, luego sincronización e instalador.

**Cámara virtual (revisión actual):** proveedor nativo Windows implementado, salida independiente 1080p30 NV12 por teléfono y botones Activar/Desactivar en el receptor. C++/Windows SDK instalados y compilación comprobada. Recompilar sólo Windows; la primera activación requiere autorizar el registro del proveedor. Falta validar el dispositivo real en OBS. El audio sigue disponible por Matroska, ya confirmado por el usuario. Ver [instalación y pruebas de cámara virtual](docs/CAMARA_VIRTUAL.md). Esta revisión sustituye las referencias históricas siguientes a proveedor o herramientas pendientes.

**Audio y latencia:** micrófono opcional y nueva salida local Matroska A/V. Copiar enlace nuevo en OBS, Formato de entrada matroska y Buffering de la red 0 MB. Compilación/contenedor comprobados localmente; validación física pendiente. La integración como cámara virtual por teléfono está diagnosticada y pendiente de proveedor nativo y herramientas C++/SDK. Ver [estado y siguientes pasos](docs/AUDIO_LATENCIA_Y_CAMARA_VIRTUAL.md).

**Multiview:** botón Abrir multiview en el receptor, cuadrícula ajustable y controles remotos según capacidades. Actualizar ambas apps; prueba física pendiente. Ver [multiview y controles](docs/MULTIVIEW_Y_CONTROLES.md).

**Salida vigente a OBS:** conexión directa local por teléfono mediante **Fuente multimedia**, botón **Copiar enlace OBS**, sin Captura de ventana. Ver [salida directa](docs/SALIDA_OBS_DIRECTA.md). La app Android ofrece hasta 30 FPS; 60 FPS queda aplazado por instrucción del usuario. Los párrafos históricos siguientes sobre 60 FPS y salida por ventanas quedan sustituidos por este estado.

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
