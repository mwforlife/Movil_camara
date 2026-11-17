# Cámaras virtuales WT MultiCam

## Estado

Corrección posterior del error 0xC00D3E85: liberar IMFActivate ya no ejecuta Shutdown sobre una fuente que el consumidor todavía conserva. Se añadió una prueba que reproducía el código exacto antes de la corrección y ahora comprueba que el mismo origen sigue entregando muestras. Los errores de activación también identifican la etapa nativa y conservan el HRESULT. Tras recompilar, Activar cámara virtual registra la DLL corregida cuando detecta que cambió; volver a autorizar ese registro. La confirmación de arranque real en OBS sigue pendiente.

Proveedor COM Media Foundation x64 implementado, con una identidad estable por usuario Windows y teléfono. El receptor incluye activación, desactivación y recuperación al abrirlo. La compilación y las pruebas del proveedor no equivalen a enumeración real en OBS: esa validación sigue pendiente.

Esta revisión sólo modifica Windows. El audio y los controles de Android ya confirmados por el usuario no necesitan otro APK.

## Probar en OBS

1. Cerrar el receptor y recompilar `Escritorio/MovilCamara/MovilCamara.sln`. La compilación llama a `Escritorio/VirtualCamera/Build.ps1` y copia la DLL y el instalador a `Native` junto al ejecutable.
2. Abrir el receptor, conectar el teléfono e iniciar su cámara como siempre.
3. Pulsar **Activar cámara virtual** en la tarjeta del teléfono. Windows pedirá autorización de administrador para registrar el proveedor la primera vez por teléfono y cuando cambie su DLL. Si se cancela, el resto del receptor sigue funcionando.
4. En OBS añadir **Dispositivo de captura de video** y elegir **WT MultiCam — nombre del teléfono (Windows Virtual Camera)**. Windows agrega el sufijo de cámara virtual; comprobar el nombre real enumerado.
5. Usar resolución personalizada **1920 × 1080**, **30 FPS**, formato **NV12**. Si OBS ofrece Buffering para el dispositivo, comparar **Desactivar** con el valor automático y comprobar estabilidad.
6. Repetir para cada teléfono; cada uno tiene su propio dispositivo. No se necesita una ventana de video ni un enlace de red para la imagen virtual. Mantener el receptor abierto; puede minimizarse.
7. Comparar retraso con la Fuente multimedia anterior usando un movimiento o reloj visible común. No dar por eliminada la latencia sin medirla.

El formato virtual inicial es fijo: 1080p30. La captura Android conserva su perfil hasta 30 FPS; un perfil de menos resolución se adapta al lienzo virtual y no gana detalle real. La imagen vertical mantiene su proporción con franjas negras. La orientación y el espejo se toman del mismo cuadro que se presenta en el receptor.

## Micrófono

**Actualización 17-11-2026:** ya está implementado **Copiar audio OBS**, una fuente Matroska de sólo sonido para usar junto al dispositivo virtual. Consultar [audio independiente](AUDIO_INDEPENDIENTE.md). Falta confirmar esta fuente nueva en OBS. El procedimiento de conservar la fuente A/V descrito debajo sigue siendo compatible, pero al cambiar al enlace nuevo conviene silenciar o retirar la ruta A/V anterior para evitar duplicados.

La cámara virtual entrega video, no crea un micrófono Windows. Para conservar el audio ya probado, mantener activa la Fuente multimedia Matroska del teléfono **debajo** de la cámara virtual en el orden de fuentes de OBS, de modo que su imagen quede cubierta. No desactivar esa fuente con el ojo si ello detiene su audio. Elegir sólo una ruta de video visible y comprobar sincronía; el desfase de audio puede necesitar ajuste en OBS tras medirlo.

## Persistencia y desconexiones

