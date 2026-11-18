# PRUEBAS - WT MultiCam

Actualizado: 18 de noviembre de 2026. Plan vigente: PLAN.md. Tareas: TODO.md.

## Empaquetado Windows — 18-11

- Usuario confirma orientación manual funcional, señala controles estrechos en horizontal y pide instaladores. Luego indica que ya generó APK y solicita concentrarse exclusivamente en escritorio. No se genera ni sustituye APK en esta entrega.
- Publicación Release win-x64 autocontenida con runtime/WPF 8.0.31 de caché local y apphost 8.0.34 del SDK instalado; no se requiere descargar runtime aparte. Los intentos de restauración en línea fallaron por DNS; se resolvió sin alterar permisos mediante fuentes locales.
- `--verify-installation` del EXE publicado: resultado 0; comprueba que CoreLib/WPF se cargan de la carpeta publicada, carga la DLL nativa y localiza ADB. Informe en `build/packaging/verification.txt`. No inicia hub ni captura ni registra cámaras.
- ADB incluido ejecuta versión 1.0.41 / Platform Tools 37.0.0 correctamente. Fuente/NOTICE incluidos.
- Asistente Inno Setup compilado con identidad, icono, idioma español y .NET incluido. Fuentes reproducibles en `Instaladores/Fuentes`; artefacto final en `Instaladores/Windows` y SHA256 adjunto.
- Desinstalación: limpieza de cámaras sólo después de confirmar; bloqueo si la app está abierta o no se pueden retirar. Se conservan vínculos y proveedor/COM protegidos compartidos. No se probó desinstalando las cámaras del usuario.
- Pendientes: Windows limpio, actualización, desinstalación/cancelación, uso de cámaras por OBS/otras cuentas, integración de USB por fabricante. La imagen generada del instalador es propuesta visual, no captura real del asistente.

## Orientación manual en transmisión — 18-11

- Implementado exactamente en Controles: dos botones con iconos en una línea, Vertical/Horizontal. Solicitan orientación fija de actividad, no orientación del sensor; cámara y disposición siguen el display ya utilizado por el protocolo. Se restaura la política previa al detener.
- No se cambia bitrate/resolución ni se llama a reiniciar captura por pulsar orientación. La actividad maneja `orientation|screenSize`; quedan pendientes continuidad física, fabricantes, frontal/trasera y salida OBS.
- AAPT2 recompila/enlaza recursos correctamente y todos los fuentes Kotlin compilan con R actualizado, salida 0. APK completo y prueba en dispositivo pendientes, según la limitación de Gradle ya registrada.

## Corrección de vista previa y acciones — 18-11

- Usuario detecta que faltan el visor de preparación y controles como en el concepto; no se considera aceptada la implementación anterior por compilar.
- Se añade modo de captura local sólo TextureView, sin encoder/ImageReader, sin sender ni micrófono. El controlador conserva sus guardas de generación al cambiar superficie; su estado remoto figura inactivo en vista previa.
- Permiso de cámara distingue abrir vista previa de iniciar transmisión. Micrófono local durante envío puede pedir RECORD_AUDIO; respuesta ligada a la captura actual para no activar audio en otra sesión.
- Fila fija de tres acciones con iconos y Detener debajo; área de detalles desplazable reserva el alto medido de los botones. Pendiente comprobar visualmente en teléfono, especialmente letra grande y horizontal.
- Recursos AAPT2 recompilados/enlazados con Android 36 y símbolos locales: salida 0. No se afirma APK ensamblado ni prueba de dispositivos; sigue pendiente el proceso completo desde Android Studio.
- Todos los fuentes Kotlin recompilados con R actualizado: salida 0 en `build/checks/mobile-ui/compile.log`. La compilación no verifica apertura de cámara, permisos ni distribución visual real.

## Implementación de diseño móvil aprobado — 18-11

- Usuario aprueba el concepto visual y solicita implementarlo. Se rediseñan layouts nativos y navegación de conexión/preparación/transmisión, sin migrar de Kotlin/XML.
- AAPT2: recursos compilados y enlazados con Android 36 y símbolos locales de dependencias, salida 0; artefactos en `build/checks/mobile-ui`. No constituye APK instalable completo.
- Compilación Kotlin de todos los fuentes con R actualizado y tipos locales: salida 0, registro `build/checks/mobile-ui/compile.log`. IDs/tipos View Binding existentes conservados.
- Se mantiene pendiente ensamblado Gradle (bloqueo de caché ya registrado), regeneración de bindings y prueba visual/funcional en teléfono. No se afirma parecido visual validado mediante captura real.
- El usuario reportó mejora parcial del lag por red pero muchos cortes antes de cambiar a diseño; la investigación de desconexiones no se declara resuelta por estos cambios.

## Wi-Fi después de mejora USB - 18-11

