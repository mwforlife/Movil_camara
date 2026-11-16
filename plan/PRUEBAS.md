# Registro de pruebas — WT MultiCam

No hay aún una validación física documentada de la última versión H.264/selector. Los reportes anteriores del usuario confirman QR, llegada de video a Windows y vista previa USB correcta en versiones previas.

Copiar esta plantilla por cada combinación. No guardar tokens, secretos de sesión ni un QR activo.

## Plantilla de sesión

- Fecha/hora:
- Versión o commit del receptor:
- Versión o commit del APK:
- Modelo del teléfono:
- Versión Android:
- Cámara / identificador:
- Perfil seleccionado (resolución, FPS, H.264/JPEG):
- Encoder reportado, si se conoce:
- PC / CPU / GPU / RAM:
- Windows:
- Conexión: USB ADB / anclaje USB / Wi-Fi:
- Router/banda Wi-Fi y PC por Ethernet/Wi-Fi:
- Tiempo de prueba:
- Otros teléfonos conectados:

| Medición | Inicio | Durante la prueba | Final |
|---|---|---|---|
| FPS enviados en Android | | | |
| FPS presentados en Windows | | | |
| Bitrate enviado | | | |
| CPU / GPU del PC | | | |
| Memoria del receptor | | | |
| Batería / temperatura accesible | | | |
| Latencia medida y método | | | |

### Comprobaciones

- [ ] Imagen correcta en el teléfono.
- [ ] Imagen correcta en Windows.
- [ ] Vertical/horizontal y ambos sentidos de giro.
- [ ] Frontal/trasera y espejo.
- [ ] Sin cuadros congelados ni crecimiento continuo de demora.
- [ ] Detener/reiniciar y cambiar perfil.
- [ ] Reconexión tras corte.
- [ ] Ventana de fuente y Modo OBS.
- [ ] OBS recibe esa ventana.
- [ ] Otro dispositivo continúa si se desconecta éste.

### Resultado

- Aprobado / fallido / parcial:
- Error exacto y en qué pantalla aparece:
- Pasos para reproducir:
- Comportamiento esperado:
- Captura o log sin credenciales:
- Próxima acción concreta:

## Historial documentado

| Fecha | Alcance | Resultado |
|---|---|---|
| 16-11-2026 | Compilación WPF y pruebas de protocolo sin sockets | Correctas durante implementación. |
| 16-11-2026 | Clases Kotlin y recursos compilados por separado | Correctos; no sustituyen el APK completo. |
| 16-11-2026 | Encode/decode local Windows en formato 1920×1080/60 | Recupera RGB32 correcto; no mide FPS sostenidos del teléfono ni de la red. |
| Pendiente | APK actual + H.264 en teléfono real | Completar una sesión usando la plantilla. |
