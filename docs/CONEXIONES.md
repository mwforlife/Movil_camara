# Conexiones de WT MultiCam

## Alcance implementado

Este documento describe identificación y emparejamiento. La recepción de video inicial se explica en [Primera prueba de video](VIDEO_PRIMERA_PRUEBA.md). No hay todavía audio ni complemento OBS nativo.

## Detección automática reforzada y solicitud desde Windows

Además del broadcast UDP, Windows busca periódicamente el servicio TCP 47778 de WT MultiCam en las interfaces Ethernet/Wi-Fi activas. No usa ping ni intenta conectar con cámaras de otros fabricantes. “Buscar ahora” permite iniciar otra búsqueda si no hay una en curso.

Para redes de hasta 1024 direcciones respeta la máscara de subred. En redes mayores limita cada interfaz al bloque /24 donde está la IP del PC; el máximo total por pasada es de 2048 direcciones, con 32 consultas concurrentes y un límite de 800 ms por dirección. La siguiente pasada comienza 20 segundos después de terminar la anterior. En redes grandes con el teléfono en otro bloque, usa la búsqueda por IP. No se recorren rangos públicos externos.

Los dispositivos no emparejados permanecen visibles hasta 90 segundos desde la última detección, evitando que desaparezcan entre búsquedas. Las sesiones emparejadas conservan su comprobación autenticada y caducidad de unos 16 segundos.

Flujo sin QR:
1. Actualiza ambas aplicaciones y deja abierta la pantalla principal en Android.
2. En Windows espera la búsqueda o pulsa “Buscar ahora”.
3. En el dispositivo disponible pulsa “Solicitar conexión”.
4. Android muestra la solicitud con la IP del PC y un código. Pulsa “Continuar” si la solicitaste.
5. Compara el código de seis dígitos en el teléfono y Windows. Acepta en Windows sólo si coinciden.
6. El teléfono cambia a emparejado. Un rechazo no crea la sesión.

La invitación de red contiene dirección, huella de certificado y un identificador temporal; no transporta la credencial de sesión ni el token de un QR. Android conecta mediante TLS y envía un secreto aleatorio por ese canal. El código visible se deriva de la huella y ese secreto; Windows requiere una confirmación explícita. Cada invitación queda vinculada a un dispositivo, caduca a los 90 segundos y sólo admite un intento de autorización. La confirmación final tiene un límite de aproximadamente 60 segundos.

La detección UDP anterior puede fallar por filtrado broadcast o selección de interfaces. La búsqueda TCP agrega una alternativa; no evita aislamiento de clientes, VLAN o reglas de firewall que bloqueen el puerto TCP del teléfono. El QR permanece disponible.

Verificación de esta corrección: WPF compila; pruebas sin sockets para máscaras /30, /32 y límite /24 pasan; ConnectionManager.kt compila por separado contra Android SDK 36 y Kotlin 2.2.10. Esto no sustituye una compilación completa del APK ni una prueba Android-PC real. El entorno de ejecución sigue impidiendo el transporte TCP local.


- Descubrimiento automático: Windows envía una consulta UDP cada 3 segundos a las direcciones broadcast de las interfaces activas. Android responde mientras la pantalla principal está abierta.
- Red manual: Windows consulta la IP IPv4 del teléfono por TCP y lo agrega como dispositivo detectado.
- Emparejamiento: el teléfono escanea el QR del PC o pega su código completo. El QR incluye dirección, puerto, huella del certificado y una autorización aleatoria de un solo uso válida por 2 minutos.
- Canal de control: TLS 1.2/1.3, con certificado del PC validado por la huella recibida en el QR. El emparejamiento entrega una credencial de sesión diferente del token QR.
- Estado: cada teléfono emparejado confirma su presencia cada 5 segundos. Tras unos 16 segundos sin señal, desaparece del panel. Descubrir metadatos no renueva una sesión autenticada.
- Varias sesiones: los dispositivos se identifican por UUID persistente generado por la app, no por dirección IP.
- Las credenciales de sesión viven en memoria. Reiniciar el PC o recrear la app requiere volver a emparejar. No hay almacén de dispositivos de confianza todavía.
- Cerrar el diálogo de emparejamiento revoca el QR pendiente. Debe permanecer abierto mientras se escanea.
- La cámara se utiliza para el escáner QR o cuando el usuario pulsa explícitamente Iniciar cámara y enviar al PC. Detectar o emparejar no activa la captura.

## Prueba desde las aplicaciones

