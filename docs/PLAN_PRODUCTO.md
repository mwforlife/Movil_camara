# Plan de producto — WT MultiCam

Propuesta inicial para WilkensTech Corp. Nombre aprobado posteriormente: WT MultiCam. El estado actual y el orden para continuar están en [plan/PLAN.md](../plan/PLAN.md). Este documento conserva el contexto de la propuesta original.

## 1. Objetivo

Convertir teléfonos Android en cámaras para streaming mediante una aplicación móvil y un receptor de escritorio. Cada teléfono conserva su identidad y su fuente de video: conectar cuatro teléfonos debe permitir crear cuatro fuentes independientes en OBS, no solamente una cuadrícula combinada.

Primer mercado: creadores, podcasts, iglesias, colegios y producción de eventos con equipamiento accesible. Propuesta de valor: preparar, controlar y supervisar una producción multicámara desde un solo lugar.

Plataformas iniciales propuestas: Android 10 o superior y Windows 11 x64. Son decisiones de alcance pendientes de validar con dispositivos reales; no equivalen a los mínimos de las APIs. Windows 10, macOS, Linux e iOS quedan para evaluación posterior.

## 2. Referencia y diferenciación

La referencia aportada muestra Full HD sin marca de agua, 4K mediante complemento OBS, baja latencia, controles remotos, restauración de parámetros y optimización de consumo. Estas prestaciones orientan el producto, pero no son promesas de rendimiento ni una especificación que debamos copiar literalmente.

OBS Studio es gratuito y de código abierto; las funciones Pro de la referencia corresponden a un producto complementario. DroidCam ya admite varios teléfonos como fuentes independientes en OBS. Por tanto, multicámara es un requisito esencial, no una diferenciación suficiente.

Diferenciación propuesta:

- Emparejamiento sencillo por QR y nombres persistentes por cámara.
- Panel único con señal, batería, temperatura cuando sea accesible y calidad por dispositivo.
- Control remoto y perfiles para repetir montajes de eventos.
- Recuperación de cortes sin perder la asignación de la fuente.
- Diagnóstico que explique problemas de red, rendimiento y permisos en español.
- Indicadores de cámara al aire y vista previa mediante integración con OBS.
- Pruebas sostenidas y degradación de calidad controlada antes de una desconexión.

## 3. Experiencia principal

1. Instalar el receptor en Windows y la app en cada teléfono.
2. Abrir un proyecto de producción en el PC.
3. Escanear el QR del PC desde el teléfono y autorizar el emparejamiento.
4. Asignar nombre: Principal, Lateral, Público, Detalle.
5. Elegir un perfil compatible con el teléfono y comenzar a transmitir.
6. Agregar cada cámara como fuente independiente en OBS.
7. Ajustar encuadre y parámetros desde el PC, guardar la configuración y supervisar la sesión.

La conexión local debe funcionar sin cuenta y sin Internet. La app no debe activar la cámara de un teléfono sin autorización del usuario.

## 4. Funciones por etapa

| Área | Primera versión utilizable | Versión avanzada |
|---|---|---|
| Multicámara | Dos teléfonos simultáneos; arquitectura por sesiones independientes | Cuatro teléfonos certificados; ampliar según mediciones |
| Video | 720p30 y 1080p30 cuando el hardware lo permita | 1080p60 y 4K condicionales |
| Conexión | Red local, QR y dirección manual | Conexión por USB validada, acceso remoto opcional |
| Android | Cámara frontal/trasera, vista previa y estado de conexión | Selección de lentes y controles manuales según capacidades |
| Controles | Zoom, linterna, orientación, espejo y enfoque/exposición compatibles | Balance de blancos, ISO y obturación cuando estén disponibles |
| Audio | Micrófono opcional por teléfono; selección explícita de la fuente activa | Desfase ajustable, monitoreo e integración con audio del PC |
| Escritorio | Cuadrícula, nombres, reconexión y estadísticas | Perfiles de evento, grupos, grabación por cámara |
| OBS | Complemento con una instancia de fuente por teléfono | Tally de programa/vista previa y controles desde OBS |
| Otros programas | Investigación y prototipo de cámara virtual | Cámaras virtuales independientes y salida de programa |
| Fiabilidad | Estado claro al perder señal, reintentos y registro de errores | Grabación local de respaldo y recuperación de archivos |
| Consumo | Vista previa atenuada y perfiles de calidad | Ajuste adaptativo por red y comportamiento térmico |

