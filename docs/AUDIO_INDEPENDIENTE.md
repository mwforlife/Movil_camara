# Audio independiente por teléfono

Actualizado: 17 de noviembre de 2026.

## Qué cambia

El receptor incluye **Copiar audio OBS** por teléfono. El enlace entrega Matroska con una única pista PCM de 16 bits, mono, 48 kHz; no incluye video. Los lectores de sólo audio no solicitan codificación JPEG. La imagen se recibe mediante la cámara virtual existente.

No se crea un micrófono virtual de Windows. Tampoco se añade un modo Android de micrófono sin cámara: se mantiene el envío de micrófono asociado al inicio de captura actual. Esta revisión no cambia Android ni el proveedor nativo de cámara virtual.

## Configurar OBS

1. Cerrar el receptor y recompilar Windows.
2. En Android activar **Enviar micrófono del teléfono**, conceder el permiso e iniciar el envío como siempre.
3. En Windows pulsar **Copiar audio OBS** en la tarjeta de ese teléfono.
4. En OBS añadir una **Fuente multimedia** nueva. Desmarcar **Archivo local**, pegar el enlace en **Entrada**, escribir **matroska** en **Formato de entrada** y establecer **Buffering de la red en 0 MB**.
5. Mantener esa fuente activa. Debe aparecer en el mezclador de OBS y no mostrar imagen; usar el dispositivo de cámara virtual para el video.
6. Silenciar o retirar la antigua fuente A/V del mismo teléfono para evitar sonido duplicado. Los enlaces anteriores siguen funcionando, pero no hace falta conservar esa segunda ruta de video.
7. Repetir por teléfono, con nombres reconocibles en el mezclador. Mantener el receptor abierto; puede minimizarse.

Al apagar el micrófono o dejar de recibir paquetes recientes, la salida emite silencio. Abrir el enlace desde OBS no inicia el micrófono ni concede permisos en Android.

Si OBS deja de reproducir la fuente al ocultarla o cambiar de escena, revisar sus opciones de cierre/reinicio al quedar inactiva y comprobar la recuperación al activarla. No guardar ni compartir capturas con el enlace completo: contiene su credencial local.

## Implementación

- Endpoint loopback `/audio/<id>` con formato Matroska explícito y credencial HMAC específica del teléfono y de la ruta de audio. Las credenciales de video no autorizan el endpoint de audio ni a la inversa.
- Enlace estable con la clave persistida del receptor. No cambia la autorización TLS del teléfono.
- Contadores separados de lectores de audio/video: una fuente de sólo audio recibe PCM sin mantener codificación de imágenes para ella. Las fuentes A/V o MJPEG abiertas por separado pueden seguir codificando su propio video.
- Un lector A/V y uno de sólo audio del mismo teléfono pueden convivir, con cursores independientes. La cola compartida mantiene hasta cinco paquetes y omite paquetes con más de 100 ms; no se reproducen colas antiguas después de un corte.
- Cada conexión nueva empieza desde la secuencia de audio actual y emite silencio hasta que llega un paquete nuevo. Un cliente bloqueado se desconecta en lugar de acumular segundos de audio.
- El contenedor usa timestamps locales crecientes y conserva muestras PCM. Esto no calibra automáticamente el desfase físico con el video virtual ni corrige deriva entre relojes.

## Evidencia de esta revisión

- Windows compila con 0 errores y 0 advertencias.
- ConnectionChecks `--offline`: 43 comprobaciones aprobadas, cinco nuevas de autorización/rutas de audio y estabilidad de enlaces.
- ObsOutputChecks: cuatro pruebas de imagen existentes y dos lecturas con FFmpeg aprobadas. La lectura nueva confirma una pista de audio, ningún video, 50 paquetes y muestras de tono/silencio intactas. La salida A/V anterior sigue abriéndose correctamente.
- La prueba HTTP `--obs-audio-output` está implementada para dos lectores, silencio, independencia, ausencia de codificación de imágenes y reconexión. Su ejecución en este entorno falló antes de conectar por Winsock 10013. **No se considera aprobada.**
- Falta la prueba física de la nueva fuente en OBS con uno y dos teléfonos. El audio A/V previo sí fue confirmado por el usuario, pero no sustituye esta comprobación.

Comandos desde la raíz:

```powershell
dotnet build Escritorio/MovilCamara/MovilCamara.csproj
dotnet run --project tools/ConnectionChecks/ConnectionChecks.csproj -- --offline
dotnet run --project tools/ObsOutputChecks/ObsOutputChecks.csproj
dotnet run --project tools/ConnectionChecks/ConnectionChecks.csproj -- --obs-audio-output
```

Cerrar el receptor antes del último comando para dejar libre el puerto 47780. Las imágenes y muestras de las pruebas son sintéticas; no son grabaciones de los teléfonos.

## Qué sigue

Actualización posterior: los controles de audio ya están implementados; necesitan probarse en los teléfonos. Para el botón remoto hay que actualizar Android y autorizar micrófono al iniciar la sesión. El usuario confirmó que la fuente de audio llega a OBS con retraso inicial y cortes ocasionales; estabilidad/fluidez se revisarán después. Ver [controles de audio del multiview](CONTROLES_AUDIO_MULTIVIEW.md).

Confirmar esta fuente en OBS y luego implementar estado/medidor, silencio, volumen y activación remota autorizada en el multiview. Después medir y ajustar sincronización; finalmente preparar el instalador Windows. Ver [plan vigente](../plan/PLAN.md).