- Usuario confirma USB bastante más fluido; red todavía funciona mal. No hay medición de retraso/FPS ni validación sostenida de USB.
- Antes H.264 tenía bitrate fijo según resolución/FPS, mientras JPEG ya adaptaba calidad. Android ahora solicita reducir bitrate si la cola se satura o el promedio de escritura supera el presupuesto por cuadro; recupera lentamente tras ocho ventanas de envío sano. Límites 25-100 % del objetivo original y rango del codec. El objetivo se conserva durante reintentos de esa captura y se reinicia al iniciar otra. Loopback/USB no adapta.
- No se altera resolución, FPS, TLS, cola AVC ni watchdog. Si el codec rechaza ajustes dinámicos, se conserva su configuración; el porcentaje mostrado es un objetivo solicitado, no bitrate aplicado confirmado. No hay aceleración GPU nueva ni medición de capacidad Wi-Fi.
- Ensamblado APK por Gradle bloqueado al intentar abrir el lock del wrapper en la caché del usuario (acceso denegado). No hay APK nuevo disponible todavía; no se requiere cambiar permisos globales. Prueba en dispositivo pendiente.
- Validación alternativa completada: todos los fuentes Kotlin de `app/src/main/java` compilados con el compilador local, Android 36 y dependencias/generados locales; salida en `build/checks/wifi-classes`, registro `wifi-compile.log`, código 0. No equivale a ensamblar/firmar APK. Prueba ejecutable `tools/WifiBitrateChecks.kt` aprobada: congestión por cola/escritura, límite inferior/superior, histéresis e independencia entre sesiones.

## Recuperación de fluidez - reporte posterior del 18-11

- Usuario: por cable más estable, sin pérdida de conexión en esta prueba, pero saltos y retraso ocasionales. No equivale a validación de estabilidad sostenida o de ambos transportes.
- Riesgo de la revisión anterior: tres paquetes a 30 FPS dejan poco margen frente a pausas breves; al saturar se espera IDR, lo cual puede producir congelación visible. Ahora ocho paquetes como límite de seguridad y recuperación descartando sólo la conversión RGB de las salidas antiguas, conservando la decodificación AVC y sus referencias. Si se supera el límite, se mantiene recuperación por IDR.
- Receptor compilado sin advertencias/errores en `build/fluency-desktop`; 73 comprobaciones sin sockets aprobadas. Prueba nativa H.264 de recuperación a 640×360 aprobada: se omiten dos de cada tres conversiones y se valida RGB gris al volver a presentar. No es una medición de rendimiento ni una prueba de conectividad.
- La captura de DroidCam no demuestra aceleración GPU activa. Esta implementación no añade GPU, no modifica Android ni promete resolver todas las causas. Pendiente comparar multiview y OBS con teléfonos reales y medir tiempos por etapa.
- Repetición nativa con `--catch-up --1080p30` aprobada: salida RGB32 de 8.294.400 bytes, gris esperado tras decodificar cuadros sin presentación intermedios. Se verificó corrección del formato; no se midieron 30 FPS sostenidos.

## Retraso y desconexiones de video - reporte posterior del 18-11

- Usuario reporta mucho retraso y pérdidas frecuentes; confirma ambos transportes: USB y Wi-Fi. Se levanta el aplazamiento de estabilidad para priorizar este problema.
- Inspección: el receptor leía el siguiente paquete H.264 sólo después de decodificar el anterior. Esto permite acumular datos en TCP cuando el decoder no sostiene la velocidad; Android tiene un watchdog que cierra escrituras bloqueadas más de 1500 ms. No hay captura temporal que demuestre que sea la única causa del reporte.
- Corrección Windows: lectura TLS independiente, máximo tres paquetes comprimidos pendientes, descarte de dependencias hasta próximo IDR tras saturación. No se aumenta el timeout ni se descartan P-frames aislados para continuar decodificando referencias rotas. JPEG conserva su ruta actual.
- Se conservan detalles de errores de recepción/codec en el estado del teléfono; COMException se informa sin dejar una tarea fallida sin observación.
- Compilación Windows sin advertencias/errores y 72 comprobaciones locales aprobadas (seis nuevas sobre orden, saturación, recuperación y cancelación). No prueban conectividad ni latencia física. Validación con teléfonos/OBS pendiente.
- Ejecutable de prueba en `build/stability-desktop/MovilCamara.exe`; no se cerró ni sustituyó la app que el usuario tenga abierta. Android no cambia en esta corrección.

## Sincronización manual - 18-11-2026

- Implementado retardo PCM 0-1000 ms/20 ms, por teléfono y con cola independiente por lector OBS. Cambios de retardo y reinicios de audio descartan el buffer; volumen/silencio se aplican después del retardo para silenciar también las muestras pendientes.
- Receptor WPF compilado en `build/sync-desktop`, sin advertencias ni errores. La aplicación abierta por el usuario no se cerró ni sustituyó.
- Pruebas locales cubren orden/copia de muestras, silencio inicial, retardo máximo, reinicio, cambios de valor, valores inválidos e independencia entre teléfonos/lectores. La prueba WPF comprueba que el ajuste se aplica al teléfono seleccionado y no se hereda al seleccionar otro.
- Resultado: 66 comprobaciones sin sockets aprobadas y prueba WPF aprobada; cuatro capturas sintéticas con 2/5 cámaras a 1400×900 y 1000×720 en `build/checks/sync-multiview`. Revisión visual de 1000×720: controles de sincronización accesibles y panel lateral desplazable. Estas imágenes no contienen cámaras reales.
- Pendiente: palmadas reales en OBS, desfase antes/después por teléfono/transporte, reconexión, mute durante retardo, duración y deriva. No se afirma sincronía medida ni resolución de cortes. Las comprobaciones locales no sustituyen esta validación.