1. Sincroniza Gradle en Android Studio: se agregó ZXing Embedded 4.3.0 para el escáner QR.
2. Compila e instala la app Android actualizada en cada teléfono.
3. Ejecuta Windows. El receptor debe indicar “Activo”.
4. Conecta PC y teléfonos a la misma red y deja la pantalla principal de la app Android abierta.
5. Espera varios segundos: cada teléfono debe aparecer como “Detectado · sin emparejar”.
6. En Windows, abre “Agregar teléfono”, selecciona la IPv4 del PC que pertenezca a esa red y escanea el QR desde Android.
7. Debe cambiar a “Emparejado · sin video”.
8. Para el segundo teléfono, genera un QR nuevo y repite. Cada QR autoriza sólo un emparejamiento.
9. Cierra o manda al fondo la app de uno de los teléfonos. Pasado el tiempo de detección, sólo ese dispositivo debe desaparecer.
10. Regresa a la app: si la instancia conserva la sesión en memoria y el PC no se reinició, reintentará la conexión.

La IP del teléfono se muestra en Android. Si falla el descubrimiento, introdúcela en “Buscar por IP” de Windows; después completa la autorización por QR o código pegado.

Si el PC tiene VPN, Ethernet, Wi-Fi o adaptadores virtuales, elige la dirección alcanzable por el teléfono. El primer adaptador de la lista puede no ser el correcto.

## USB

### Anclaje de red USB

Conecta el cable y activa “Anclaje de red USB” en los ajustes Android. Esto crea una red entre PC y teléfono. Vuelve a la app y usa el descubrimiento, IP manual o QR con la dirección del adaptador USB del PC. La disponibilidad y asignación de direcciones dependen del fabricante.

### ADB para desarrollo

1. Instala Android SDK Platform-Tools. La app busca adb.exe en ANDROID_HOME, ANDROID_SDK_ROOT o la ubicación habitual del SDK del usuario.
2. Habilita depuración USB y autoriza este PC en el teléfono.
3. Mantén WT MultiCam abierta.
4. En Windows, “Agregar teléfono” → “Detectar dispositivos USB (ADB)”.
5. Selecciona el número de serie correcto y pulsa “Preparar USB y generar QR”.
6. La app hace un forward TCP temporal para comprobar que WT MultiCam responde, lo elimina y crea un reverse del puerto TCP 47779.
7. Escanea el QR resultante desde el teléfono seleccionado. La dirección 127.0.0.1 de ese QR usa el túnel ADB, no la Wi-Fi.
8. El túnel reverse permanece disponible durante el uso. Para retirarlo manualmente: adb -s NUMERO_SERIE reverse --remove tcp:47779.

El modo ADB no es USB plug-and-play para distribución comercial. Requiere herramientas y autorización de desarrollo. No habilitamos ADB por red ni abrimos el puerto 5555. Varias sesiones USB se distinguen por UUID aunque compartan la dirección loopback.

## Puertos y redes

| Puerto | Transporte | Destino | Uso |
|---|---|---|---|
| 47777 | UDP | Android | Consulta de descubrimiento; responde al puerto de origen |
| 47778 | TCP | Android | Metadatos de dispositivo para búsqueda manual/USB |
| 47779 | TCP/TLS | Windows | Emparejamiento y presencia autenticada |

Permite WT MultiCam en el firewall de Windows para redes privadas si lo solicita. La app no modifica automáticamente las reglas del firewall. No se necesita abrir puertos del router ni acceso a Internet para conectar por LAN.

La app anuncia sólo identificador aleatorio y nombre de fabricante/modelo por descubrimiento. Esa información no está autenticada y se muestra como detectada, nunca como emparejada. Redes de invitados, VLAN separadas, aislamiento entre clientes, VPN y políticas empresariales pueden bloquear incluso las búsquedas por IP.

## Verificación técnica

- Windows: dotnet build Escritorio/MovilCamara/MovilCamara.csproj
- Recursos Android: aapt2 compile --dir app/src/main/res -o build/connection-resources.zip
- QR sin sockets: dotnet run --project tools/ConnectionChecks/ConnectionChecks.csproj -- --offline
- Integración TLS local: dotnet run --project tools/ConnectionChecks/ConnectionChecks.csproj

Las comprobaciones de integración usan datos ficticios para verificar dos sesiones independientes, QR de un solo uso, revocación, renovación, rechazo de credenciales incorrectas, pin del certificado y búsqueda por IP sin autorización implícita. Ejecutarlas sin otra instancia del receptor, porque usan los mismos puertos.

Validado durante implementación: compilación WPF, compilación de recursos Android, generación visual del QR y comprobaciones sin sockets. El entorno de implementación bloquea la conexión TCP local con SocketException 10013 e impide comunicar con el daemon de Gradle. No se ha validado aquí el APK completo, una pareja Android-PC real, el escaneo físico del QR ni el transporte por USB. Deben probarse desde el entorno del usuario.

## Próximas mejoras

Persistencia segura y revocación de confianza, servicio de sesión para continuar con la app en segundo plano, descubrimiento mediante DNS-SD/mDNS como alternativa, diagnóstico de interfaces, gestión del túnel USB y transmisión multimedia. Hasta entonces, mantener la pantalla principal abierta.
