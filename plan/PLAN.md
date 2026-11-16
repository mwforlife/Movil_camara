# PLAN — WT MultiCam

## Estado más reciente — audio, latencia y cámara virtual

El usuario confirmó multiview, zoom y recepción correcta del audio. La salida Matroska A/V sigue disponible; la latencia física y la sincronía aún deben medirse.

Implementado proveedor COM Media Foundation x64, puente de último cuadro NV12 y administrador de una cámara virtual por teléfono. C++/SDK instalados y compilación comprobada. Botones Activar/Desactivar cámara virtual, registro elevado del proveedor por teléfono y recuperación del dispositivo al abrir el receptor. Salida inicial fija 1080p30, conservando proporción y orientación. El video virtual evita HTTP/MJPEG; el audio permanece en la Fuente multimedia. **El usuario confirmó activación y video real en OBS tras corregir 0xC00D3E85. Las capturas muestran dos teléfonos en el multiview y dos fuentes de captura con imágenes distintas en OBS.** Ver [cámaras virtuales: instalación y pruebas](../docs/CAMARA_VIRTUAL.md).

Próximo paso: validar dos cámaras durante 30 minutos, desconectar/reconectar una sin afectar a la otra, recuperar las mismas fuentes tras reiniciar receptor/OBS y medir latencia/sincronía. La activación básica ya fue confirmada por el usuario. 60 FPS continúa aplazado.

## Ajuste actual del multiview — 80/20 y zoom continuo

Por solicitud del usuario se reserva aproximadamente 80 % para el monitor y 20 % para miniaturas compactas en franja horizontal. Se retira Aplicar zoom: el deslizador transmite cambios agrupados cada 80 ms, con una orden en vuelo y el último valor pendiente, sin acumulación. Mantiene confirmación Android y destinatario fijo. Actualizar el receptor y comprobar arrastre de zoom por USB/Wi-Fi; Android con controles remotos no requiere cambios de protocolo.

## Multiview — siguiente validación

Implementado multiview WPF con cuadrícula de cámaras vinculadas, monitor seleccionado y controles remotos de zoom, compensación de exposición, autofocus y linterna según capacidades. Canal TLS autenticado y confirmación Android por orden. Ver [multiview y controles](../docs/MULTIVIEW_Y_CONTROLES.md).

Próximo paso: compilar/actualizar ambas apps, iniciar una cámara y abrir Abrir multiview en el receptor; comprobar ajustes reales por USB y Wi-Fi. Después dos teléfonos, selección independiente y desconexión durante una orden. UI y código comprobados localmente; controles físicos todavía pendientes. No hay conmutación de programa/tally/audio en esta revisión.

## Salida OBS implementada — estado más reciente

Se añade salida directa local por UUID mediante Fuente multimedia de OBS, con enlaces estables y botón Copiar enlace OBS. No necesita capturar ventanas ni instalar complemento. Compilación y protocolo comprobados localmente; pendiente prueba real en OBS. Ver [salida directa](../docs/SALIDA_OBS_DIRECTA.md). Próximo paso: actualizar receptor y APK limitado a 30 FPS, añadir una Fuente multimedia y validar imagen, orientación, minimizar, reconexión y después dos teléfonos. Un complemento propio o webcam virtual es una mejora futura, no la salida actual.

## Prioridad vigente — 16-11-2026

Por instrucción del usuario, se retira por ahora el modo 60 FPS y se mantiene salida hasta 1080p30 según capacidades. La prueba Xiaomi falló al preparar la sesión de alta velocidad: `Surface size 1202x2005 is not part of the high speed supported size list [1280x720, 1920x1080]`. Se aplaza su investigación.

El usuario descarta Captura de ventana como integración final con OBS. La siguiente entrega es una fuente nativa independiente por teléfono mediante complemento OBS; revisar SDK y entorno de compilación. Mantener icono y persistencia de vínculos/perfiles. No priorizar monetización.

## Actualización de implementación — 16-11-2026

Ver [alta velocidad y reconexión](../docs/ALTA_VELOCIDAD_Y_RECONEXION.md). Se implementan perfiles 60 FPS mediante sesión de alta velocidad anunciada (Xiaomi: sensor 120, limitación a 60 antes del encoder), icono WilkensTech, último perfil por PC/transporte/cámara y vínculos persistentes cifrados Android/Windows. La distorsión de reinicio vertical USB fue confirmada como corregida por el usuario. Reportó 29 FPS en Android y 26–29 en Windows por USB/Wi-Fi a un perfil de hasta 30, sin especificar resolución/formato/duración.