## Confirmación de controles de audio - 18-11-2026

- Después de las instrucciones de probar medidor, volumen, silencio, micrófono remoto y selección, el usuario responde: “Todo eso va bien”. Se registra como confirmación de funcionamiento básico de esos controles.
- No se especifican modelos, transportes, duración ni casos de permisos denegados/reconexión; esos escenarios siguen pendientes.
- No se infiere que se hayan resuelto el retraso inicial o los cortes de audio reportados previamente. Estabilidad/fluidez continúan aplazadas según la decisión del usuario.
- Siguiente hito: medir sincronización con palmada visible/audible por teléfono y documentar qué señal llega primero y cuántos milisegundos, antes de elegir la compensación.
- Esta actualización sólo modifica documentación; no se ejecutaron nuevas pruebas del producto.

## Controles de audio del multiview - 17-11-2026

- Usuario confirma recepción del audio independiente, con retraso al conectar y cortes ocasionales. Solicita aplazar estabilidad/fluidez y continuar con controles. No se considera estable ni sincronizado el audio por esta confirmación.
- Implementados volumen de salida 0-100 %, silencio y medidor de pico por teléfono. Silencio no detiene AudioRecord; los valores son independientes por ID y sólo se mantienen durante la ejecución del receptor.
- El medidor muestra pico de salida antes de OBS, refresca cada 150 ms y caduca sin muestras recientes. Se distingue captura activa en Android de audio recibido/conectado en Windows.
- Implementada orden remota de micrófono con cámara/destinatario, confirmación de grabación real AudioRecord y autorización explícita de la sesión. No concede permisos ni inicia cámara. Detener cámara/salir de Android invalida la autorización.
- ConnectionChecks --offline: 55 comprobaciones aprobadas. Incluyen extremos PCM, no modificar entrada, dBFS, independencia de fuentes, recuperación de volumen al quitar silencio, caducidad, valores inválidos, compatibilidad con APK antiguo y confirmación/rechazo remoto de micrófono.
- Windows compila en build/audio-controls-desktop con 0 errores y 0 advertencias. La copia a la salida habitual falló porque el EXE del usuario está abierto; no se cerró su aplicación.
- Los 16 fuentes Kotlin, incluida MainActivity, compilan contra SDK 36 con dependencias y bindings locales. APK completo NO generado: el wrapper en caché temporal intentó descargar Gradle y falló por DNS services.gradle.org. La compilación aislada no sustituye generar/instalar el APK completo.
- MultiviewChecks aprobado: selección mantiene volumen/silencio por teléfono, otro teléfono conserva sus propios ajustes y el botón remoto permanece deshabilitado sin capacidades/autorización. Render con dos/cinco cámaras sintéticas en 1400x900 y 1000x720; proporción 80/20 preservada y controles desplazables. Capturas revisadas.
- Pendiente físico: actualizar ambas aplicaciones, iniciar Android marcando Enviar micrófono, comprobar nivel, volumen y silencio en OBS y detener/reactivar desde PC. Probar dos teléfonos y permisos/cortes durante una orden. No se ejecutaron pruebas de estabilidad o fluidez en esta entrega.


## Estado actual y nueva entrega

El usuario confirmó funcionamiento de cámaras virtuales, multiview, zoom y audio previo. Las capturas anteriores muestran Motorola y Xiaomi con dos fuentes de captura en OBS. No se infiere duración, sincronía o toda la matriz de reconexiones de esa confirmación general.

La salida independiente de sólo audio ya fue probada por el usuario: llega sonido a OBS, con retraso inicial y cortes ocasionales. Esa entrega sólo cambió Windows y conservó enlaces anteriores. Los controles remotos añadidos después requieren actualizar también Android; no crean un micrófono virtual del sistema.

## Verificaciones ejecutadas el 17-11-2026

| Comprobación | Resultado | Alcance |
|---|---|---|
| Compilación completa Windows | Aprobada, 0 errores y 0 advertencias | Incluye recursos, botón nuevo y proveedor nativo existente. |
| ConnectionChecks --offline | 43 comprobaciones aprobadas | Incluye rutas/credenciales de audio por teléfono, separación de audio/video y enlaces estables. No prueba sockets. |
| ObsOutputChecks, imagen | Cuatro comprobaciones aprobadas | Orientación/espejo, JPEG sin recodificar e imagen sin señal. |
| FFmpeg, Matroska A/V anterior | Aprobado | 25 paquetes de video y 50 PCM, timestamps crecientes. |
| FFmpeg, Matroska sólo audio | Aprobado | Una pista de audio, ninguna de video, 50 paquetes PCM; tono/silencio y bytes intactos. |
| ConnectionChecks --obs-audio-output | Bloqueada, NO aprobada | Winsock 10013 al conectar a 127.0.0.1:47780; no se alcanzó la prueba de dos lectores ni reconexión. |
| Fuente de sólo audio en OBS con teléfonos | Recepción confirmada posteriormente | Usuario reporta retraso inicial y cortes ocasionales; estabilidad pendiente. |