- Se guardan teléfono, CLSID y nombre inicial en `%LOCALAPPDATA%/WilkensTech/WTMultiCam/virtual-cameras.json`, mediante reemplazo del archivo. El nombre usado para registrar la cámara se conserva para reabrir la misma identidad.
- Al abrir el receptor se recuperan sólo las cámaras que el usuario activó. La recuperación nunca solicita elevación automáticamente: si el proveedor necesita registro/actualización se informa en el panel.
- Cerrar el receptor deja el dispositivo instalado para conservar la fuente en OBS, pero limpia su imagen. Un cuadro con más de dos segundos sin actualizar se entrega negro. Desconectar el teléfono no afecta a las otras cámaras.
- **Desactivar cámara virtual** retira ese dispositivo de Windows y de la lista de recuperación. Conserva el registro COM y la DLL para una futura activación sin reinstalar la misma versión. La limpieza completa de versiones de DLL/registro COM queda para el instalador de distribución.
- Si falla la recuperación, usar Desactivar y volver a Activar para actualizar el proveedor. No debe iniciarse la cámara ni el micrófono de Android automáticamente.

## Arquitectura

- `Escritorio/VirtualCamera/Provider.cpp`: fábrica COM, objeto `IMFActivate` con almacén de atributos, `IMFMediaSourceEx`, `IMFMediaStream2`, eventos y entrega de muestras NV12. El origen implementa también `IMFGetService`, `IKsControl` e `IMFSampleAllocatorControl` con memoria propia. No declara soporte de controles KS que no implemente.
- `FrameBridge.h/.cpp`: búfer de último cuadro, conversión BGR32 a NV12 de rango limitado/BT.601 y ajuste proporcional. Compartición mediante archivo mapeado, con bloqueo cooperativo de rango para evitar cuadros parcialmente escritos. Se conserva sólo un cuadro; no hay cola de video acumulada.
- El archivo de intercambio contiene sólo el último cuadro y la cabecera (3.110.440 bytes por cámara), no una grabación temporal completa. Se limpia al detener/cerrar el emisor; tras un cierre abrupto puede conservar el último cuadro en el archivo, aunque el origen lo rechaza por antigüedad. Su limpieza definitiva queda con la retirada del componente.
- El archivo se ubica en `%PROGRAMDATA%/WilkensTech/WTMultiCam/VirtualCamera/Frames/<SID>/<CLSID>.bin`. ACL del directorio: usuario propietario, administradores y SYSTEM con control; Local Service con lectura para Camera Frame Server. El origen obtiene la ruta de una clave COM escrita por el instalador. Falta comprobar estos permisos desde el servicio real, no sólo con otro proceso del mismo usuario.
- `Install-Provider.ps1`: copia versionada por SHA-256 a Program Files y registro COM x64 en HKLM por teléfono. El servicio no carga la DLL desde la carpeta de compilación modificable por el usuario. Requiere elevación; no se ejecuta durante compilación ni pruebas.
- `VirtualCameraOutput.cs`: administra el dispositivo con `MFCreateVirtualCamera`, acceso del usuario actual y duración de sistema. Publicación fuera del hilo UI, con una única imagen pendiente por teléfono. La imagen procede del cuadro ya orientado/congelado que muestra el receptor. Por ello sigue compartiendo su etapa actual de decodificación/presentación; aún no es una ruta D3D sin copias.
- La integración elimina HTTP/MJPEG para el video virtual. No demuestra por sí misma una reducción de latencia de extremo a extremo.

## Comprobaciones

```powershell
.\Escritorio\VirtualCamera\Build.ps1 -RunChecks
dotnet build Escritorio/MovilCamara/MovilCamara.csproj
dotnet run --project tools/ConnectionChecks/ConnectionChecks.csproj -- --offline
```

Las pruebas nativas usan imágenes sintéticas y archivos temporales; no registran COM ni crean dispositivos. Comprueban activación y atributos, muestras y tokens, proporción, independencia entre fuentes, comunicación entre procesos, detención/reinicio, recuperación del publicador, caducidad de cuadros y liberación COM. La enumeración en OBS, el acceso desde Frame Server, la continuidad en escenas, el rendimiento y dos teléfonos físicos siguen pendientes.

Referencias oficiales:

- https://learn.microsoft.com/en-us/windows/win32/api/mfvirtualcamera/nf-mfvirtualcamera-mfcreatevirtualcamera
- https://learn.microsoft.com/en-us/windows-hardware/drivers/stream/frame-server-custom-media-source
- https://github.com/microsoft/Windows-Camera/tree/master/Samples/VirtualCamera