Las funciones esenciales de estabilidad, emparejamiento seguro y recuperación no deben depender de una licencia avanzada.

## 5. Aplicación Android

- Pantallas: conexión, cámara, ajustes y diagnóstico.
- Mostrar resolución/FPS efectivos, bitrate, batería y estado de transmisión.
- Transmitir con indicador visible y acción para detener.
- Comprobar permisos de cámara y micrófono de forma contextual.
- Servicio en primer plano para una sesión iniciada por el usuario, respetando las restricciones de Android.
- Consultar capacidades antes de ofrecer parámetros o combinaciones de resolución/FPS.
- Mantener un identificador propio persistente, sin depender de dirección IP o MAC.
- Recordar parámetros por cámara y aplicar solamente valores válidos al restaurarlos.
- Gestionar llamadas, pérdida de cámara, cambio de red, suspensión e interrupciones del fabricante.
- Modo de pantalla atenuada; funcionamiento con pantalla bloqueada sujeto a pruebas por fabricante, sin garantía universal.

“Cualquier Android” es la intención comercial, pero el soporte real debe definirse con una matriz de versiones, fabricantes, cámaras y codecs. No todos los teléfonos admiten 4K, 60 FPS, lentes adicionales o control manual.

## 6. Aplicación Windows

- Panel de dispositivos conectados, conocidos y pendientes de autorización.
- Cuadrícula de previsualización con controles por dispositivo.
- Mostrar estadísticas reales y distinguir FPS de captura, recepción y salida.
- Proyectos locales con nombres, perfiles, preferencias y asignaciones persistentes.
- Gestión independiente de sesiones: un teléfono que falla no debe detener a los demás.
- Desactivar miniaturas o reducir su frecuencia para ahorrar GPU.
- Asistente para red local, firewall y permisos sin abrir puertos hacia Internet automáticamente.
- Revocar dispositivos y regenerar credenciales de conexión.
- Exportar diagnóstico sin tokens, audio, video ni datos sensibles por defecto.
- Instalador, desinstalador y actualizaciones firmadas para distribución comercial.

## 7. Arquitectura propuesta

Android captura → codificación → WebRTC → receptor Windows → puente de frames → fuente OBS / cámara virtual.

Separar cuatro componentes:

1. Control y emparejamiento: descubrimiento, autenticación, configuración y reconexión.
2. Transporte multimedia: audio/video y estadísticas por sesión.
3. Motor del receptor: decodificación, tiempos, buffers acotados y distribución de frames.
4. Adaptadores de salida: OBS y cámaras virtuales, sin acoplar la captura móvil a un programa concreto.

Stack recomendado como punto de partida:

| Componente | Propuesta | Motivo y validación pendiente |
|---|---|---|
| Android | Kotlin, Jetpack Compose, Camera2 mediante integración con libwebrtc | Control de capacidades y camino de captura sin copias innecesarias; probar antes de cerrar arquitectura |
| Captura | Camera2; evaluar CameraX si simplifica el prototipo | Evitar dos capturas simultáneas compitiendo por la misma cámara |
| Transporte | libwebrtc; H.264 preferente si se negocia y acelera, VP8 como alternativa | Baja latencia y control de congestión; medir interoperabilidad y revisar distribución/licencias |
| Interfaz Windows | C# y WPF sobre una versión LTS de .NET vigente al implementar | Desarrollo nativo y controles de escritorio |
| Motor multimedia | C++ con libwebrtc y puente a C# | Compartir frames con salidas nativas; complejidad de integración a validar |
| OBS | Complemento C/C++ con libobs | Fuente independiente por dispositivo, con menor dependencia de capturas de ventanas |
| Persistencia | JSON para configuración; SQLite si crece el historial | No hace falta un servidor MySQL para transmitir localmente |

WAMP/PHP pueden servir para una futura web comercial o licencias, pero no serían el motor de video. La ubicación del repositorio en www no obliga a crear una aplicación web.

