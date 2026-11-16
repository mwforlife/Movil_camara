## Confirmación del usuario: cámaras virtuales en OBS - 16-11-2026

- Usuario confirma que la activación ahora funciona después de la corrección de 0xC00D3E85.
- Capturas: multiview con Motorola moto e30 y Xiaomi 25080RABDG vinculados, ambas miniaturas con imagen; Xiaomi muestra 1920x1080 y 27 FPS en pantalla. OBS muestra dos fuentes de Dispositivo de captura de video e imágenes distintas simultáneas en la vista previa. Esto confirma el uso real básico; no certifica 30 FPS sostenidos ni estabilidad prolongada.
- La cifra 60 FPS de la barra de OBS es la frecuencia de su composición; no demuestra captura a 60 FPS desde los teléfonos. La función de 60 FPS continúa aplazada.
- Audio recibido correctamente confirmado en la conversación anterior; en esta captura Xiaomi indica micrófono apagado. No se infiere sincronía A/V de una imagen.
- Siguiente validación: mantener dos cámaras durante 30 minutos, desconectar/reconectar una sin interrumpir la otra, reiniciar receptor/OBS conservando las fuentes y medir latencia/sincronía con palmada o estímulo común. Anotar USB/Wi-Fi y FPS por teléfono.
## Corrección de activación 0xC00D3E85 - 16-11-2026

- Usuario reporta COMException MF_E_SHUTDOWN al iniciar el dispositivo virtual desde WT_StartCamera.
- Regresión local reproducida antes de modificar el proveedor: activar una fuente, conservar IMFMediaSource y liberar IMFActivate hacía que GetCharacteristics devolviera exactamente 0xC00D3E85. El destructor del activador llamaba a ShutdownObject y cerraba prematuramente la fuente ajena.
- Corrección: liberar el activador sólo libera su referencia COM; el cierre explícito sigue a cargo de ShutdownObject o IMFMediaSource::Shutdown. La fuente conserva la capacidad de arrancar y entregar muestras tras liberar el activador.
- Build.ps1 -RunChecks: 33 comprobaciones aprobadas; la regresión pasa de 0xC00D3E85 a S_OK y entrega una muestra válida. Se conservan las pruebas de independencia, proceso separado, reinicio, caducidad y liberación sin ciclos al cerrar explícitamente.
- Compilación completa WPF/C++ aprobada, 0 errores y 0 advertencias. Hash de DLL en Native coincide con el proveedor compilado.
- WT_StartCameraEx informa etapa de fallo (COM, plataforma MF, creación o inicio del dispositivo). El mensaje administrado conserva el código HRESULT sin depender de información de error COM residual.
- Falta confirmar la activación real reportada por el usuario. Cerrar receptor/OBS, recompilar Windows y volver a Activar cámara virtual autorizando el registro de la nueva DLL. No cambia Android y no se han alterado dispositivos del usuario desde las herramientas.
## Cámara virtual implementada - 16-11-2026

- Proveedor nativo COM x64 con fábrica IMFActivate, origen IMFMediaSourceEx y flujo IMFMediaStream2. Puente de último cuadro BGR32 a NV12 1920x1080/30, ajuste proporcional y marcas de tiempo crecientes.
- Administrador WPF y botones Activar/Desactivar por teléfono. Identidad por SID+UUID; nombre original persistido. Recuperación sin elevación automática, retirada del dispositivo y cierre del publicador desde MTA.
- Instalador elevado incluido, con DLL versionada en Program Files, registro COM por teléfono y directorio de intercambio con permiso de lectura para Local Service. No se ha ejecutado este instalador ni registrado un dispositivo desde las herramientas.
- Compilación completa del receptor, incluido el proveedor C++, aprobada: 0 errores y 0 advertencias. DLL e Install-Provider.ps1 verificados en Native junto al ejecutable.
- Build.ps1 -RunChecks: 29 comprobaciones nativas del proceso principal aprobadas, incluido lector en proceso separado. Comprueba activación/atributos, muestras NV12, colores/proporción, token, timestamps, independencia de dos fuentes, stop/restart, reconexión del publicador, caducidad y ausencia de ciclo COM al cerrar. Usa imágenes sintéticas; no prueba teléfonos ni OBS.
- ConnectionChecks --offline: 38 comprobaciones aprobadas de audio, protocolo, controles/zoom, enlaces y persistencia. Análisis sintáctico del instalador aprobado; no equivale a probar elevación/ACL/registro.
- CheckVirtualCamera.ps1: herramientas y API disponibles; fuente y DLL presentes. Detecta Visual Studio Insiders. No registra dispositivos.
- Pendiente: activar desde la aplicación, comprobar lectura real desde Camera Frame Server y enumeración/imagen en OBS, dos teléfonos físicos y latencia/sincronía comparadas. El audio sigue por la Fuente multimedia Matroska; conservarla activa debajo de la cámara virtual. No se creó micrófono virtual.
- Android no cambia en esta revisión. Instrucciones: docs/CAMARA_VIRTUAL.md.
## Confirmación de audio y herramientas nativas - 16-11-2026

