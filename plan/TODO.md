# TODO - WT MultiCam

Actualizado: 18 de noviembre de 2026.
Orden: audio independiente → controles de audio en multiview → sincronización → instalador.
Ver PLAN.md para el paso a paso y PRUEBAS.md para evidencias. Una casilla de implementación no equivale a una validación física o sostenida.

## Diseño móvil aprobado — 18-11

- [x] Añadir sólo en Controles de transmisión iconos Vertical/Horizontal en una fila, fijando cámara e interfaz; restaurar política anterior al detener.
- [ ] Validar giro manual con el teléfono inmóvil, JPEG/H.264, cámara frontal/trasera, salida OBS, selección marcada y continuidad de audio/video. Verificar restauración al detener/volver y funcionamiento con rotación automática Android desactivada.

- [x] Corregir diferencia respecto del concepto: vista previa local 16:9 en preparación sin enviar video/audio ni crear codificador.
- [x] Fijar fila de iconos Micrófono/Cámara/Controles fuera del scroll, con Detener debajo; activar micrófono localmente con permiso durante captura.
- [ ] Comprobar en dispositivo transición vista previa → envío → preparación, cambio de cámara/perfil, permisos concedidos/denegados, rotación, fondo y ausencia de captura/envío en la vista previa.

- [x] Implementar conexión, preparación y transmisión con estilo azul oscuro/celeste y Detener rojo.
- [x] QR, ayuda USB, estado real de vínculo, perfiles compatibles y autorización de micrófono.
- [x] Micrófono local por sesión, ajustes desplegables y controles desplazables con superficie conservada al rotar.
- [x] Compilar/enlazar recursos y compilar fuentes Kotlin con los recursos actualizados.
- [ ] Ensamblar/reinstalar APK en Android Studio y validar QR/USB, permisos, navegación, micrófono remoto/local y errores.
- [ ] Revisar visualmente tamaños pequeños, letra grande y rotación en teléfonos reales.
- [ ] Retomar desconexiones Wi-Fi con texto/captura del error; mejora parcial de lag no confirma estabilidad.

## Estabilidad de transmisión

Prioridad sobre los hitos siguientes: resolver retraso y desconexiones de video reportados el 18-11 en USB y Wi-Fi.

- [x] Registrar confirmación de USB bastante más fluido; no extenderla a Wi-Fi, que el usuario reporta todavía deficiente.
- [x] Implementar adaptación de bitrate H.264 en Android sólo por red: presión de cola/escritura, reducción y recuperación con histéresis.
- [ ] Compilar APK e instalar en teléfonos; comprobar soporte de bitrate dinámico por fabricante y mejora física Wi-Fi con mismo perfil.
- [ ] Medir red local y tiempos por etapa si persiste; no inferir capacidad Wi-Fi sólo del tiempo de escritura TCP. Probar 720p30 como comparación controlada si 1080p30 sigue saturando.

- [x] Revisar recuperación ante pausas breves: ampliar límite de tres a ocho paquetes y evitar conversión RGB de salidas atrasadas, manteniendo decodificación de referencias AVC.
- [ ] Comparar saltos y retraso con `build/fluency-desktop`, USB y Wi-Fi, mismo perfil 1080p30 H.264. Medir antes/después sin atribuir resolución o formato por sí solos a una mejora.
- [ ] Evaluar aceleración de hardware Windows con capacidades y mediciones; la revisión actual no la implementa.

- [x] Separar recepción TLS H.264 de la decodificación y acotar a tres paquetes pendientes; tras perder dependencias, esperar siguiente cuadro clave.
- [x] Mostrar el motivo de interrupción en el estado de video, incluyendo fallos COM del decodificador.
- [ ] Validar receptor corregido con un teléfono por USB y Wi-Fi; comparar retraso del multiview y de OBS.
- [ ] Si persiste, registrar codec, resolución/FPS, CPU y texto del fallo; revisar transporte Android, watchdog de escritura y ruta de salida/UI según la etapa afectada.
- [ ] Repetir con dos teléfonos y sesión prolongada antes de declarar estable.

## Base completada

