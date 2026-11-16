# TODO — WT MultiCam

## Audio y cámara virtual — prioridad vigente

- [x] Corregir cierre prematuro al liberar IMFActivate: regresión reproducida con 0xC00D3E85 y comprobada con fuente viva y entrega de muestras tras liberar el activador.
- [x] Usuario confirma activación y video real en OBS tras actualizar el proveedor corregido; capturas con dos fuentes de captura e imágenes distintas.

- [x] AudioRecord opcional, permiso explícito, PCM 48 kHz mono y detención junto a cámara/ciclo de vida.
- [x] Transporte TLS autenticado y validación de audio en Windows.
- [x] Salida Matroska A/V con timestamps; reducir espera de codificación y colas de video/audio.
- [x] Verificar contenedor generado con el FFmpeg instalado con OBS.
- [x] Diagnosticar API de cámara virtual y requisitos nativos, sin registrar dispositivos.
- [x] Instalar herramientas de escritorio C++ y Windows 11 SDK; compilación/enlace C++ con MFCreateVirtualCamera comprobados el 16-11-2026.
- [x] Implementar proveedor COM Media Foundation, puente de cuadros y administrador de una cámara virtual por teléfono; compilación y muestras sintéticas comprobadas. Ver docs/CAMARA_VIRTUAL.md.
- [x] Botones Activar/Desactivar, registro COM elevado por teléfono, identidad persistente y recuperación sin solicitar elevación automática.
- [x] Pruebas nativas de activación, independencia de fuentes, comunicación entre procesos, muestras, reinicio, caducidad y liberación COM.
- [x] Usuario confirma recepción correcta del audio y funcionamiento del multiview/zoom.
- [ ] Validar activación/desactivación y enumeración real como dispositivos OBS.
- [ ] Comprobar el instalador ya implementado desde la app (UAC), acceso del servicio Frame Server y enumeración real en OBS. Desactivar retira el dispositivo; limpieza completa de COM/DLL queda para distribución.
- [ ] Comparar latencia virtual vs Matroska y comprobar sincronía con audio de la Fuente multimedia activa debajo del video virtual.
- [ ] Generar APK completo nuevo y probar micrófono/permisos, Matroska en OBS, búfer 0 MB, latencia y sincronía con palmada.
- [ ] Validar dos teléfonos, audio activado sólo donde se elija y estabilidad prolongada.
- [ ] Definir integración separada si también se requiere dispositivo de micrófono virtual.

## Revisión 80/20 y zoom continuo

- [x] Ampliar monitor a aproximadamente 80 % y reservar 20 % a una franja compacta de miniaturas.
- [x] Retirar Aplicar zoom y enviar durante el movimiento del deslizador.
- [x] Una orden de zoom en vuelo y sólo el valor más reciente pendiente; descartar pendientes al cambiar de cámara.
- [x] Probar serialización, sustitución de valores intermedios, descarte de pendientes y recuperación tras error en el emisor.
- [ ] Confirmar fluidez real y valor final del zoom al soltar por USB y Wi-Fi, sin saltos ni ajustes sobre otra cámara.

## Multiview y controles remotos — prioridad actual

- [x] Monitor seleccionado y cuadrícula ajustable de una a cuatro columnas.
- [x] Mantener vistas de teléfonos previamente emparejados cuando se desconectan; excluir sólo detectados.
- [x] Zoom, exposición por pasos, autofocus y linterna remotos, limitados por capacidades.
- [x] Cola acotada, cámara destinataria e identificador de orden con confirmación/error Android.
- [x] Compilación WPF/Kotlin, 31 comprobaciones sin sockets y render con cinco cámaras de prueba en dos tamaños.
- [ ] Compilar APK completo actualizado y probar controles reales en un teléfono, por USB/Wi-Fi.
- [ ] Dos teléfonos: verificar que el ajuste sólo afecta al seleccionado y que una desconexión no corta al otro.
- [ ] Probar corte de conexión/cambio de cámara con una orden pendiente y comprobar rechazo/caducidad.
- [ ] Considerar persistencia de zoom/exposición y ampliar controles cuando la base esté validada.

Detalles: [multiview y controles](../docs/MULTIVIEW_Y_CONTROLES.md).

## OBS directo — estado más reciente

- [x] Salida local independiente por teléfono, Fuente multimedia en OBS y botón Copiar enlace OBS; sin Captura de ventana.
- [x] Compilación WPF y cinco comprobaciones adicionales de rutas, tokens, estabilidad de enlaces y MJPEG sin sockets.
- [ ] Probar HTTP real con `ConnectionChecks --obs-output` cuando el entorno permita sockets.
- [ ] Validar OBS con un teléfono, giro/espejo, minimizar receptor y detener/reiniciar video.
- [ ] Validar dos teléfonos, independencia y cambios de escena; medir CPU/latencia de conversión MJPEG.
- [ ] Validar recuperación de OBS al reiniciar receptor y conservar escenas/enlaces.

La tarea anterior de complemento nativo queda como optimización futura. La salida vigente usa Fuente multimedia directa; no captura ventanas.