- Usuario confirma que recibe correctamente el audio y que el resto del funcionamiento actual va bien. No se ha medido aún latencia ni sincronía con instrumentación.
- Instalados MSVC x64 14.52.37025 (Visual Studio 18 Insiders) y Windows SDK 10.0.26100.0; cabecera mfvirtualcamera.h disponible.
- Prueba real mínima C++ compilada, enlazada con mfsensorgroup.lib y ejecutada con salida 0. Comprueba herramientas y referencia a MFCreateVirtualCamera; no crea una cámara ni prueba un proveedor Media Foundation.
- Detector actualizado para incluir instalaciones prerelease y búsqueda de compilador cuando vswhere no devuelve instalaciones.
- Siguiente desarrollo: proveedor COM Media Foundation, puente de cuadros y administración de dispositivos independientes por teléfono. Aún no hay cámaras virtuales registradas.
# Registro de pruebas — WT MultiCam

## Audio opcional, latencia y ruta virtual — 16-11-2026

- Usuario confirma multiview y zoom, y recepción en OBS con demora. Solicita audio del teléfono y propone usar dispositivos de captura mediante cámaras virtuales.
- Micrófono opcional en Android, permiso RECORD_AUDIO, PCM mono 48 kHz y transporte TLS añadido. Audio se detiene junto a captura y al salir al fondo. Receptor valida longitud de 1920 bytes/timestamps y muestra estado.
- Nueva salida por enlaces `&format=matroska`: tracks MJPEG/PCM con timestamps explícitos y silencio cuando no se optó por micrófono. Los enlaces antiguos siguen con MJPEG/video solamente. Menor cola H.264 y codificación de salida despertada por cuadro; no se ha medido la latencia final.
- Windows: compilación final correcta, 0 errores/advertencias. ConnectionChecks --offline: 38 comprobaciones aprobadas, incluidas tres de audio (conservación, tamaño y truncamiento).
- ObsOutputChecks: cuatro pruebas de imagen aprobadas y prueba real de demultiplexado con las DLL FFmpeg 62 de OBS: 25 cuadros y 50 paquetes PCM de tono sintético, timestamps crecientes hasta 960/980 ms, tamaños PCM correctos. No prueba la reproducción HTTP en OBS ni el teléfono.
- Kotlin de captura/video/conexión/audio compila contra SDK 36. Recursos compilados con aapt2. APK completo y regeneración de View Binding para los nuevos controles de audio todavía pendientes.
- Fuente oficial OBS verificada: buffering_mb tiene valor predeterminado 2 y rango 0–16 MB. Instrucciones de la aplicación actualizadas a formato matroska y búfer 0 MB. No se modificó automáticamente la configuración del OBS del usuario: el acceso a su ventana falló al iniciar el servicio de automatización (program not found).
- CheckVirtualCamera.ps1: build 26200/x64, exportación MFCreateVirtualCamera disponible; mfvirtualcamera.h y MSVC x64 no encontrados. Proveedor nativo aún no implementado; ningún dispositivo/driver/registro creado. Requiere herramientas C++/Windows SDK y desarrollo del proveedor/puente/instalación real.
- Próximo paso: preparar herramientas nativas para cámaras virtuales. Para probar audio/latencia de la salida intermedia, actualizar ambas apps, optar por micrófono y usar enlace nuevo en OBS con formato matroska/búfer 0 MB. Medir con palmada y comprobar permisos, apagado, reconexiones y dos teléfonos.

## Multiview 80/20 y zoom continuo — 16-11-2026

- Receptor Windows compilado sin errores ni advertencias. Render y comprobación de proporción 4:1 aprobados con dos y cinco cámaras sintéticas, en 1400×900 y 1000×720; capturas revisadas. Miniaturas compactas sin estiramiento y barra horizontal cuando no caben.