WebRTC necesita señalización propia; el receptor alojará ese servicio local. Usar canales autenticados para control y medios cifrados. El QR debe contener credenciales de emparejamiento breves y de un solo uso; verificar la identidad del PC mediante un mecanismo de confianza anclado al QR. No basta con conocer una IP.

El núcleo recibe una transmisión por teléfono y distribuye frames localmente. Evitar que cada consumidor provoque otra transmisión o decodificación sin necesidad. Diseñar IPC y memoria compartida con permisos y manejo de caída de procesos.

## 8. Integración con streaming

### OBS

Crear una fuente denominada provisionalmente “WilkensTech Cámara”. Cada instancia selecciona un dispositivo persistente. Varias escenas pueden reutilizar la misma sesión. El PC mantiene la gestión central y el complemento consume su salida.

Un prototipo mediante fuente de navegador puede ayudar a validar el transporte, pero no demuestra compatibilidad universal ni sustituye la integración final.

### Cámara virtual

Recibir video en Windows no convierte automáticamente el teléfono en una webcam del sistema. Se necesita un adaptador adicional. Evaluar Media Foundation en Windows 11 con un prototipo que pruebe OBS, aplicaciones de videollamada y el programa de streaming objetivo.

Decidir después de la prueba si se ofrecen varias cámaras virtuales persistentes, una salida seleccionable o ambas. Una sola salida combinada no satisface el requisito de fuentes independientes.

La cámara virtual de video tampoco crea un micrófono virtual: el audio fuera de OBS requiere su propio camino, con coste adicional de desarrollo y distribución. Windows 10 necesita evaluación separada.

## 9. Conectividad

- Primera etapa: Wi-Fi local, preferiblemente PC por Ethernet y teléfonos en una red de buena capacidad.
- Descubrimiento automático como comodidad; IP manual como alternativa cuando multicast esté bloqueado.
- USB: distinguir tethering USB como red de transporte de ADB con depuración. Probar tethering con fabricantes y redes reales; no prometer un modo plug-and-play hasta validarlo. ADB puede servir para desarrollo, no como requisito cotidiano del cliente.
- Internet: etapa posterior con señalización remota, STUN/TURN, autenticación y costes de relay. No es requisito de la primera versión local.

## 10. Audio, sincronía y latencia

Usar timestamps, buffers acotados y ajustes de desfase por fuente. WebRTC ayuda a sincronizar audio/video dentro de una sesión, pero no garantiza sincronía entre teléfonos diferentes. Medir desfase entre cámaras mediante un estímulo visual y sonoro común.

Evitar habilitar todos los micrófonos por defecto: puede causar eco y duplicación. Permitir elegir un micrófono principal y silenciar las otras fuentes. Una grabación de respaldo en el teléfono requiere sincronización y consumo adicional; se valida aparte.

Metas iniciales, no garantías:

- Dos teléfonos a 1080p30 durante 60 minutos en un PC y red de referencia documentados.
- Latencia de extremo a extremo mediana de hasta 200 ms en LAN de referencia; registrar también p95, pérdidas y carga.
- Reconexión en hasta 10 segundos después de restablecerse la red en el escenario de prueba.
- Error residual de sincronía entre fuentes de hasta 80 ms tras calibración en prueba controlada.
- Beta de cuatro teléfonos a 1080p30 durante dos horas antes de anunciar ese nivel de soporte.

Ejemplo de dimensionamiento: cuatro fuentes a 6 Mbps consumen 24 Mbps de video, más audio, protocolo, retransmisiones y margen de red. El bitrate real debe configurarse y medirse; la velocidad nominal del router no garantiza capacidad útil.

## 11. Fases y criterios de salida

| Fase | Entrega | Criterio para avanzar |
|---|---|---|
| 0. Viabilidad | Un Android transmite al receptor Windows | Captura, transporte y decodificación medidos; inventario de hardware |
| 1. Multicámara | Dos teléfonos, QR, nombres y reconexión | 60 minutos y recuperación de un dispositivo sin afectar al otro |
| 2. OBS | Complemento y fuentes independientes | Dos cámaras utilizables en escenas distintas y reutilización sin sesiones duplicadas |
| 3. Producto base | Controles, perfiles, diagnóstico e instalador | Instalación limpia y sesión completa sin herramientas de desarrollo |
| 4. Compatibilidad | Prototipo y salida de cámara virtual | Matriz de programas validada y política de audio definida |
| 5. Avanzado | Cuatro cámaras, USB, tally, grabación de respaldo | Pruebas sostenidas y evaluación por función |
| 6. Comercial | Licencias, distribución y soporte | Producto estable, actualizaciones verificadas y revisión de dependencias |

