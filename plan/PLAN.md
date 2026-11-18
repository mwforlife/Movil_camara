# PLAN - WT MultiCam

Actualizado: 18 de noviembre de 2026.
Proyecto: C:\xampp\htdocs\Movil_camara. Empresa: WilkensTech.

Punto de entrada para retomar: [TODO.md](TODO.md) contiene tareas y [PRUEBAS.md](PRUEBAS.md) conserva evidencia, validaciones pendientes e historial. El estado de este archivo sustituye instrucciones antiguas de captura de ventana o de volver a ofrecer 60 FPS.

## Estado vigente

Entrega de instalador Windows solicitada el 18-11: el usuario ya generó APK y limita el trabajo a computador. Preparado asistente Inno Setup personalizado en `Instaladores/Windows`, receptor x64 con .NET/WPF 8.0.31 integrados, ADB y proveedor nativo. No necesita descarga .NET separada. Fuentes y guía en [Instaladores](../Instaladores/README.md). Publicación y carga del runtime/proveedor comprobadas; falta instalación/actualización/desinstalación en equipo limpio. Se conservan datos y componentes protegidos compartidos al desinstalar. No se ejecuta instalación sobre el equipo actual. El usuario confirma orientación manual funcional, pero controles horizontal siguen estrechos: ajuste visual pendiente, fuera del alcance final Windows de esta entrega.

Último ajuste solicitado: sólo en Controles de transmisión se incorporan dos iconos en una fila, Vertical/Horizontal. Fijan toda la actividad y la orientación de cámara/salida mediante la rotación de pantalla existente; al detener se restaura la política anterior. No se añade selector a preparación. Validación en teléfono pendiente.

Corrección tras revisión del usuario: faltaba la vista previa de preparación y la fila de acciones del concepto. Ahora se implementa visor local real 16:9 antes de transmitir, con permiso de cámara y sin codificación/envío/audio. Se reutiliza el controlador de cámara cambiando su superficie; controles remotos no se habilitan en modo vista previa. En transmisión se deja fila fija con iconos Micrófono/Cámara/Controles y Detener debajo; micrófono permite autorización local durante el envío. Validación física pendiente tras recompilar Android. Esta revisión sustituye la limitación anterior de no abrir cámara en preparación.

Último trabajo autorizado: implementar el diseño móvil aprobado. Android ahora organiza Conectar → Preparar cámara → En transmisión, con identidad azul/celeste, tarjetas, controles desplegables y parada separada. Perfiles compatibles y autorizaciones existentes se conservan; no hay vista previa ficticia ni lista de PCs inventada. Recursos AAPT2 y compilación Kotlin comprobados; APK/validación visual en teléfono pendientes. Sólo Android requiere recompilar para el diseño. Ver [diseño móvil](../docs/DISENO_MOVIL.md). La última prueba de red indica lag algo mejor, pero desconexiones frecuentes; causa pendiente de mensaje/captura del fallo. No se considera estable Wi-Fi.

Última confirmación del usuario: USB bastante más fluido; Wi-Fi sigue funcionando mal. Prioridad actual: transmisión por red. Se añade en Android control adaptativo del bitrate H.264 por presión de cola/tiempo de escritura, sólo para destino no loopback; USB mantiene su política. Reducción multiplicativa hasta 25 % y recuperación lenta tras ocho ventanas sanas, limitada al bitrate admitido por el codec. Conserva resolución y FPS configurados; puede reducir detalle. Es una heurística de congestión de envío, no una medida de ancho de banda/RTT. Requiere recompilar/instalar Android; receptor Windows `build/fluency-desktop` se conserva. Falta prueba física por Wi-Fi.