- [x] Android Kotlin/XML, receptor WPF x64 e identidad WilkensTech.
- [x] QR/TLS, USB ADB, Wi-Fi y video básico confirmados por el usuario.
- [x] Implementar perfiles por capacidades hasta 30 FPS y vínculos/perfiles persistentes cifrados.
- [x] Corregir distorsión de reinicio vertical USB; confirmación del usuario.
- [x] Multiview 80/20 y zoom continuo aceptados por el usuario.
- [x] Implementar exposición, reenfoque y linterna remotos según capacidades.
- [x] Micrófono opcional PCM/TLS y Matroska A/V; usuario confirma audio recibido.
- [x] Cámara virtual independiente por teléfono, 1080p30 NV12 y registro desde la app.
- [x] Corregir 0xC00D3E85; usuario confirma video en OBS con dos fuentes.
- [x] Retirar 60 FPS y conservar el diagnóstico como antecedente.

## 1. Audio independiente - recepción confirmada, estabilidad pendiente

- [x] Salida Matroska sólo PCM por teléfono; sin pista ni codificación de video para sus lectores.
- [x] Botón Copiar audio OBS, enlace estable y autenticación específica de audio/teléfono.
- [x] Conservar compatibilidad de enlaces A/V y MJPEG anteriores.
- [x] Mantener cola acotada, paquetes recientes y silencio cuando no llega micrófono.
- [x] Comprobar con FFmpeg una pista de audio, ausencia de video, 50 paquetes y conservación de tono/silencio.
- [x] Comprobar autorización, separación de rutas/credenciales y estabilidad de enlaces sin sockets.
- [ ] Ejecutar/aprobar prueba HTTP de dos lectores, independencia, silencio y reconexión donde el entorno lo permita.
- [x] Usuario confirma audio de la nueva Fuente multimedia en OBS, con retraso al conectar y cortes ocasionales.
- [ ] Silenciar/retirar el audio A/V anterior para evitar duplicados y verificar la nueva fuente activa.
- [ ] Probar dos teléfonos, detener/reiniciar uno y conservar sonido del otro.
- [x] Registrar recepción y problemas reportados; no dar por validada estabilidad o sincronía.
- [ ] Investigar retraso inicial y cortes de audio en fase de estabilidad/fluidez, aplazada por el usuario.

## 2. Audio en multiview - funcionamiento básico confirmado

- [x] Medidor de pico de salida por teléfono, refresco cada 150 ms y nivel cero sin muestras recientes.
- [x] Volumen 0-100 % y silencio aplicados al publicar PCM, sin modificar entrada ni otras fuentes.
- [x] Distinguir silencio de salida y captura activa; nivel previo al volumen de OBS.
- [x] Micrófono remoto con confirmación de AudioRecord y autorización explícita al iniciar la sesión Android.
- [x] Destinatario/cámara fijos y descarte de confirmaciones obsoletas; no heredar ajustes al cambiar selección.
- [ ] Probar permisos denegados, cierre, corte durante una orden y reconexión sin inicio automático.
- [x] Volumen/silencio sólo en memoria durante esta ejecución, por teléfono; autorización remota no persistida ni restaurada.
- [x] Comprobar localmente PCM, silencio, dBFS, independencia, confirmaciones y UI con dos/cinco teléfonos sintéticos.
- [ ] Probar USB/Wi-Fi y dos teléfonos; documentar necesidad de actualizar Android.

- [x] Usuario confirma el 18-11 que los controles de audio funcionan bien; no se infieren casos detallados no reportados.

## 3. Sincronización - siguiente hito

- [x] Implementar retardo manual por teléfono, 0-1000 ms en pasos de 20 ms, aplicado al PCM de salidas audio/A/V.
- [x] Añadir guía de palmada y restablecer a 0; indicar retardo de video en OBS cuando el audio llega tarde.
- [x] Mantener buffer acotado por lector; descartar pendientes al cambiar retardo, detener captura o ante atraso del temporizador.
- [x] Definir ajuste temporal por ID durante el proceso; reconexión conserva el valor, reinicio vuelve a 0. Recalibrar tras cambios de transporte/perfil.