- La captura del usuario muestra Xiaomi 25080RABDG y motorola moto e30 en el multiview. Reportó miniaturas demasiado grandes y pidió priorizar la vista principal y aplicar zoom durante el movimiento.
- Se cambia a proporción 4:1 entre monitor y franja inferior: miniaturas con tamaño acotado, sin estiramiento vertical y desplazamiento horizontal para cámaras adicionales.
- Se retira Aplicar zoom. Deslizador con muestreo cada 80 ms, una orden en vuelo y sólo el último valor pendiente. Confirmaciones no generan nuevas órdenes y no desplazan el deslizador durante el arrastre. Cambiar de teléfono/cámara descarta valores pendientes y valida la cámara destinataria.
- ConnectionChecks --offline: 35 comprobaciones aprobadas; cuatro nuevas verifican exclusión de envíos simultáneos, sustitución de valores intermedios, descarte al cambiar cámara y liberación tras un error.
- Esta revisión no cambia el protocolo Android; basta actualizar Windows cuando el APK de controles remotos ya está instalado. Fluidez real del zoom por USB/Wi-Fi y valor final al soltar todavía pendientes de prueba del usuario.

## Multiview y canal de control — 16-11-2026

- WPF compila sin errores ni advertencias con la nueva ventana Multiview y su botón en la barra lateral.
- ConnectionChecks --offline: 31 comprobaciones aprobadas. Las seis nuevas comprueban cámara/operación/valor, rechazo de confirmación de otro teléfono, ausencia de éxito anticipado, confirmación correcta, rechazo del hardware y valores no finitos.
- MultiviewChecks: render sintético en 1400×900 y 1000×720 con cinco cámaras emparejadas de prueba; una sexta sin emparejar se excluye. Se verifica selección conservada, controles deshabilitados sin capacidades y espacio para las cinco miniaturas. Capturas inspeccionadas visualmente.
- Todos los fuentes Kotlin, incluida MainActivity, compilan contra SDK 36 y dependencias/bindings locales. Se mantienen controles y red fuera de la Activity; la Activity sólo conecta callbacks.
- Ajustes incluidos: zoom nativo/recorte según soporte, compensación AE, autofocus y flash continuo. Se aplican en el hilo Camera2 y responden por el canal TLS existente; no hay captura de ventana en la ruta OBS.
- Pendiente: APK Gradle completo y prueba física de zoom/exposición/enfoque/linterna en USB/Wi-Fi y dos teléfonos. Las pruebas locales no certifican que cada fabricante acepte todos los controles ni miden latencia de control real.

## Salida directa OBS y retirada de 60 FPS — 16-11-2026

- Android: retirados perfiles 60 FPS normales y de alta velocidad, retirada creación de sesión restringida y añadida comprobación máxima de 30 al abrir cámara. Perfiles guardados de 60 no coinciden con la lista y vuelven a una opción disponible. Se conserva icono y persistencia.
- Windows: salida HTTP MJPEG exclusivamente loopback, independiente por UUID, botón Copiar enlace OBS y tarjeta Directa local. OBS consume una Fuente multimedia por teléfono; la salida no lee ventanas ni depende del render WPF.
- Compilación WPF aprobada en directorios intermedios/salida temporales: 0 errores y 0 advertencias. Se aisló la compilación para no interferir con archivos generados de Visual Studio.
- ConnectionChecks --offline: 25 comprobaciones aprobadas, cinco de ellas sobre autenticación/rutas por cámara, estabilidad de enlaces y formato multipart MJPEG.
- ObsOutputChecks: cuatro comprobaciones aprobadas: dimensiones verticales, giro/espejo por cuadrantes, reutilización JPEG sin recodificar y JPEG válido de señal negra.
- Kotlin de captura/video/conexión compila contra Android 36; recursos aapt2 compilados. APK Gradle completo pendiente.
- ConnectionChecks --obs-output: NO aprobado en este entorno. HTTP a 127.0.0.1:47780 bloqueado por Winsock 10013 (socket no permitido). No se pudo ejecutar la validación HTTP de dos fuentes, ni se afirma compatibilidad real en OBS hasta probarla en el equipo del usuario.
- Próxima acción: cerrar el receptor anterior, recompilar Windows y Android; iniciar teléfono a 1080p30; copiar enlace en Fuente multimedia de OBS, Archivo local desmarcado y Formato de entrada mpjpeg. Comprobar imagen y minimizar receptor, después reinicios y dos teléfonos.

## Alta velocidad aplazada — 16-11-2026