Revisión de fluidez posterior: por USB el usuario mantiene conexión, pero observa saltos y retraso ocasionales. La captura de DroidCam muestra 1080p AVC/H.264 y una opción de aceleración de hardware, sin demostrar que esté activa. En WT MultiCam se conserva H.264 y se mejora recuperación: cola acotada de ocho paquetes (sustituye tres), decodificar referencias pendientes sin convertir sus salidas viejas a RGB y presentar el cuadro reciente. Esto reduce trabajo de conversión/asignación al alcanzar el tiempo actual; no habilita aceleración GPU ni garantiza latencia. Probar Windows en `build/fluency-desktop`; Android sin cambios. Validación física pendiente.

Prioridad actual (18-11, reporte posterior): el usuario informa mucho retraso de video y desconexiones frecuentes tanto por USB como por Wi-Fi. Se retoma estabilidad antes de continuar calibración/instalador. Primera corrección Windows: lectura H.264 separada de decodificación, cola máxima de tres paquetes con recuperación por cuadro clave, y detalle del fallo en el estado del video. Es una causa potencial de acumulación y bloqueo corregida en código, no una solución física confirmada. Próximo paso: comparar multiview y OBS con el receptor de `build/stability-desktop`, un teléfono primero, registrar perfil y texto del fallo si persiste.

Sincronización manual implementada el 18-11: el multiview incorpora retardo de audio por teléfono de 0 a 1000 ms en pasos de 20 ms, restablecimiento y guía de palmada. Afecta a las salidas Matroska de audio y A/V. Ajuste temporal por ID mientras el receptor está abierto; se conserva al reconectar dentro del proceso, pero requiere recalibrar al cambiar transporte/perfil. Si el audio llega tarde, usar 0 ms y retrasar el video en OBS. No hay medición automática ni corrección de deriva. Falta medir y validar físicamente el resultado; los cortes siguen pendientes. Ver [guía de sincronización](../docs/SINCRONIZACION_AUDIO.md).

El usuario confirmó funcionamiento correcto de video, audio, multiview y zoom. Las capturas muestran Motorola moto e30 y Xiaomi 25080RABDG en el multiview y dos fuentes de captura con imágenes distintas en OBS. La confirmación es de funcionamiento básico; no especifica duración, latencia medida ni todos los casos de recuperación.

Último reporte: la fuente de audio independiente llega a OBS, con retraso inicial y cortes ocasionales. El usuario aplaza la investigación de estabilidad/fluidez y solicita continuar con controles de audio. Ya están implementados medidor de pico de salida, volumen 0-100 %, silencio y activación/detención remota autorizada por sesión; el usuario confirmó el 18-11 que los controles funcionan bien. La confirmación no detalla transportes, modelos, permisos denegados ni pruebas sostenidas. Ver [controles de audio](../docs/CONTROLES_AUDIO_MULTIVIEW.md).

| Área | Implementado y confirmado | Pendiente específico |
|---|---|---|
| Android | Kotlin/XML, Camera2, JPEG y H.264, selector por capacidades hasta 30 FPS, icono WilkensTech | Matriz de fabricantes, perfiles y sesiones prolongadas. |
| USB/Wi-Fi | QR/TLS, ADB y vínculos/perfiles persistentes; usuario confirmó video por ambos transportes | Registrar cambio de IP, reinicios, olvido y recuperación por escenario. |
| Multiview | Monitor 80 %, miniaturas 20 %, zoom continuo aceptado; exposición/enfoque/linterna implementados | Verificar los demás controles por fabricante y transporte. |
| Cámara virtual | Un dispositivo Windows por teléfono, 1080p30 NV12, registro elevado y recuperación de identidad | Medir rendimiento sostenido y recuperación de escenas. |
| Correcciones | Reinicio vertical USB y cierre prematuro 0xC00D3E85 resueltos y confirmados | Mantener regresiones. |
| Audio previo | Micrófono opcional PCM 48 kHz mono por TLS y salida Matroska A/V; recepción confirmada | Medir sincronía y deriva. |
| Audio independiente | Fuente de sólo audio y botón Copiar audio OBS; recepción confirmada por el usuario | Investigar retraso inicial y cortes ocasionales en fase posterior. |
| Controles de audio | Medidor, volumen, silencio de salida y micrófono remoto con confirmación Android | Funcionamiento básico confirmado por el usuario; matriz detallada pendiente. |
| Distribución | Registro del proveedor disponible desde la app | Instalador completo aún pendiente. |