Los 33 checks nativos de la cámara virtual corresponden a la revisión anterior; no se repitieron en esta entrega, que no modifica el proveedor. Los cambios actuales separan lectores de audio/video para evitar codificar imágenes por una fuente sólo audio. El test HTTP preparado debe comprobar este comportamiento en un entorno permitido.

## Próxima sesión física: audio independiente

1. Cerrar y recompilar Windows. Usar el APK actual con micrófono autorizado e iniciar el envío.
2. Mantener cámara virtual para video; copiar audio OBS y crear Fuente multimedia, Archivo local desmarcado, formato matroska y búfer 0 MB.
3. Silenciar/retirar la ruta A/V anterior del mismo teléfono y conservar la fuente nueva activa.
4. Confirmar audio y ausencia de otra imagen; apagar el micrófono y verificar silencio.
5. Detener/reiniciar y repetir con dos teléfonos: uno debe continuar cuando el otro se apaga o reconecta.
6. Registrar latencia/sincronía observada sin atribuir al formato una mejora no medida.

## Matriz de próximas pruebas

| Caso | Resultado esperado | Estado |
|---|---|---|
| Un teléfono, audio nuevo + cámara virtual | Imagen y sonido por rutas independientes, sin audio duplicado | Pendiente |
| Dos teléfonos | Fuentes de audio independientes y sin mezclas | Pendiente |
| Apagar micrófono o perder audio | Silencio sin reproducir paquetes antiguos | Pendiente |
| Reconectar un teléfono | Recuperar fuente sin cortar el otro | Pendiente |
| Reiniciar receptor/OBS | Conservar enlaces/identidad, sin duplicados | Pendiente |
| Sesión 30 minutos, luego 60 | Sin congelamientos ni crecimiento continuo de demora/memoria | Pendiente |
| Palmada al inicio/final | Medición del desfase y posible deriva por teléfono | Pendiente |
| Controles de audio | Permisos, nivel, silencio y volumen correctos por teléfono | Implementados y comprobados localmente; prueba física pendiente |
| Instalador completo | Instalar/actualizar/desinstalar sin dispositivos huérfanos | Aún no implementado |

## Plantilla vigente por sesión

- Fecha/hora y duración:
- Versiones del receptor/APK y Windows/Android:
- Teléfono, cámara y perfil (resolución/FPS/formato):
- Transporte USB/Wi-Fi y otros teléfonos conectados:
- Ruta de video (cámara virtual) y de audio (nuevo enlace sólo audio / A/V anterior):
- Formato/búfer de OBS y fuente anterior silenciada/retirada:
- Micrófono autorizado/activado y cambios durante la sesión:

| Medición | Inicio | Durante | Final |
|---|---|---|---|
| FPS enviados / presentados por teléfono | | | |
| CPU/GPU y memoria del receptor | | | |
| Temperatura/batería accesible | | | |
| Desfase audio-video en ms y método | | | |
| Latencia real y método | | | |

Registrar si audio o video llega primero. Los 60 FPS de composición de OBS no prueban captura a 60 FPS. No incluir credenciales, enlaces completos ni QR activos.

- Resultado: aprobado / fallido / parcial / bloqueado:
- Pasos y error exacto, si existe:
- Recuperación independiente y cambios de escena:
- Próxima acción:

## Historial conservado de revisiones anteriores

Las entradas siguientes se conservan como evidencia de su momento. Sus tareas pendientes, instrucciones de Captura de ventana, ausencia de herramientas o propuestas de 60 FPS son históricas; para trabajo nuevo usar PLAN/TODO y las secciones actuales de este archivo.

### Confirmación del usuario: cámaras virtuales en OBS - 16-11-2026

- Usuario confirma que la activación ahora funciona después de la corrección de 0xC00D3E85.
- Capturas: multiview con Motorola moto e30 y Xiaomi 25080RABDG vinculados, ambas miniaturas con imagen; Xiaomi muestra 1920x1080 y 27 FPS en pantalla. OBS muestra dos fuentes de Dispositivo de captura de video e imágenes distintas simultáneas en la vista previa. Esto confirma el uso real básico; no certifica 30 FPS sostenidos ni estabilidad prolongada.
- La cifra 60 FPS de la barra de OBS es la frecuencia de su composición; no demuestra captura a 60 FPS desde los teléfonos. La función de 60 FPS continúa aplazada.
- Audio recibido correctamente confirmado en la conversación anterior; en esta captura Xiaomi indica micrófono apagado. No se infiere sincronía A/V de una imagen.
- Siguiente validación: mantener dos cámaras durante 30 minutos, desconectar/reconectar una sin interrumpir la otra, reiniciar receptor/OBS conservando las fuentes y medir latencia/sincronía con palmada o estímulo común. Anotar USB/Wi-Fi y FPS por teléfono.
### Corrección de activación 0xC00D3E85 - 16-11-2026

