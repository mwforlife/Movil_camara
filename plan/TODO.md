# TODO — WT MultiCam

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
