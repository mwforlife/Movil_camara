# Interfaz Android — WT MultiCam

Implementación del concepto aprobado el 18-11-2026: fondo azul oscuro, tarjetas con borde discreto, acciones celestes, texto claro y botón de detener rojo. Se conserva el emblema existente de WilkensTech.

## Recorrido

1. **Conectar:** escanear QR, ayuda de conexión USB y tarjeta del computador conectado con transporte/IP reales. El vínculo confirmado abre Preparar cámara. Volver permite consultar conexión sin desvincular.
2. **Preparar cámara:** vista previa real 16:9 con permiso de cámara, selección de cámara, perfil compatible y consentimiento de micrófono, seguido de Iniciar transmisión. El perfil combina resolución/FPS/formato para evitar combinaciones inválidas. La vista previa sólo configura una superficie TextureView, sin codificador, ImageReader, envío de video ni captura de audio. Cambiar el perfil reinicia el encuadre local; salir de preparación o pasar al fondo lo detiene.
3. **En transmisión:** imagen real, estado recibido del emisor con transporte, resumen del perfil configurado y cifras reales de envío cuando existen. No se muestran FPS/bitrate inventados ni se confunden FPS configurados con medidos. Fila fija con iconos Micrófono, Cámara y Controles, con Detener debajo, fuera del contenido desplazable. Micrófono puede solicitar permiso local y activarse durante la captura aunque no se hubiera marcado al iniciar; autorización remota limitada a esa sesión. Cámara detiene y vuelve a preparar la siguiente cámara. Controles despliega estado y acceso al cambio de calidad, que vuelve a preparación deteniendo el envío.

Opciones de conexión reúne direcciones, pegar código, desconectar y olvidar vínculos. USB explica el procedimiento real de autorización y detección desde Windows; no simula un vínculo ni presenta una lista de computadores disponibles sin datos.

## Comportamiento y accesibilidad

- Sólo dentro de **Controles** de transmisión aparece una fila con dos iconos: **Vertical** y **Horizontal**. Fijan la orientación completa de la actividad; el visor, los controles y la rotación enviada siguen la pantalla fijada, independientemente de la postura física. No hay selector en preparación ni modo automático adicional. Al detener, salir o desconectar se restaura la política de orientación anterior. Cambio en caliente mediante `configChanges`, sin iniciar otra transmisión deliberadamente; validar continuidad en los teléfonos.

- Atrás desde transmisión detiene captura y vuelve a preparación. Desde preparación vuelve a conexión. Ir al fondo conserva la política existente de detener cámara/micrófono.
- Controles táctiles con altura mínima 48 dp, conexión/preparación desplazables, textos traducibles y contraste con fondo oscuro.
- La disposición de cámara conserva la superficie al girar. En horizontal los controles van al costado y disponen de desplazamiento; en vertical se limita su altura para reservar espacio al visor.
- El estado de envío y el de audio permanecen en el área desplazable aunque se oculten ajustes. Acciones principales y Detener se reservan fuera de esa área; su altura real ajusta el margen del contenido. Un error de cámara abre sus detalles.
- No modifica protocolo ni elimina los cambios previos de estabilidad/bitrate. Windows no requiere actualización por este diseño.

## Verificación

Recursos compilados y enlazados con AAPT2, usando Android 36 y el paquete local previo para símbolos de dependencias. Todos los fuentes Kotlin compilados con los recursos nuevos, dependencias locales y tipos View Binding existentes, código de salida 0. Se conservaron IDs/tipos de las vistas enlazadas; los nuevos grupos/acciones se localizan mediante tags XML.

Esto no sustituye el ensamblado completo de Gradle, regeneración de View Binding ni prueba en dispositivo. El APK sigue pendiente de recompilar desde Android Studio debido al bloqueo de acceso de Gradle de este entorno. No hay capturas reales de la nueva interfaz todavía.

Comprobar en teléfono: QR/USB y autorización, volver sin perder vínculo, cambiar perfiles, permiso de micrófono concedido/denegado, captura y control remoto, rotación vertical/horizontal, letra grande, ocultar/mostrar controles, detener, errores y salida al fondo. Verificar por separado las desconexiones Wi-Fi pendientes.