- Usuario probó 60 FPS: `No se pudo preparar cámara: Surface size 1202x2005 is not part of the high speed supported size list [1280x720, 1920x1080]`.
- La sesión detectó una superficie con dimensiones del visor en vez de un tamaño permitido. No se considera validado el modo de alta velocidad.
- Por instrucción del usuario se retira el modo: selector limitado a 15/24/30, sin crear sesiones restringidas de alta velocidad. Perfil guardado de 60 vuelve a una opción disponible de hasta 30.
- Nueva prioridad explícita: integración directa con OBS; no usar Captura de ventana como salida final.

## Alta velocidad y persistencia — 16-11-2026

- Diagnóstico del usuario: Xiaomi 25080RABDG, Android 16, trasera 0. Normal: máximo 30 FPS y duración 1080p de 33 333 333 ns tanto para encoder como visor. Encoders MTK admiten 1080p60. Alta velocidad anunciada: [30,120] / [120,120].
- Se implementa el perfil H.264 60 con sensor 120 y limitación Surface a 60 antes de codificar; icono de marca y vínculos/perfiles persistentes. Ver `docs/ALTA_VELOCIDAD_Y_RECONEXION.md`.
- Compilación Windows aprobada en directorio temporal, 0 errores/advertencias (`dotnet build ... -o <temporal> -p:NuGetAudit=false --disable-build-servers`). La compilación a la carpeta habitual encontró el EXE abierto; no se cerró la app del usuario.
- 20 comprobaciones ConnectionChecks sin sockets aprobadas: incluye guardado cifrado, recuperación de dos vínculos, rechazo con otro certificado y archivo corrupto. No valida transporte ni Windows Certificate Store físico.
- Todos los fuentes Kotlin, incluida MainActivity, compilan contra SDK Android 36 y dependencias/View Binding ya generados de la compilación local del usuario. No sustituye generar el APK completo con Gradle.
- Recursos Android compilados mediante aapt2. Pruebas de reglas de perfiles/pacing aprobadas, incluyendo elección de rango fijo 120 y rechazo de rangos variables solos.
- Pendiente: APK completo y pruebas reales de alta velocidad, reconexión, perfil persistido e icono en el launcher. No se ha confirmado 60 FPS sostenido ni se ha instalado esta revisión en el teléfono desde este entorno.

## Rendimiento reportado — 16-11-2026

- Usuario informa unos 29 FPS enviados en Android y 26–29 FPS presentados en Windows, tanto por USB como por Wi-Fi. No se especificaron resolución, formato, duración ni modelo.
- Selector sólo ofrece hasta 30 FPS aunque la cámara del fabricante admite 1920×1080/60 según el usuario.
- Se añade botón «Diagnóstico de 60 FPS» con modelo/Android, cámara, rangos AE normales, duración mínima de captura/visor 1080p, codecs AVC y rangos de alta velocidad si existen. Permite copiar el informe sin datos de conexión.
- No se habilita 60 FPS sin conocer qué filtro falla. Pendiente compilar el APK actualizado y obtener el informe de la cámara seleccionada. La nueva interfaz y consulta no han sido validadas en teléfono.

## Reinicio en vertical — 16-11-2026

- Confirmación posterior del usuario: «Ahora si esta arreglado». Se considera resuelta la distorsión reportada al detener/reiniciar en vertical por USB. No se informaron modelo, perfil, FPS ni duración; esta confirmación no valida rendimiento, Wi-Fi ni toda la matriz de cámaras.

- Reporte del usuario: al detener el envío y volver a iniciarlo por USB con el teléfono vertical, la imagen se distorsiona.
- Ajuste Android: reaplicar la transformación de TextureView al primer cuadro de cada sesión configurada; descartar transformaciones encoladas de aperturas anteriores y comprobar la generación antes de liberar recursos en una apertura obsoleta.
- Pendiente físico: iniciar en vertical, detener y reiniciar cinco veces; repetir tras giro horizontal/vertical y con ambas cámaras. Comprobar proporciones tanto en el teléfono como en Windows, con H.264 y JPEG. Registrar perfil y pantalla afectada si persiste.
- No se considera validado el arreglo hasta repetir el caso en el teléfono.

## Sesión 16-11-2026 — revisión en XAMPP