La cámara virtual entrega video, no un micrófono del sistema. La salida de sólo audio es una Fuente multimedia independiente. Los enlaces A/V anteriores se conservan. La fuente independiente sólo requirió cambios Windows; los controles remotos nuevos requieren además actualizar Android. Volumen/silencio funcionan en Windows con el APK anterior, pero no habilitan su botón remoto.

## Orden de trabajo acordado

| Orden | Entrega | Estado |
|---|---|---|
| 1 | Audio independiente por teléfono, sin otra transmisión de imágenes a OBS | Recepción confirmada; retraso/cortes pendientes. |
| 2 | Controles de audio en multiview: activar/desactivar, silenciar, volumen y nivel | Funcionamiento básico confirmado; siguiente hito: sincronización medida. |
| 3 | Ajuste de sincronización medido por teléfono | Pendiente. |
| 4 | Instalador/desinstalador Windows | Pendiente. |

### Paso 1: cerrar validación de audio independiente

1. Cerrar el receptor y recompilar Windows. No hace falta actualizar Android en esta revisión.
2. Iniciar el video y activar Enviar micrófono en Android con su permiso explícito.
3. Usar la cámara virtual para la imagen y Copiar audio OBS para crear una Fuente multimedia de sólo audio. Desmarcar Archivo local, pegar el enlace, escribir matroska y usar búfer de red 0 MB.
4. Silenciar o retirar la fuente A/V anterior del mismo teléfono para evitar audio duplicado. Mantener la nueva fuente activa.
5. Verificar sonido, silencio al apagar el micrófono, reinicio y recuperación de la fuente. Repetir con dos teléfonos y comprobar independencia.
6. Registrar resultados en PRUEBAS. No marcar esta validación por haber aprobado sólo la lectura del contenedor en FFmpeg.

La fuente nueva no activa la captura en Android por conectarse desde OBS. El permiso y el inicio siguen en el teléfono. La captura actual del micrófono se inicia junto al envío de cámara; esta entrega separa la salida de audio para OBS, no crea un modo Android de micrófono sin cámara.

### Paso 2: controles de audio en el multiview

Funcionamiento básico confirmado por el usuario el 18-11. Próximo paso: medir sincronización con una palmada visible/audible; conservar como pendientes las pruebas detalladas de permisos, cortes y transportes. La autorización caduca al detener la cámara o salir de Android. Los ajustes de volumen/silencio se mantienen por teléfono durante esta ejecución; no se persisten ni arrancan capturas al reiniciar.

1. Mostrar estado real y medidor de nivel por teléfono; definir si representa entrada o salida.
2. Implementar silenciar y volumen con aplicación inmediata, límites y prevención de saturación. Distinguir silenciar salida de detener captura.
3. Extender el canal remoto para iniciar/detener micrófono con identificador, teléfono destinatario y confirmación. Una orden remota no concede RECORD_AUDIO ni evita la autorización Android.
4. Descartar órdenes pendientes al cambiar de teléfono y mantener independencia entre fuentes.
5. Probar nivel cero, señal intensa, permiso denegado, corte durante una orden y reconexión sin captura automática.
6. Decidir qué preferencias recordar y documentar si es necesario actualizar Android además del receptor.

### Paso 3: sincronización de audio y video

1. Grabar una palmada visible y audible; medir desfase en milisegundos por teléfono y transporte.
2. Definir signo y rango del ajuste: audio adelantado puede retardarse; audio tardío puede requerir retardo de video o reducir su latencia.
3. Aplicar un ajuste por teléfono en la app u OBS según la medición, sin prometer adelantar datos aún no recibidos.
4. Repetir al principio y final de una sesión; distinguir desfase fijo de deriva de relojes.
5. Comprobar cambios de escena, perfil, transporte y reconexión; definir cuándo restaurar o reiniciar el ajuste.
6. Registrar mediciones antes/después.

