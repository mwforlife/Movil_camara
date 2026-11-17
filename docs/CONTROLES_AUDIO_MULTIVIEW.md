# Audio en el multiview - 17-11-2026

## Estado y alcance

El usuario confirmó que la fuente independiente recibe audio en OBS, pero reportó retraso inicial y cortes ocasionales. Solicitó aplazar estabilidad/fluidez y avanzar con controles. Esta entrega añade controles y diagnóstico visual; no resuelve ni da por resueltos esos cortes.

En el panel del teléfono seleccionado:

- **Medidor:** pico de salida en dBFS, después de volumen/silencio del receptor y antes de los ajustes de OBS. Actualiza cada 150 ms y vuelve a cero sin paquetes recientes. No reproduce audio por los altavoces del PC ni mide el volumen físico del micrófono.
- **Volumen de salida:** 0-100 %, sólo atenuación; no amplifica sobre la entrada ni modifica los bytes PCM originales. Se aplica a los enlaces sólo audio y A/V de ese teléfono.
- **Silenciar salida a OBS:** emite ceros sin detener AudioRecord en Android. El estado de captura permanece visible por separado.
- **Activar/Detener micrófono:** orden remota autenticada, ligada al teléfono/cámara y confirmada por Android. Iniciar sólo se confirma cuando AudioRecord informa RECORDSTATE_RECORDING; la llegada de paquetes al PC sigue siendo un estado separado.

Los ajustes se conservan por teléfono durante la ejecución de Windows, incluyendo cambio de selección y cortes de conexión. No se guardan en disco. Reiniciar el receptor restaura 100 % y sin silencio, pero no inicia la cámara ni el micrófono del teléfono.

## Actualización y uso

1. Cerrar el receptor. Compilar Windows y generar/instalar el APK Android actualizado.
2. En Android marcar **Enviar micrófono del teléfono**, autorizar el permiso e iniciar la cámara. Esto habilita el control remoto durante esa sesión de captura.
3. Abrir el multiview y seleccionar el teléfono. Ajustar volumen y silencio, comprobando la fuente correcta en OBS.
4. Probar **Detener micrófono**: detiene captura y envío, a diferencia del silencio local. Después **Activar micrófono** puede volver a abrirlo durante esa misma sesión autorizada.
5. Al detener la cámara, cambiarla o salir de Android caduca esa autorización. Para una sesión nueva, volver a marcar Enviar micrófono e iniciar explícitamente.
6. Repetir con otro teléfono y comprobar que no hereda volumen ni silencio del anterior.

Si el APK es antiguo, el volumen y silencio locales siguen disponibles, pero el botón de micrófono se deshabilita. Si no se autorizó el micrófono al iniciar la sesión, Windows indica hacerlo en Android; una orden remota no abre el diálogo de permisos ni lo concede automáticamente.

La aplicación Android debe estar activa y con cámara iniciada. No se introduce captura de micrófono independiente de la cámara ni un dispositivo de micrófono virtual del sistema.

## Implementación

- AudioMixer observa entrada PCM autenticada sin requerir lectores de OBS, guarda pico/fecha y procesa la salida con configuración por ID. No interpreta falta de paquetes como prueba de que el micrófono dejó de capturar.
- ObsDirectServer aplica volumen/silencio al emitir cada paquete en ambas rutas Matroska, de modo que no conserva en su cola la ganancia del momento de recepción. No puede retirar audio ya almacenado en OBS o en el socket.
- MultiViewWindow muestra estado/nivel de selección sin publicar eventos UI por cada paquete. El panel conserva la distribución 80/20 y permite desplazamiento vertical de controles.
- El estado de cámara añade capacidades opcionales de audio. Los receptores/APK antiguos no habilitan control remoto por defecto. La orden microphone usa la cola autenticada con destinatario, cámara, caducidad y confirmación existentes.
- Android valida ciclo de vida, cámara actual, permiso y autorización de sesión. El inicio asíncrono se confirma al abrir AudioRecord; cancelaciones o cambios de sesión/orden no cierran una captura de una orden posterior.

## Verificación y próximos pasos

Windows se compila en una carpeta separada porque el receptor del usuario está abierto. Los 16 fuentes Kotlin compilan por separado con SDK 36 y dependencias/bindings existentes. La generación Gradle del APK sigue pendiente: el wrapper del entorno temporal no pudo descargar la distribución por DNS. Esa comprobación aislada no reemplaza el APK completo.

ConnectionChecks --offline valida el mezclador y las confirmaciones; MultiviewChecks prueba selección/independencia y controles remotos deshabilitados sin capacidad, y genera vistas con dos/cinco cámaras en tamaños normal y mínimo. Falta comprobar controles reales y permisos con los teléfonos por USB/Wi-Fi.

Después de esa confirmación, seguir con sincronización medida y las tareas del [plan](../plan/PLAN.md). Estabilidad de transmisión y fluidez siguen pendientes conforme a lo solicitado.