Próximo paso: cerrar el receptor, recompilar ambas apps, actualizar conservando datos, emparejar una vez y probar 1080p60 de alta velocidad. Después comprobar vínculo y perfil tras reiniciar ambas apps y cambiar de red/USB. La nueva implementación no está todavía validada físicamente. Las referencias históricas a sesiones sólo en memoria o ausencia de alta velocidad quedan sustituidas por esta actualización.

Actualizado: 16 de noviembre de 2026.
Empresa: WilkensTech.
Carpeta del proyecto: C:\xampp\htdocs\Movil_camara.

Este es el punto de entrada para continuar el desarrollo. El trabajo ejecutable está en [TODO.md](TODO.md) y las evidencias físicas se anotan en [PRUEBAS.md](PRUEBAS.md).

## 1. Qué sigue ahora

El siguiente hito es **estabilidad y recuperación de dos cámaras virtuales**, con una sesión de 30 minutos, reconexión independiente y medición de latencia/sincronía. Activación y video básico en OBS confirmados por el usuario.

No considerar terminado 1080p60 por haber añadido un perfil o superado una prueba local de codec. Falta medir captura, envío, recepción y presentación con el hardware real.

## 2. Objetivo del producto

Usar varios teléfonos Android como cámaras independientes para streaming, con receptor Windows, control centralizado e integración con OBS y otros programas.

Cada teléfono debe conservar su propia fuente. Una cuadrícula combinada no sustituye las fuentes individuales.

Nombre aprobado: **WT MultiCam**. Mantener los logos y la identidad de WilkensTech.

## 3. Estado al dejar el proyecto

| Área | Implementación actual | Evidencia y pendiente |
|---|---|---|
| Android | Kotlin, XML y View Binding | El usuario confirmó compilaciones de versiones anteriores. Falta confirmar el APK completo de la última versión con selector y H.264. |
| Windows | WPF/C#, .NET 8; barra personalizada | Compila localmente sin errores ni advertencias. |
| Emparejamiento LAN | QR/código, TLS y huella del receptor | QR y recepción de video confirmados por el usuario en versiones anteriores. |
| Descubrimiento | UDP más búsqueda TCP local y botón Buscar ahora | El teléfono apareció en el panel del usuario. No equivale a estar emparejado. |
| Solicitud desde PC | Invitación y comparación de código | El usuario reportó rechazo; dejó esta opción para después. El flujo visible prioriza QR. |
| USB | ADB reverse/forward; conexión automática para PCs recordados | Usuario confirmó video USB y vista previa USB correcta. La última automatización/persistencia requiere prueba física específica. |
| Video JPEG | Captura YUV, conversión optimizada, TLS y visor alternativo Wi-Fi | Usuario confirmó video en Windows. La última corrección del visor local Wi-Fi aún debe confirmarse. |
| Video H.264 | Encoder hardware Android, protocolo v2 y Media Foundation en Windows | Clases Kotlin/recursos comprobados por separado. Prueba real local de encode/decode Windows en formato 1080p60 superada; pendiente Android-PC real. |
| Calidad | Selector por cámara, resolución, FPS y formato | Consulta Camera2 y codecs. Hasta 1080p60 si los datos reportados lo permiten; falta confirmar la lista en teléfonos físicos. |
| Fuentes | Ventanas independientes para Captura de ventana en OBS | Apertura y cierre de ventana comprobados localmente. Falta prueba completa en OBS del usuario. |
| Multicámara | Sesiones y vistas separadas por UUID | Diseñado e implementado para varios dispositivos; falta validación física simultánea. |
| Audio | No implementado | Pendiente. |
| Plugin OBS / cámara virtual | No implementados | La salida actual es Captura de ventana. |

### Límites que deben quedar claros

- No hay certificación de 1080p60 sostenido, multicámara ni latencia de extremo a extremo.
- La prueba local del codec comprueba formato y píxeles, no transmisión real a 60 FPS.
- La consulta de capacidades no garantiza que toda combinación de superficies abra y funcione sostenidamente.
- Se implementan sesiones restringidas de alta velocidad para salida 60 FPS desde rangos fijos anunciados; no se ofrece salida 120 FPS ni modos privados de fabricantes.
- El receptor usa Media Foundation; no hay ruta explícita de presentación D3D/DXVA.
- USB automático requiere ADB, depuración autorizada, cable de datos y app abierta.
- La cámara se inicia por acción del usuario; emparejar o conectar USB no inicia la captura.
- La identidad del PC persiste en el almacén de certificados. Los vínculos se guardan cifrados en Android y Windows; validar recuperación física de esta revisión.
- El entorno del asistente ha bloqueado TCP local y la comunicación con Gradle. Compilar y probar en el entorno del usuario antes de dar un hito por validado.

## 4. Reglas de trabajo acordadas