- [ ] Medir palmada visible/audible en milisegundos por teléfono y transporte.
- [ ] Validar físicamente el rango y la corrección de audio adelantado; el audio tardío requiere retardo de video en OBS.
- [ ] Evaluar retardo de video cuando retardar audio no resuelva el desfase.
- [ ] Medir al inicio/final para distinguir desfase fijo y deriva.
- [ ] Comprobar reconexión, cambios de escena/perfil/transporte y persistencia del ajuste.
- [ ] Registrar resultados antes/después; audio recibido no demuestra sincronía.

## 4. Instalador Windows

- [x] Crear instalador beta x64 personalizado en carpeta Instaladores, con fuentes, marca, accesos y comprobación de carga.
- [x] Incluir .NET/WPF y ADB; localizar ADB junto al ejecutable antes de buscar SDK externo.
- [x] Preparar retirada de cámaras del usuario al confirmar desinstalación; conservar datos y proveedor protegido compartido.
- [ ] Probar instalación/actualización/desinstalación real en Windows 11 limpio, con y sin SDK/.NET y con OBS en uso. Pendiente comprobar escenarios de otra cuenta.
- [ ] Ajustar amplitud de controles Android en horizontal en un trabajo posterior; usuario limita entrega actual al computador y ya generó APK.

- [x] Paquete beta x64, requisito Windows 11 y .NET/WPF integrado.
- [x] Incluir receptor, proveedor y recursos sin exigir Visual Studio al usuario final.
- [ ] Elevación y actualización de DLL en uso, con rutas protegidas.
- [ ] Desinstalar dispositivos, claves COM y versiones del proveedor sin dejar huérfanos.
- [ ] Elección explícita para conservar/eliminar vínculos y preferencias.
- [ ] Instalación limpia, actualización, desinstalación y recuperación en otro equipo.
- [ ] Revisar licencias, firma y ayuda antes de distribuir.

## Validación transversal y estabilidad

- [ ] Documentar dos teléfonos durante 30 minutos; ampliar a 60 tras referencia estable.
- [ ] Anotar versiones, perfil, transporte, FPS por aplicación, CPU/GPU, memoria y temperatura accesible.
- [ ] USB+Wi-Fi combinados y desconexión independiente.
- [ ] Recuperación de las mismas fuentes tras reiniciar receptor/OBS o cambiar IP.
- [ ] Desconectar/Olvidar, PC conocido/nuevo, rechazo de confianza y permisos denegados.
- [ ] Cambios de calidad/cámara, giro/espejo y reinicios sin mezclar cuadros anteriores.
- [ ] Verificar exposición, enfoque y linterna en cada fabricante; zoom básico ya confirmado.
- [ ] Comprobar liberación de cámara, micrófono, codecs y tareas al cerrar.
- [ ] Diagnósticos sin credenciales ni video por defecto; contadores de FPS/descartes por etapa.
- [ ] Ampliar matriz de teléfonos y ejecutar las pruebas de sockets en un entorno permitido.

## Mejoras posteriores

- [ ] Nombres editables, perfiles de evento y más controles manuales según capacidades.
- [ ] Batería, temperatura, señal, bitrate y optimización RGB/decoder/UI según mediciones.
- [ ] Revisar concurrencia MediaCodec, fallos del decoder y automatización de pruebas Kotlin.
- [ ] Tally, descubrimiento mDNS, solicitud LAN desde PC y diagnóstico/limpieza ADB.
- [ ] Evaluar USB sin depuración, micrófono virtual del sistema y otros programas destino.
- [ ] Grabación local de respaldo, recuperación de archivos y timestamps de sincronización.
- [ ] Accesibilidad, idiomas, beta y matriz de compatibilidad.

## Aplazado o sustituido

60 FPS sigue retirado. Captura de ventana ya no es la salida final. Un complemento OBS propio no es requisito para la cámara virtual funcional. Micrófono virtual, 4K, Internet, otras plataformas, pagos, anuncios y tiendas quedan fuera de los próximos hitos.

## Cierre de sesión

Actualizar PLAN, TODO y PRUEBAS con alcance y siguiente paso. Respetar los cambios existentes y no crear commits/publicar sin instrucción.