- Ubicación actual: `C:\xampp\htdocs\Movil_camara`.
- Protocolo: `dotnet run --project tools/ConnectionChecks/ConnectionChecks.csproj --disable-build-servers -- --offline` aprobado (16 comprobaciones). No valida sockets ni teléfono.
- Codec: `dotnet run --project tools/CodecChecks/CodecChecks.csproj --disable-build-servers -- --1080p60` aprobado: encode/decode H.264 real a RGB32, 8 294 400 bytes, píxel gris 97. No mide 60 FPS sostenidos.
- Windows: compilación no completada. La configuración NuGet global restringía QRCoder (NU1100); se añadió `NuGet.Config` local con mapeo explícito. El siguiente intento alcanzó la descarga, pero falló por DNS: NU1301, host desconocido `api.nuget.org`. No se generó un receptor nuevo validado.
- Android: `JAVA_HOME` del entorno apunta a `Android Studio1\jbr`, instalación incompleta. Se probó Java 21 de `C:\Program Files\Android\Android Studio\jbr` y el SDK de `C:\Users\infor\AppData\Local\Android\Sdk`, sin cambiar variables globales. Con caché temporal se superó el bloqueo de escritura del wrapper, pero Gradle no pudo conectarse a su daemon local. APK completo pendiente.
- ADB: no pudo iniciar/comunicarse con el servidor local. No se pudo determinar si hay un teléfono conectado; no se instaló ni modificó ningún dispositivo.
- Para .NET se desactivó telemetría y se usó `DOTNET_CLI_HOME` temporal para evitar el registro y la carpeta de usuario restringidos. No se modificó la configuración global del equipo.

### Próxima acción fuera de este entorno restringido

1. Abrir Android Studio en la raíz; usar su JDK integrado válido y compilar `:app:assembleDebug`.
2. Compilar `Escritorio/MovilCamara/MovilCamara.sln` en Visual Studio con acceso a NuGet. El mapeo local de QRCoder ya está incluido.
3. Actualizar ambas aplicaciones. Probar un teléfono por USB: H.264 720p30, 1080p30 y 1080p60 sólo si se ofrece.
4. Repetir por Wi-Fi y registrar imagen local, orientación, FPS y errores con la plantilla siguiente.

Resultado de esta sesión: parcial; comprobaciones locales aprobadas, compilaciones completas y validación física pendientes. Las casillas P0 permanecen abiertas.

No hay aún una validación física documentada de la última versión H.264/selector. Los reportes anteriores del usuario confirman QR, llegada de video a Windows y vista previa USB correcta en versiones previas.

Copiar esta plantilla por cada combinación. No guardar tokens, secretos de sesión ni un QR activo.

## Plantilla de sesión

- Fecha/hora:
- Versión o commit del receptor:
- Versión o commit del APK:
- Modelo del teléfono:
- Versión Android:
- Cámara / identificador:
- Perfil seleccionado (resolución, FPS, H.264/JPEG):
- Encoder reportado, si se conoce:
- PC / CPU / GPU / RAM:
- Windows:
- Conexión: USB ADB / anclaje USB / Wi-Fi:
- Router/banda Wi-Fi y PC por Ethernet/Wi-Fi:
- Tiempo de prueba:
- Otros teléfonos conectados:

| Medición | Inicio | Durante la prueba | Final |
|---|---|---|---|
| FPS enviados en Android | | | |
| FPS presentados en Windows | | | |
| Bitrate enviado | | | |
| CPU / GPU del PC | | | |
| Memoria del receptor | | | |
| Batería / temperatura accesible | | | |
| Latencia medida y método | | | |

### Comprobaciones

- [ ] Imagen correcta en el teléfono.
- [ ] Imagen correcta en Windows.
- [ ] Vertical/horizontal y ambos sentidos de giro.
- [ ] Frontal/trasera y espejo.
- [ ] Sin cuadros congelados ni crecimiento continuo de demora.
- [ ] Detener/reiniciar y cambiar perfil.
- [ ] Reconexión tras corte.
- [ ] Ventana de fuente y Modo OBS.
- [ ] OBS recibe esa ventana.
- [ ] Otro dispositivo continúa si se desconecta éste.

### Resultado

- Aprobado / fallido / parcial:
- Error exacto y en qué pantalla aparece:
- Pasos para reproducir:
- Comportamiento esperado:
- Captura o log sin credenciales:
- Próxima acción concreta:

## Historial documentado

| Fecha | Alcance | Resultado |
|---|---|---|
| 16-11-2026 | Compilación WPF y pruebas de protocolo sin sockets | Correctas durante implementación. |
| 16-11-2026 | Clases Kotlin y recursos compilados por separado | Correctos; no sustituyen el APK completo. |
| 16-11-2026 | Encode/decode local Windows en formato 1920×1080/60 | Recupera RGB32 correcto; no mide FPS sostenidos del teléfono ni de la red. |
| Pendiente | APK actual + H.264 en teléfono real | Completar una sesión usando la plantilla. |