No fijar fecha de lanzamiento hasta completar las pruebas de transporte nativo, integración OBS y cámara virtual. Son los principales riesgos técnicos.

## 12. Calidad y pruebas

- Matriz mínima: varios fabricantes, un teléfono antiguo y uno de gama media; versiones Android distintas.
- Pruebas con 1, 2 y 4 dispositivos; diferentes combinaciones de resolución y FPS.
- Corte de Wi-Fi, cambio de IP, cierre de la app, reinicio del receptor y desconexión USB.
- Escenarios térmicos y de carga durante sesiones largas.
- Permisos denegados, revocación de confianza, QR caducado y controles no autorizados.
- Varias escenas OBS reutilizando una fuente y coexistencia de diferentes consumidores.
- Verificar instalación/desinstalación, ausencia de procesos huérfanos y liberación de cámara/micrófono.
- Registrar hardware, versiones, p50/p95 de latencia, FPS, bitrate, pérdidas, CPU, GPU y memoria. No etiquetar una métrica de red como latencia total de cámara a pantalla.

## 13. Modelo comercial propuesto

Primero construir una beta completa para validar utilidad. Después evaluar:

| Edición | Propuesta por validar |
|---|---|
| Personal | Una cámara, calidad base y todas las funciones esenciales de fiabilidad |
| Pro | Multicámara, Full HD, controles remotos y perfiles |
| Studio | Más cámaras certificadas, respaldo, tally y flujos de producción avanzados |

No fijar precios ni límites definitivos todavía. Evitar anuncios durante producción. Valorar licencia por estación de trabajo para que usar varios teléfonos no obligue a pagar por cada uno. Las funciones remotas con costes de servidor podrían tener un modelo separado. La resolución disponible siempre depende del hardware.

## 14. Estructura futura del repositorio

```text
android/                 Aplicación Android
desktop/                 Interfaz Windows
native/                  Transporte y motor multimedia
integrations/obs/        Complemento OBS
integrations/windows/    Cámara virtual
protocol/                Mensajes, versiones y estados compartidos
docs/                    Especificaciones y decisiones
tools/                   Desarrollo y diagnóstico
```

Crear estas carpetas al iniciar cada implementación, evitando proyectos vacíos que aparenten funcionalidad terminada.

## 15. Próxima decisión de implementación

Comenzar con una prueba vertical Android → Windows a 720p30 y después agregar un segundo teléfono. La primera demostración debe probar video real y recuperación de conexión. Validar en paralelo conceptual la interfaz nativa de frames para que el complemento OBS y la cámara virtual no requieran rehacer el motor.

Pendientes para ajustar alcance: versión de Windows, modelos Android de prueba, programas de streaming prioritarios además de OBS y número deseado de cámaras simultáneas. Mientras se recopilan, usar dos cámaras como objetivo inicial y cuatro como beta.

## Fuentes oficiales consultadas

- DroidCam: https://droidcam.app/
- DroidCam multicámara OBS: https://droidcam.app/obs/usage/
- OBS Studio: https://obsproject.com/
- Complementos OBS: https://docs.obsproject.com/plugins
- WebRTC: https://webrtc.org/getting-started/peer-connections
- Capacidades Camera2: https://developer.android.com/reference/android/hardware/camera2/CameraCharacteristics
- Servicios de cámara Android: https://developer.android.com/develop/background-work/services/fgs/service-types
- Cámara virtual Windows: https://learn.microsoft.com/en-us/windows/win32/api/mfvirtualcamera/nf-mfvirtualcamera-mfcreatevirtualcamera
- Ejemplo Microsoft: https://github.com/microsoft/Windows-Camera/tree/master/Samples/VirtualCamera

Las tecnologías y límites de plataforma deben verificarse nuevamente al comenzar la implementación. Este documento es una propuesta técnica y de producto, no una declaración de compatibilidad probada.