- Usuario reporta COMException MF_E_SHUTDOWN al iniciar el dispositivo virtual desde WT_StartCamera.
- Regresión local reproducida antes de modificar el proveedor: activar una fuente, conservar IMFMediaSource y liberar IMFActivate hacía que GetCharacteristics devolviera exactamente 0xC00D3E85. El destructor del activador llamaba a ShutdownObject y cerraba prematuramente la fuente ajena.
- Corrección: liberar el activador sólo libera su referencia COM; el cierre explícito sigue a cargo de ShutdownObject o IMFMediaSource::Shutdown. La fuente conserva la capacidad de arrancar y entregar muestras tras liberar el activador.
- Build.ps1 -RunChecks: 33 comprobaciones aprobadas; la regresión pasa de 0xC00D3E85 a S_OK y entrega una muestra válida. Se conservan las pruebas de independencia, proceso separado, reinicio, caducidad y liberación sin ciclos al cerrar explícitamente.
- Compilación completa WPF/C++ aprobada, 0 errores y 0 advertencias. Hash de DLL en Native coincide con el proveedor compilado.
- WT_StartCameraEx informa etapa de fallo (COM, plataforma MF, creación o inicio del dispositivo). El mensaje administrado conserva el código HRESULT sin depender de información de error COM residual.
- Falta confirmar la activación real reportada por el usuario. Cerrar receptor/OBS, recompilar Windows y volver a Activar cámara virtual autorizando el registro de la nueva DLL. No cambia Android y no se han alterado dispositivos del usuario desde las herramientas.
### Cámara virtual implementada - 16-11-2026

- Proveedor nativo COM x64 con fábrica IMFActivate, origen IMFMediaSourceEx y flujo IMFMediaStream2. Puente de último cuadro BGR32 a NV12 1920x1080/30, ajuste proporcional y marcas de tiempo crecientes.
- Administrador WPF y botones Activar/Desactivar por teléfono. Identidad por SID+UUID; nombre original persistido. Recuperación sin elevación automática, retirada del dispositivo y cierre del publicador desde MTA.
- Instalador elevado incluido, con DLL versionada en Program Files, registro COM por teléfono y directorio de intercambio con permiso de lectura para Local Service. No se ha ejecutado este instalador ni registrado un dispositivo desde las herramientas.
- Compilación completa del receptor, incluido el proveedor C++, aprobada: 0 errores y 0 advertencias. DLL e Install-Provider.ps1 verificados en Native junto al ejecutable.
- Build.ps1 -RunChecks: 29 comprobaciones nativas del proceso principal aprobadas, incluido lector en proceso separado. Comprueba activación/atributos, muestras NV12, colores/proporción, token, timestamps, independencia de dos fuentes, stop/restart, reconexión del publicador, caducidad y ausencia de ciclo COM al cerrar. Usa imágenes sintéticas; no prueba teléfonos ni OBS.
- ConnectionChecks --offline: 38 comprobaciones aprobadas de audio, protocolo, controles/zoom, enlaces y persistencia. Análisis sintáctico del instalador aprobado; no equivale a probar elevación/ACL/registro.
- CheckVirtualCamera.ps1: herramientas y API disponibles; fuente y DLL presentes. Detecta Visual Studio Insiders. No registra dispositivos.
- Pendiente: activar desde la aplicación, comprobar lectura real desde Camera Frame Server y enumeración/imagen en OBS, dos teléfonos físicos y latencia/sincronía comparadas. El audio sigue por la Fuente multimedia Matroska; conservarla activa debajo de la cámara virtual. No se creó micrófono virtual.
- Android no cambia en esta revisión. Instrucciones: docs/CAMARA_VIRTUAL.md.
### Confirmación de audio y herramientas nativas - 16-11-2026

- Usuario confirma que recibe correctamente el audio y que el resto del funcionamiento actual va bien. No se ha medido aún latencia ni sincronía con instrumentación.
- Instalados MSVC x64 14.52.37025 (Visual Studio 18 Insiders) y Windows SDK 10.0.26100.0; cabecera mfvirtualcamera.h disponible.
- Prueba real mínima C++ compilada, enlazada con mfsensorgroup.lib y ejecutada con salida 0. Comprueba herramientas y referencia a MFCreateVirtualCamera; no crea una cámara ni prueba un proveedor Media Foundation.
- Detector actualizado para incluir instalaciones prerelease y búsqueda de compilador cuando vswhere no devuelve instalaciones.
- Siguiente desarrollo: proveedor COM Media Foundation, puente de cuadros y administración de dispositivos independientes por teléfono. Aún no hay cámaras virtuales registradas.
## Registro de pruebas — WT MultiCam

### Audio opcional, latencia y ruta virtual — 16-11-2026