## Prioridad vigente — 16-11-2026

- [x] Retirar perfiles 60 FPS y sesión de alta velocidad de la app; perfiles disponibles hasta 30 FPS. Un perfil antiguo de 60 no se restaura.
- [ ] Validar APK actualizado con perfil 1080p30.
- [ ] Implementar fuente nativa OBS independiente por teléfono, sin Captura de ventana.
- [ ] Validar reconexión y último perfil USB/Wi-Fi, icono y dos teléfonos.
- [ ] Retomar 60 FPS sólo en una fase posterior, corrigiendo el tamaño real de las superficies. Las tareas anteriores sobre 60 quedan aplazadas.

## Implementación nueva — pendiente prueba física

- [x] Perfil H.264 de 60 FPS con sensor de alta velocidad anunciado y limitación previa al encoder (Android 10+).
- [x] Icono normal/adaptativo de WilkensTech.
- [x] Recordar último perfil utilizado por PC, transporte y cámara, validándolo contra las capacidades actuales.
- [x] Guardar vínculos cifrados Android/Windows y reintentar autenticación con TLS fijado al PC autorizado.
- [x] Comprobar recuperación local de vínculos Windows y rechazo de otra identidad/archivo corrupto.
- [ ] Compilar APK completo de esta revisión y actualizar ambas apps; emparejar una vez para guardar el vínculo nuevo.
- [ ] Medir 1080p60 de alta velocidad en Xiaomi por USB/Wi-Fi, incluyendo consumo/temperatura.
- [ ] Comprobar icono y restauración del perfil después de reiniciar Android.
- [ ] Comprobar reconexión tras reiniciar receptor/Android, cambio de IP y corte de red/USB.
- [ ] Confirmar Desconectar/Olvidar y ausencia de inicio automático de cámara.

Detalles: [alta velocidad y reconexión](../docs/ALTA_VELOCIDAD_Y_RECONEXION.md).

Consultar primero [PLAN.md](PLAN.md). Las casillas pendientes no significan necesariamente falta de código: muchas requieren prueba física.

## Implementado y comprobado localmente

- [x] Identidad WT MultiCam / WilkensTech en ambas apps.
- [x] UI Android XML con View Binding.
- [x] Receptor WPF y barra principal personalizada.
- [x] QR con token temporal y canal TLS validado por huella.
- [x] Descubrimiento UDP/TCP e IP manual implementados.
- [x] Automatización USB ADB y confianza de PC implementadas.
- [x] Apertura/cierre de vistas y ventana en espera comprobados localmente.
- [x] Vista independiente y Modo OBS implementados.
- [x] JPEG, copia YUV optimizada y cola acotada.
- [x] Selector de cámara y perfiles de resolución/FPS implementado.
- [x] Consulta de cámara y encoder hardware para filtrar H.264/60 FPS.
- [x] Encoder H.264 Surface, cola limitada y recuperación por keyframe implementados.
- [x] Receptor H.264 y conversor de color Media Foundation implementados.
- [x] Prueba local encode/decode Windows en formato 1080p60.
- [x] Comprobaciones de protocolo, geometría, YUV y reglas de FPS.

## P0 — Próxima sesión: versión actual en un teléfono

- [x] Corregir distorsión al detener/reiniciar en vertical por USB: usuario confirmó solución el 16-11-2026. Rendimiento y otros modos siguen pendientes.

- [ ] Compilar el APK completo de la última versión, incluidas Activity y clases generadas.
- [ ] Instalar ambas versiones actualizadas y anotar sus identificadores.
- [ ] Comprobar listado real por cámara: resoluciones, FPS y formato.
- [ ] Confirmar H.264 720p30 en USB.
- [ ] Confirmar H.264 1080p30 en USB.
- [ ] Si se ofrece, medir H.264 1080p60 en USB.
- [ ] Repetir H.264 por Wi-Fi.
- [ ] Confirmar JPEG de compatibilidad tras los cambios nuevos.
- [ ] Confirmar que el visor del teléfono no se deforma por Wi-Fi.
- [ ] Confirmar orientación, espejo y proporción en ambas cámaras.
- [ ] Probar cambio de perfil y de cámara, sin mezclar frames de sesiones anteriores.
- [ ] Validar que un perfil rechazado dé un error útil y permita volver al selector.
- [ ] Registrar todo en [PRUEBAS.md](PRUEBAS.md).

Cierre P0: prueba real documentada de un teléfono. No marcar 1080p60 por una prueba de formato local.

## P1 — Multicámara y estabilidad

