# TODO - WT MultiCam

Actualizado: 17 de noviembre de 2026.
Orden: audio independiente → controles de audio en multiview → sincronización → instalador.
Ver PLAN.md para el paso a paso y PRUEBAS.md para evidencias. Una casilla de implementación no equivale a una validación física o sostenida.

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

## 2. Audio en multiview - implementado, prueba física pendiente

- [x] Medidor de pico de salida por teléfono, refresco cada 150 ms y nivel cero sin muestras recientes.
- [x] Volumen 0-100 % y silencio aplicados al publicar PCM, sin modificar entrada ni otras fuentes.
- [x] Distinguir silencio de salida y captura activa; nivel previo al volumen de OBS.
- [x] Micrófono remoto con confirmación de AudioRecord y autorización explícita al iniciar la sesión Android.
- [x] Destinatario/cámara fijos y descarte de confirmaciones obsoletas; no heredar ajustes al cambiar selección.
- [ ] Probar permisos denegados, cierre, corte durante una orden y reconexión sin inicio automático.
- [x] Volumen/silencio sólo en memoria durante esta ejecución, por teléfono; autorización remota no persistida ni restaurada.
- [x] Comprobar localmente PCM, silencio, dBFS, independencia, confirmaciones y UI con dos/cinco teléfonos sintéticos.
- [ ] Probar USB/Wi-Fi y dos teléfonos; documentar necesidad de actualizar Android.

## 3. Sincronización

- [ ] Medir palmada visible/audible en milisegundos por teléfono y transporte.
- [ ] Definir signo/rango y aplicar ajuste según audio adelantado o tardío.
- [ ] Evaluar retardo de video cuando retardar audio no resuelva el desfase.
- [ ] Medir al inicio/final para distinguir desfase fijo y deriva.
- [ ] Comprobar reconexión, cambios de escena/perfil/transporte y persistencia del ajuste.
- [ ] Registrar resultados antes/después; audio recibido no demuestra sincronía.

## 4. Instalador Windows

- [ ] Paquete x64, requisitos Windows 11 y política de .NET.
- [ ] Incluir receptor, proveedor y recursos sin exigir Visual Studio al usuario final.
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