- Usuario confirma multiview y zoom, y recepción en OBS con demora. Solicita audio del teléfono y propone usar dispositivos de captura mediante cámaras virtuales.
- Micrófono opcional en Android, permiso RECORD_AUDIO, PCM mono 48 kHz y transporte TLS añadido. Audio se detiene junto a captura y al salir al fondo. Receptor valida longitud de 1920 bytes/timestamps y muestra estado.
- Nueva salida por enlaces `&format=matroska`: tracks MJPEG/PCM con timestamps explícitos y silencio cuando no se optó por micrófono. Los enlaces antiguos siguen con MJPEG/video solamente. Menor cola H.264 y codificación de salida despertada por cuadro; no se ha medido la latencia final.
- Windows: compilación final correcta, 0 errores/advertencias. ConnectionChecks --offline: 38 comprobaciones aprobadas, incluidas tres de audio (conservación, tamaño y truncamiento).
- ObsOutputChecks: cuatro pruebas de imagen aprobadas y prueba real de demultiplexado con las DLL FFmpeg 62 de OBS: 25 cuadros y 50 paquetes PCM de tono sintético, timestamps crecientes hasta 960/980 ms, tamaños PCM correctos. No prueba la reproducción HTTP en OBS ni el teléfono.
- Kotlin de captura/video/conexión/audio compila contra SDK 36. Recursos compilados con aapt2. APK completo y regeneración de View Binding para los nuevos controles de audio todavía pendientes.
- Fuente oficial OBS verificada: buffering_mb tiene valor predeterminado 2 y rango 0–16 MB. Instrucciones de la aplicación actualizadas a formato matroska y búfer 0 MB. No se modificó automáticamente la configuración del OBS del usuario: el acceso a su ventana falló al iniciar el servicio de automatización (program not found).
- CheckVirtualCamera.ps1: build 26200/x64, exportación MFCreateVirtualCamera disponible; mfvirtualcamera.h y MSVC x64 no encontrados. Proveedor nativo aún no implementado; ningún dispositivo/driver/registro creado. Requiere herramientas C++/Windows SDK y desarrollo del proveedor/puente/instalación real.
- Próximo paso: preparar herramientas nativas para cámaras virtuales. Para probar audio/latencia de la salida intermedia, actualizar ambas apps, optar por micrófono y usar enlace nuevo en OBS con formato matroska/búfer 0 MB. Medir con palmada y comprobar permisos, apagado, reconexiones y dos teléfonos.

### Multiview 80/20 y zoom continuo — 16-11-2026

- Receptor Windows compilado sin errores ni advertencias. Render y comprobación de proporción 4:1 aprobados con dos y cinco cámaras sintéticas, en 1400×900 y 1000×720; capturas revisadas. Miniaturas compactas sin estiramiento y barra horizontal cuando no caben.

- La captura del usuario muestra Xiaomi 25080RABDG y motorola moto e30 en el multiview. Reportó miniaturas demasiado grandes y pidió priorizar la vista principal y aplicar zoom durante el movimiento.
- Se cambia a proporción 4:1 entre monitor y franja inferior: miniaturas con tamaño acotado, sin estiramiento vertical y desplazamiento horizontal para cámaras adicionales.
- Se retira Aplicar zoom. Deslizador con muestreo cada 80 ms, una orden en vuelo y sólo el último valor pendiente. Confirmaciones no generan nuevas órdenes y no desplazan el deslizador durante el arrastre. Cambiar de teléfono/cámara descarta valores pendientes y valida la cámara destinataria.
- ConnectionChecks --offline: 35 comprobaciones aprobadas; cuatro nuevas verifican exclusión de envíos simultáneos, sustitución de valores intermedios, descarte al cambiar cámara y liberación tras un error.
- Esta revisión no cambia el protocolo Android; basta actualizar Windows cuando el APK de controles remotos ya está instalado. Fluidez real del zoom por USB/Wi-Fi y valor final al soltar todavía pendientes de prueba del usuario.

### Multiview y canal de control — 16-11-2026

- WPF compila sin errores ni advertencias con la nueva ventana Multiview y su botón en la barra lateral.
- ConnectionChecks --offline: 31 comprobaciones aprobadas. Las seis nuevas comprueban cámara/operación/valor, rechazo de confirmación de otro teléfono, ausencia de éxito anticipado, confirmación correcta, rechazo del hardware y valores no finitos.
- MultiviewChecks: render sintético en 1400×900 y 1000×720 con cinco cámaras emparejadas de prueba; una sexta sin emparejar se excluye. Se verifica selección conservada, controles deshabilitados sin capacidades y espacio para las cinco miniaturas. Capturas inspeccionadas visualmente.
- Todos los fuentes Kotlin, incluida MainActivity, compilan contra SDK 36 y dependencias/bindings locales. Se mantienen controles y red fuera de la Activity; la Activity sólo conecta callbacks.
- Ajustes incluidos: zoom nativo/recorte según soporte, compensación AE, autofocus y flash continuo. Se aplican en el hilo Camera2 y responden por el canal TLS existente; no hay captura de ventana en la ruta OBS.
- Pendiente: APK Gradle completo y prueba física de zoom/exposición/enfoque/linterna en USB/Wi-Fi y dos teléfonos. Las pruebas locales no certifican que cada fabricante acepte todos los controles ni miden latencia de control real.

