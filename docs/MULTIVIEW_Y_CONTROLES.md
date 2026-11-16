# Multiview y controles remotos — 16-11-2026

## Revisión vigente: vista 80/20 y zoom continuo

La vista principal ocupa aproximadamente el 80 % del área de video; el 20 % restante se reserva a una franja horizontal de miniaturas compactas. No se estiran las tarjetas para llenar el alto. Si no caben todas, se desplaza la franja horizontalmente. La selección, el estado y las fuentes OBS permanecen independientes.

El zoom ahora se aplica mientras se mueve el deslizador, sin botón Aplicar. El escritorio agrupa los cambios cada 80 ms, con una única orden en vuelo y sólo el último valor pendiente. Al llegar la confirmación se puede enviar el valor más reciente; no se reproduce una cola de posiciones antiguas. Cambiar de teléfono/cámara descarta el zoom pendiente de la selección anterior y valida el identificador de cámara antes de enviar. La sincronización desde Android no genera nuevas órdenes ni mueve el deslizador mientras se arrastra.

Esta revisión sólo requiere actualizar el receptor Windows si Android ya utiliza la versión de controles remotos. La fluidez efectiva depende del teléfono y de la red, y sigue pendiente probarla físicamente.

Exposición mantiene su botón de aplicación.

## Uso

1. Actualizar Android y Windows con esta revisión. Conectar los teléfonos por USB o Wi-Fi e iniciar el envío en Android.
2. En la barra lateral del receptor pulsar **Abrir multiview**. Se abre un monitor independiente, reutilizado si se vuelve a pulsar el botón.
3. Seleccionar una miniatura para mostrarla en el monitor grande. La franja inferior conserva las miniaturas compactas y permite desplazamiento horizontal si no caben todas.
4. El panel derecho muestra los controles que anuncia la cámara activa. Al iniciar la captura puede tardar unos segundos en recibirlos.
5. Mover el deslizador de zoom: se envía durante el movimiento, sin botón de aplicación. Para exposición, ajustar su deslizador y pulsar **Aplicar exposición**. La exposición está expresada en pasos de compensación de Camera2, no en EV calculados. **Restablecer zoom** vuelve a 1× dentro del rango permitido.
6. **Reenfocar** solicita autofocus; **Encender/Apagar linterna** controla el flash continuo. Se deshabilitan si el hardware no los anuncia.
7. La barra inferior informa de la confirmación de Android o del error/ausencia de respuesta. Si se selecciona otra cámara mientras hay un ajuste pendiente, la orden mantiene el destinatario original.

Las cámaras que ya se emparejaron en esta ejecución se conservan visibles al desconectarse. Se retira su imagen y se informa Sin señal; vuelven a utilizar su vista al reconectar. Los teléfonos sólo detectados y nunca emparejados no aparecen en el multiview.

## Alcance

Es un monitor y panel de control. Seleccionar una miniatura no hace un corte de programa en OBS. Las fuentes directas OBS siguen siendo independientes. No se muestran medidores de audio, REC, ON AIR o tally porque todavía no existe información real para esos indicadores.

No hay pan/tilt mecánico, control ISO/obturación/balance de blancos, cambio remoto de cámara o inicio remoto de captura en esta revisión. Zoom/exposición se aplican a la sesión actual y vuelven al valor inicial al reiniciar la captura; su persistencia específica puede añadirse después. El perfil de resolución/FPS/formato y el vínculo siguen guardándose como antes.

## Implementación

- WPF: `MultiViewWindow` utiliza los mismos modelos y bitmaps del panel, sin decodificar nuevamente para cada miniatura. Mantiene selección, imagen grande, estado/FPS y franja horizontal de miniaturas.
- Transporte: Android consulta comandos mediante una conexión TLS autenticada con el mismo secreto y huella del vínculo existente, tanto por USB como por Wi-Fi. Es un canal separado del video y de la salida OBS.
- El PC limita la cola a ocho órdenes por teléfono, espera confirmación hasta doce segundos y no marca el ajuste como aplicado al enviarlo. La confirmación se correlaciona con UUID del teléfono y un identificador aleatorio de orden.
- Cada orden incluye el identificador de cámara; Android rechaza órdenes si la captura terminó o cambió de cámara/generación. Se validan operación, valor finito, rango de zoom, exposición y disponibilidad de enfoque/linterna. Las órdenes no se guardan ni se reproducen tras reiniciar el receptor.
- Android modifica `CaptureRequest` en el hilo de cámara y publica capacidades/estado en cada consulta. En Android 11+ usa `CONTROL_ZOOM_RATIO` cuando existe; en otros dispositivos usa recorte digital centrado. Ante un error restaura los valores del builder para que un ajuste posterior no arrastre una modificación rechazada.
- La confirmación indica que Android aceptó la solicitud Camera2, no una medición óptica independiente ni confirmación de transmisión/programa en OBS.
- Equipos con versiones anteriores pueden seguir con video pero no anunciarán controles. El panel permanece deshabilitado hasta recibir capacidades recientes.

Referencias Camera2: https://developer.android.com/reference/android/hardware/camera2/CaptureRequest

## Validación

- WPF compilado; 31 comprobaciones de protocolo/persistencia/control remoto sin sockets aprobadas.
- Render de interfaz con cinco cámaras sintéticas en 1400×900 y 1000×720. Se comprueba exclusión de una sexta cámara sin emparejar, selección conservada, miniaturas visibles y controles deshabilitados cuando no hay capacidades.
- Todos los fuentes Kotlin compilan contra SDK Android 36 y las dependencias/bindings ya generados; no sustituye la compilación Gradle completa del APK.
- Pendiente físico: APK actualizado, controles Camera2 reales, USB/Wi-Fi, dos teléfonos simultáneos, desconexión durante una orden y comprobación de que cada ajuste afecta sólo a la cámara elegida. No se ha validado esta revisión en teléfonos desde el entorno del asistente.

Comandos:

```powershell
dotnet build Escritorio/MovilCamara/MovilCamara.csproj
dotnet run --project tools/ConnectionChecks/ConnectionChecks.csproj -- --offline
# Después de compilar Windows, render de prueba sin abrir un receptor de red:
dotnet run --project tools/MultiviewChecks/MultiviewChecks.csproj
```

El render de prueba usa imágenes sintéticas, no teléfonos ni pruebas de red. Puede indicarse otro DLL compilado mediante `-p:WTAppAssembly=<ruta>`.