### Paso 4: instalador Windows

1. Definir paquete x64, requisitos Windows 11 y dependencia o inclusión de .NET.
2. Empaquetar receptor, proveedor y recursos sin exigir herramientas de desarrollo al usuario final.
3. Gestionar elevación, rutas protegidas y actualización de DLL en uso.
4. Implementar desinstalación de dispositivos, registro COM y versiones de DLL. Desactivar una cámara no equivale a desinstalar el producto.
5. Ofrecer conservar/eliminar vínculos y preferencias explícitamente.
6. Probar instalación limpia, actualización, desinstalación y recuperación en otro equipo; revisar licencias y firma antes de distribuir.

## Validación transversal

- Dos teléfonos durante 30 minutos; ampliar a 60 si la referencia es estable. Anotar perfiles, FPS, transporte, CPU/GPU, memoria y temperatura accesible.
- Desconectar/reconectar uno sin afectar al otro, incluyendo USB y Wi-Fi combinados.
- Reiniciar receptor/OBS conservando fuentes e identidad, sin duplicados. Verificar cambio de IP y respeto de Desconectar/Olvidar.
- Probar orientación, frontal/trasera, espejo, cambio de perfil y detención/reinicio sin cuadros antiguos.
- Medir latencia real y sincronía. La composición de OBS a 60 FPS no demuestra captura a 60 FPS.
- Comprobar permisos y liberación de cámara, micrófono, codecs y tareas al cerrar.

## Alcance aplazado

60 FPS continúa retirado. El intento Xiaomi falló por superficie 1202x2005 fuera de tamaños de alta velocidad 1280x720/1920x1080. No volver a priorizarlo sin nueva decisión. Captura de ventana está sustituida como salida final por cámaras virtuales; un complemento OBS propio es una alternativa futura. Micrófono virtual del sistema, tally, controles manuales avanzados, grabación de respaldo, 4K, Internet y otras plataformas quedan para después. Pagos, anuncios y monetización no son prioridades actuales.

## Código, comprobaciones y reglas para retomar

- Android permanece en la raíz; Windows en Escritorio/MovilCamara. No modificar otros proyectos ni resetear cambios existentes.
- AudioWireProtocol.cs valida PCM; ConnectionHub.cs recibe audio; ObsDirectServer.cs publica enlaces; LiveMatroska.cs genera los contenedores A/V y sólo audio.
- MultiViewWindow.xaml y su código asociado son el punto de entrada para los próximos controles de audio.
- VirtualCameraOutput.cs y Escritorio/VirtualCamera mantienen la salida virtual de video; no necesitan cambios para esta entrega de sólo audio.
- Ver [audio independiente](../docs/AUDIO_INDEPENDIENTE.md), [cámara virtual](../docs/CAMARA_VIRTUAL.md), [multiview](../docs/MULTIVIEW_Y_CONTROLES.md) y [conexiones](../docs/CONEXIONES.md).

Comandos pertinentes desde la raíz:

    dotnet build Escritorio/MovilCamara/MovilCamara.csproj
    dotnet run --project tools/ConnectionChecks/ConnectionChecks.csproj -- --offline
    dotnet run --project tools/ObsOutputChecks/ObsOutputChecks.csproj
    dotnet run --project tools/ConnectionChecks/ConnectionChecks.csproj -- --obs-audio-output

La prueba HTTP requiere el puerto local 47780 libre. Las pruebas con sockets pueden estar bloqueadas por el entorno; no contar un bloqueo como aprobado. Si cambia Android, generar el APK completo; las comprobaciones Kotlin/aapt2 aisladas no sustituyen Gradle. Cambios sólo de documentación no requieren recompilar.

Al cerrar cada entrega, actualizar los tres archivos del plan, distinguir pruebas locales de confirmaciones del usuario y dejar el siguiente paso concreto. No publicar, crear commits ni hacer push sin instrucción.