### Salida directa OBS y retirada de 60 FPS — 16-11-2026

- Android: retirados perfiles 60 FPS normales y de alta velocidad, retirada creación de sesión restringida y añadida comprobación máxima de 30 al abrir cámara. Perfiles guardados de 60 no coinciden con la lista y vuelven a una opción disponible. Se conserva icono y persistencia.
- Windows: salida HTTP MJPEG exclusivamente loopback, independiente por UUID, botón Copiar enlace OBS y tarjeta Directa local. OBS consume una Fuente multimedia por teléfono; la salida no lee ventanas ni depende del render WPF.
- Compilación WPF aprobada en directorios intermedios/salida temporales: 0 errores y 0 advertencias. Se aisló la compilación para no interferir con archivos generados de Visual Studio.
- ConnectionChecks --offline: 25 comprobaciones aprobadas, cinco de ellas sobre autenticación/rutas por cámara, estabilidad de enlaces y formato multipart MJPEG.
- ObsOutputChecks: cuatro comprobaciones aprobadas: dimensiones verticales, giro/espejo por cuadrantes, reutilización JPEG sin recodificar y JPEG válido de señal negra.
- Kotlin de captura/video/conexión compila contra Android 36; recursos aapt2 compilados. APK Gradle completo pendiente.
- ConnectionChecks --obs-output: NO aprobado en este entorno. HTTP a 127.0.0.1:47780 bloqueado por Winsock 10013 (socket no permitido). No se pudo ejecutar la validación HTTP de dos fuentes, ni se afirma compatibilidad real en OBS hasta probarla en el equipo del usuario.
- Próxima acción: cerrar el receptor anterior, recompilar Windows y Android; iniciar teléfono a 1080p30; copiar enlace en Fuente multimedia de OBS, Archivo local desmarcado y Formato de entrada mpjpeg. Comprobar imagen y minimizar receptor, después reinicios y dos teléfonos.

### Alta velocidad aplazada — 16-11-2026

- Usuario probó 60 FPS: `No se pudo preparar cámara: Surface size 1202x2005 is not part of the high speed supported size list [1280x720, 1920x1080]`.
- La sesión detectó una superficie con dimensiones del visor en vez de un tamaño permitido. No se considera validado el modo de alta velocidad.
- Por instrucción del usuario se retira el modo: selector limitado a 15/24/30, sin crear sesiones restringidas de alta velocidad. Perfil guardado de 60 vuelve a una opción disponible de hasta 30.
- Nueva prioridad explícita: integración directa con OBS; no usar Captura de ventana como salida final.

### Alta velocidad y persistencia — 16-11-2026

- Diagnóstico del usuario: Xiaomi 25080RABDG, Android 16, trasera 0. Normal: máximo 30 FPS y duración 1080p de 33 333 333 ns tanto para encoder como visor. Encoders MTK admiten 1080p60. Alta velocidad anunciada: [30,120] / [120,120].
- Se implementa el perfil H.264 60 con sensor 120 y limitación Surface a 60 antes de codificar; icono de marca y vínculos/perfiles persistentes. Ver `docs/ALTA_VELOCIDAD_Y_RECONEXION.md`.
- Compilación Windows aprobada en directorio temporal, 0 errores/advertencias (`dotnet build ... -o <temporal> -p:NuGetAudit=false --disable-build-servers`). La compilación a la carpeta habitual encontró el EXE abierto; no se cerró la app del usuario.
- 20 comprobaciones ConnectionChecks sin sockets aprobadas: incluye guardado cifrado, recuperación de dos vínculos, rechazo con otro certificado y archivo corrupto. No valida transporte ni Windows Certificate Store físico.
- Todos los fuentes Kotlin, incluida MainActivity, compilan contra SDK Android 36 y dependencias/View Binding ya generados de la compilación local del usuario. No sustituye generar el APK completo con Gradle.
- Recursos Android compilados mediante aapt2. Pruebas de reglas de perfiles/pacing aprobadas, incluyendo elección de rango fijo 120 y rechazo de rangos variables solos.
- Pendiente: APK completo y pruebas reales de alta velocidad, reconexión, perfil persistido e icono en el launcher. No se ha confirmado 60 FPS sostenido ni se ha instalado esta revisión en el teléfono desde este entorno.

### Rendimiento reportado — 16-11-2026

- Usuario informa unos 29 FPS enviados en Android y 26–29 FPS presentados en Windows, tanto por USB como por Wi-Fi. No se especificaron resolución, formato, duración ni modelo.
- Selector sólo ofrece hasta 30 FPS aunque la cámara del fabricante admite 1920×1080/60 según el usuario.
- Se añade botón «Diagnóstico de 60 FPS» con modelo/Android, cámara, rangos AE normales, duración mínima de captura/visor 1080p, codecs AVC y rangos de alta velocidad si existen. Permite copiar el informe sin datos de conexión.
- No se habilita 60 FPS sin conocer qué filtro falla. Pendiente compilar el APK actualizado y obtener el informe de la cámara seleccionada. La nueva interfaz y consulta no han sido validadas en teléfono.