- Mantener Android en la raíz y Windows dentro de Escritorio.
- Diseños Android en XML; Activity para ciclo de vida y eventos. Captura, codec, transporte y capacidades en clases separadas.
- Mantener WPF y la barra personalizada arrastrable con controles de ventana.
- Dar prioridad a QR y USB para las pruebas; no bloquear el video por la solicitud LAN aún pendiente.
- No ofrecer perfiles inventados ni forzar 60 FPS cuando faltan datos suficientes.
- Conservar JPEG como modo de compatibilidad.
- Registrar qué pasó realmente en el teléfono y qué sólo pasó en pruebas locales.
- No introducir licencias o monetización antes de validar estabilidad y utilidad.
- No modificar otros proyectos de WilkensTech para trabajar en WT MultiCam.
- No publicar, subir versiones, crear commits o hacer push sin una instrucción que lo autorice.

## 5. Paso a paso para retomar

### Paso 1 — Leer y revisar el estado

1. Leer este archivo y [TODO.md](TODO.md).
2. Revisar los resultados nuevos de [PRUEBAS.md](PRUEBAS.md).
3. Ejecutar git status y respetar los cambios del usuario. Hay archivos nuevos y cambios preparados previamente; no resetear ni limpiar el repositorio.
4. Abrir Android Studio en la carpeta principal.
5. Abrir Escritorio/MovilCamara/MovilCamara.sln en Visual Studio.

### Paso 2 — Compilar la última versión completa

1. Sincronizar Gradle y comprobar el JDK configurado en Android Studio.
2. Compilar el APK completo, incluyendo MainActivity y las clases generadas por View Binding.
3. Compilar Windows.
4. Instalar/actualizar Android conservando los datos si se desea conservar la confianza USB.
5. Anotar versiones del APK, receptor, teléfono y sistema operativo.

Comandos desde la raíz, con SDK/JDK y permisos adecuados:

~~~powershell
.\gradlew.bat :app:assembleDebug
dotnet build Escritorio/MovilCamara/MovilCamara.csproj
dotnet run --project tools/ConnectionChecks/ConnectionChecks.csproj -- --offline
dotnet run --project tools/CodecChecks/CodecChecks.csproj -- --1080p60
~~~

Las comprobaciones CLI de geometría, YUV, perfiles y pacing están en tools/PreviewChecks; todavía no están integradas como tests Gradle. Las pruebas Android existentes de plantilla no cubren estas funciones.

Para comprobar sockets locales, cerrar el receptor antes de ejecutar:

~~~powershell
dotnet run --project tools/ConnectionChecks/ConnectionChecks.csproj
~~~

Ese comando utiliza los mismos puertos que la app. No afirmar que pasó si el sistema bloquea el transporte.

### Paso 3 — Probar un teléfono con H.264

1. Conectar por USB o QR.
2. Confirmar que aparece como emparejado.
3. Anotar los perfiles que muestra cada cámara.
4. Elegir H.264 1280×720/30 para una prueba inicial corta.
5. Probar 1920×1080/30.
6. Probar 1920×1080/60 sólo si aparece.
7. Comparar FPS enviados en Android con FPS mostrados en Windows.
8. Verificar orientación vertical/horizontal, giro a ambos lados, espejo frontal y proporciones.
9. Detener, cambiar calidad y reiniciar sin volver a emparejar.
10. Repetir por Wi-Fi. Confirmar especialmente el visor del teléfono, que fue el punto con distorsión.
11. Si H.264 falla, guardar el error y probar JPEG para separar problemas de conexión de problemas de codec.

### Paso 4 — Probar reconexión y permisos

1. Desenchufar/reconectar USB con la app abierta.
2. Comprobar PC recordado sin QR y PC nuevo con confirmación única.
3. Comprobar rechazo de autorización y Olvidar PCs USB.
4. Cortar/restablecer Wi-Fi.
5. Reiniciar el receptor; observar el nuevo emparejamiento y la recuperación de la fuente.
6. Enviar Android al fondo y volver. Debe detenerse la captura sin dejar cámara ni tareas huérfanas.
7. Probar permiso de cámara denegado y otro programa usando la cámara.

### Paso 5 — Validar multicámara

1. Conectar dos teléfonos con UUID diferentes.
2. Iniciar perfiles moderados (por ejemplo, 720p30).
3. Abrir una fuente independiente por teléfono.
4. Detener o desconectar uno: el otro debe continuar.
5. Repetir con un dispositivo USB y otro Wi-Fi.
6. Probar una sesión sostenida y registrar CPU, memoria, temperatura y FPS.
7. Subir calidad gradualmente. No saltar directamente a dos o cuatro cámaras a 1080p60 sin una referencia medida.

### Paso 6 — Validar OBS