- [ ] Dos teléfonos simultáneos con fuentes independientes.
- [ ] Un teléfono USB y otro Wi-Fi.
- [ ] Desconectar uno sin interrumpir el otro.
- [ ] Sesión sostenida de 30 minutos con un teléfono como referencia.
- [ ] Sesión de 60 minutos con dos teléfonos, tras superar la referencia.
- [ ] Registrar crecimiento de memoria, CPU/GPU y temperatura.
- [ ] Validar corte y recuperación de Wi-Fi.
- [ ] Validar desconexión/reconexión USB.
- [ ] Validar reinicio del receptor con identidad persistente.
- [ ] Probar PC USB conocido sin QR y PC nuevo con autorización única.
- [ ] Probar rechazo/olvido de confianza y evitar reconexión contra la voluntad del usuario.
- [ ] Comprobar cámara y tareas liberadas al detener o cerrar.
- [ ] Ejecutar pruebas locales con sockets fuera del entorno bloqueado.
- [ ] Diagnóstico exportable sin credenciales ni video por defecto.
- [ ] Contadores separados: captura, encode, envío, decode, presentación, descartes e IDR.
- [ ] Medir latencia real cámara-pantalla con un estímulo visible común.
- [ ] Elegir y aplicar optimizaciones según el cuello medido.
- [ ] Integrar pruebas Kotlin de tools/PreviewChecks en una suite ejecutable reproducible.
- [ ] Validar varios fabricantes, Android antiguo/gama media y distintas cámaras frontales.

## P2 — OBS y otros programas

- [ ] Probar Captura de ventana con video real en OBS.
- [ ] Dos ventanas, dos fuentes y cambios de escena.
- [ ] Comprobar Modo OBS, cierre flotante, Escape y reapertura.
- [ ] Mantener identidad de fuente al reconectar.
- [ ] Decidir primera integración profesional: plugin OBS o cámara virtual.
- [ ] Documentar alcance, dependencias y compatibilidad de esa decisión.
- [ ] Implementar una fuente independiente por teléfono en la integración elegida.
- [ ] Investigar cámara virtual de Windows 11 para aplicaciones distintas de OBS.
- [ ] Decidir múltiples webcams virtuales, salida seleccionable o ambas.
- [ ] Validar cada programa destino antes de anunciar compatibilidad.

## P3 — Control de cámaras y producción

- [ ] Nombres de cámara persistentes y editables.
- [ ] Guardar perfil de calidad por teléfono/cámara.
- [ ] Perfiles de evento y restauración segura.
- [ ] Zoom, enfoque, exposición y linterna según capacidades.
- [ ] Balance de blancos, ISO y obturación cuando estén disponibles.
- [ ] Indicadores de señal, batería y temperatura accesible.
- [ ] Bitrate H.264 configurable/adaptativo.
- [ ] Tally de programa/vista previa mediante OBS.
- [ ] Retomar solicitud LAN desde PC reportada como fallida.
- [ ] Evaluar mDNS/DNS-SD como alternativa al barrido de red.
- [ ] Mejorar gestión, limpieza y diagnóstico de túneles ADB.
- [ ] Evaluar un modo USB comercial que no requiera configuración de desarrollo.

## P4 — Audio y respaldo

- [ ] Micrófono opcional y fuente principal elegida explícitamente.
- [ ] Sincronía audio/video por sesión.
- [ ] Ajuste y medición de desfase entre teléfonos.
- [ ] Monitoreo sin eco ni micrófonos duplicados por defecto.
- [ ] Evaluar audio fuera de OBS y necesidad de micrófono virtual.
- [ ] Grabación local de respaldo y recuperación de archivos.
- [ ] Registro de timestamps para sincronizar respaldos.

## P5 — Distribución y producto

- [ ] Matriz de compatibilidad y límites comprobados.
- [ ] Instalador/desinstalador Windows.
- [ ] Firma de ejecutables y paquetes.
- [ ] Actualizaciones verificadas y recuperación ante fallo.
- [ ] Política de datos, permisos y diagnóstico.
- [ ] Onboarding y ayuda de red/USB.
- [ ] Idiomas y revisión de accesibilidad.
- [ ] Beta con usuarios y correcciones priorizadas.
- [ ] Definir licencia y precio después de validar uso real.
- [ ] Revisar licencias/dependencias antes de distribución.
- [ ] Evaluar macOS, Linux, iOS, 4K y acceso remoto como alcance posterior.

## Pendientes técnicos observados para revisar

- [ ] Revisión de recursos y concurrencia de MediaCodec al cambiar perfiles repetidamente.
- [ ] Revisión de memoria/asignaciones RGB y presentación WPF a 1080p60.
- [ ] Manejar fallos del decoder durante una sesión con mensajes más específicos.
- [ ] Revisar el emparejamiento persistente LAN; las sesiones actuales están en memoria.
- [ ] Confirmar comportamiento del estado USB reportado por Xiaomi y otros fabricantes.
- [ ] Actualizar documentación histórica cuando contradiga el modo actual, manteniendo referencia de versiones.

## Fuera del siguiente hito

No priorizar ahora: pagos, anuncios, publicación en tiendas, 4K, IA visual, conexión por Internet o cuatro cámaras 1080p60. Primero cerrar P0 y P1.

## Regla para marcar tareas

[x] significa completada con evidencia suficiente para el alcance de esa casilla. Si sólo hay código, escribirlo explícitamente. Si falta teléfono, OBS o una prueba sostenida, dejar la validación pendiente.