### Reinicio en vertical — 16-11-2026

- Confirmación posterior del usuario: «Ahora si esta arreglado». Se considera resuelta la distorsión reportada al detener/reiniciar en vertical por USB. No se informaron modelo, perfil, FPS ni duración; esta confirmación no valida rendimiento, Wi-Fi ni toda la matriz de cámaras.

- Reporte del usuario: al detener el envío y volver a iniciarlo por USB con el teléfono vertical, la imagen se distorsiona.
- Ajuste Android: reaplicar la transformación de TextureView al primer cuadro de cada sesión configurada; descartar transformaciones encoladas de aperturas anteriores y comprobar la generación antes de liberar recursos en una apertura obsoleta.
- Pendiente físico: iniciar en vertical, detener y reiniciar cinco veces; repetir tras giro horizontal/vertical y con ambas cámaras. Comprobar proporciones tanto en el teléfono como en Windows, con H.264 y JPEG. Registrar perfil y pantalla afectada si persiste.
- No se considera validado el arreglo hasta repetir el caso en el teléfono.

### Sesión 16-11-2026 — revisión en XAMPP

- Ubicación actual: `C:\xampp\htdocs\Movil_camara`.
- Protocolo: `dotnet run --project tools/ConnectionChecks/ConnectionChecks.csproj --disable-build-servers -- --offline` aprobado (16 comprobaciones). No valida sockets ni teléfono.
- Codec: `dotnet run --project tools/CodecChecks/CodecChecks.csproj --disable-build-servers -- --1080p60` aprobado: encode/decode H.264 real a RGB32, 8 294 400 bytes, píxel gris 97. No mide 60 FPS sostenidos.
- Windows: compilación no completada. La configuración NuGet global restringía QRCoder (NU1100); se añadió `NuGet.Config` local con mapeo explícito. El siguiente intento alcanzó la descarga, pero falló por DNS: NU1301, host desconocido `api.nuget.org`. No se generó un receptor nuevo validado.
- Android: `JAVA_HOME` del entorno apunta a `Android Studio1\jbr`, instalación incompleta. Se probó Java 21 de `C:\Program Files\Android\Android Studio\jbr` y el SDK de `C:\Users\infor\AppData\Local\Android\Sdk`, sin cambiar variables globales. Con caché temporal se superó el bloqueo de escritura del wrapper, pero Gradle no pudo conectarse a su daemon local. APK completo pendiente.
- ADB: no pudo iniciar/comunicarse con el servidor local. No se pudo determinar si hay un teléfono conectado; no se instaló ni modificó ningún dispositivo.
- Para .NET se desactivó telemetría y se usó `DOTNET_CLI_HOME` temporal para evitar el registro y la carpeta de usuario restringidos. No se modificó la configuración global del equipo.

#### Próxima acción fuera de este entorno restringido

1. Abrir Android Studio en la raíz; usar su JDK integrado válido y compilar `:app:assembleDebug`.
2. Compilar `Escritorio/MovilCamara/MovilCamara.sln` en Visual Studio con acceso a NuGet. El mapeo local de QRCoder ya está incluido.
3. Actualizar ambas aplicaciones. Probar un teléfono por USB: H.264 720p30, 1080p30 y 1080p60 sólo si se ofrece.
4. Repetir por Wi-Fi y registrar imagen local, orientación, FPS y errores con la plantilla siguiente.

Resultado de esta sesión: parcial; comprobaciones locales aprobadas, compilaciones completas y validación física pendientes. Las casillas P0 permanecen abiertas.

No hay aún una validación física documentada de la última versión H.264/selector. Los reportes anteriores del usuario confirman QR, llegada de video a Windows y vista previa USB correcta en versiones previas.

Copiar esta plantilla por cada combinación. No guardar tokens, secretos de sesión ni un QR activo.

### Plantilla de sesión

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

#### Comprobaciones

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

#### Resultado

- Aprobado / fallido / parcial:
- Error exacto y en qué pantalla aparece:
- Pasos para reproducir:
- Comportamiento esperado:
- Captura o log sin credenciales:
- Próxima acción concreta:

### Historial documentado

| Fecha | Alcance | Resultado |
|---|---|---|
| 16-11-2026 | Compilación WPF y pruebas de protocolo sin sockets | Correctas durante implementación. |
| 16-11-2026 | Clases Kotlin y recursos compilados por separado | Correctos; no sustituyen el APK completo. |
| 16-11-2026 | Encode/decode local Windows en formato 1920×1080/60 | Recupera RGB32 correcto; no mide FPS sostenidos del teléfono ni de la red. |
| Pendiente | APK actual + H.264 en teléfono real | Completar una sesión usando la plantilla. |