1. Abrir una fuente por teléfono.
2. Activar Modo OBS para ocultar controles.
3. Agregar cada ventana como fuente separada en OBS.
4. Probar escenas, cambios de escena, cierre/reapertura y ausencia de overlays.
5. Verificar Cerrar, Escape y cierre flotante con el puntero.
6. Registrar resultado real y limitaciones del método de captura.

### Paso 7 — Corregir según evidencia

Orden recomendado: errores de compilación/apertura; corrupción o rotación; pérdida de sesión; acumulación de latencia; FPS/temperatura; detalles visuales.

No seguir añadiendo funciones si el flujo de un teléfono aún falla.

## 6. Fases después de la validación inicial

| Fase | Entrega | Condición para avanzar |
|---|---|---|
| P0: base real | APK y receptor probados con un teléfono | Imagen correcta, perfiles válidos y resultados documentados. |
| P1: estabilidad y rendimiento | Dos cámaras, diagnóstico, recuperación y sesiones sostenidas | Sin mezclar fuentes, fugas crecientes ni bloqueos sin recuperación. |
| P2: integración profesional | Fuente OBS nativa y/o cámara virtual, decisión documentada | Conservar fuentes independientes y validar programas destino. |
| P3: control de producción | Controles remotos, nombres, perfiles de evento y tally | Recuperación y compatibilidad por capacidad comprobadas. |
| P4: audio y sincronía | Audio elegido explícitamente y desfase ajustable | Medición de deriva, eco y sincronía entre sesiones. |
| P5: distribución | Instaladores, firma, actualizaciones y soporte | Beta estable en matriz de dispositivos y equipos. |

### Rendimiento: siguiente decisión técnica

Medir antes de cambiar el motor otra vez. Identificar si el límite está en captura, encode, red, decode, conversión de color o presentación.

Prioridades probables después de medir:
- Reducir asignaciones y copias RGB en Windows.
- Decodificar/presentar fuera del hilo UI y evaluar D3D11/DXVA si ese es el cuello.
- Ajustar bitrate H.264 según congestión sin perder la recuperación por IDR.
- Exponer pérdidas de cola, cuadros clave, reconexiones y FPS por etapa.
- Evaluar WebRTC/SRT u otro transporte sólo con una necesidad y comparación concretas.

## 7. Mapa del código

| Ruta | Responsabilidad |
|---|---|
| app/src/main/res/layout/ | Pantallas XML y controles de calidad. |
| app/src/main/java/com/wilkenstech/movilcamara/MainActivity.kt | Eventos, permisos, selector y ciclo de vida. |
| app/src/main/java/com/wilkenstech/movilcamara/camera/ | Captura, capacidades, geometría, pacing y copia YUV. |
| app/src/main/java/com/wilkenstech/movilcamara/video/ | Encoder H.264 y envío multimedia. |
| app/src/main/java/com/wilkenstech/movilcamara/connection/ | Descubrimiento, confianza y TLS. |
| Escritorio/MovilCamara/Services/ConnectionHub.cs | Emparejamiento, sesiones y recepción. |
| Escritorio/MovilCamara/Services/H264Decoder.cs | Decodificación y conversión nativas de Windows. |
| Escritorio/MovilCamara/Services/UsbAutoConnector.cs | Detección y preparación USB. |
| Escritorio/MovilCamara/VideoOutputWindow.xaml | Vista independiente y Modo OBS. |
| tools/ConnectionChecks/ | Pruebas de protocolo y conexiones. |
| tools/CodecChecks/ | Prueba local real de encode/decode Windows. |
| tools/PreviewChecks/ | Comprobaciones Kotlin sin hardware. |

## 8. Documentación técnica

- [H.264 y 1080p60](../docs/H264_1080P60.md): estado técnico más reciente del video.
- [Conexiones](../docs/CONEXIONES.md).
- [USB automático](../docs/USB_AUTOMATICO.md).
- [Vista previa/orientación](../docs/VISTA_PREVIA_ORIENTACION.md).
- [Fluidez y visor Wi-Fi](../docs/FLUIDEZ_Y_VISTA_WIFI.md).
- [Propuesta original](../docs/PLAN_PRODUCTO.md): contexto e ideas; no usar sus fases iniciales como estado actual.

## 9. Cierre de cada sesión de trabajo

1. Marcar en TODO sólo lo terminado y probado; separar implementación de validación física.
2. Registrar pruebas, errores y configuraciones concretas.
3. Actualizar este archivo si cambia arquitectura, alcance o siguiente hito.
4. Documentar comandos y bloqueos reales, sin tokens, claves o códigos QR activos.
5. Dejar indicado el próximo paso que permita continuar sin depender del chat.
