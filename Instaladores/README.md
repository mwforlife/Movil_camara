# WT MultiCam — Instalador Windows

Archivo listo para probar: [WTMultiCam-Setup-1.0.0-beta.1-win-x64.exe](Windows/WTMultiCam-Setup-1.0.0-beta.1-win-x64.exe).

## Qué incluye

- Receptor de escritorio y proveedor de cámara virtual.
- .NET y WPF 8.0.31 integrados en la aplicación; no requiere instalar .NET aparte ni tener Visual Studio.
- ADB oficial y bibliotecas auxiliares, con NOTICE y versión de Platform Tools.
- Asistente en español, identidad WilkensTech, icono propio, acceso de Inicio y escritorio opcional.
- Desinstalación del receptor y retirada de cámaras configuradas para el usuario, tras confirmar la desinstalación. Conserva vínculos/preferencias y componentes protegidos compartidos del proveedor.

Windows 11 de 64 bits. Instalación por usuario en `%LOCALAPPDATA%\Programs\WilkensTech\WTMultiCam`. La primera activación de la cámara virtual sigue solicitando autorización de Windows. USB requiere autorizar depuración y, según fabricante, un controlador USB. No se abre ni instala Android: el usuario ya generó su APK y pidió limitar el trabajo a Windows.

## Uso

1. Cierra todas las copias del receptor y ejecuta el instalador.
2. Elige carpeta y si quieres acceso en el escritorio.
3. Abre WT MultiCam y conecta el teléfono con tu APK.
4. Activa la cámara virtual y comprueba imagen/audio en OBS.

Para actualizar utiliza el mismo instalador con la aplicación cerrada. Para desinstalar usa Configuración de Windows o el acceso Desinstalar. Si una cámara está en uso, cierra OBS y otras aplicaciones que la utilicen y vuelve a intentarlo. Los registros COM/proveedores protegidos se conservan deliberadamente para no borrar componentes compartidos o de otras cuentas.

## Recompilar

Inno Setup 6, SDK .NET con herramientas WPF y C++/Windows SDK para generar el proveedor; Platform Tools oficial para incluir ADB. Estos requisitos son del equipo de compilación, no del usuario final.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Instaladores\Fuentes\Compilar-Windows.ps1
```

En este equipo se usó `-OfflineLocal` con paquetes .NET 8.0.31 ya almacenados y apphost 8.0.34 instalado. El script usa el apphost local para evitar descargar el paquete del host faltante; no cambia el runtime que se integra. Con conexión, el modo normal permite restaurar los paquetes que seleccione el SDK. Revisar/actualizar los runtimes al preparar cada entrega; este paquete no se actualiza automáticamente con el .NET global de Windows.

Fuentes: `Fuentes/WTMultiCam.iss`, `Fuentes/Compilar-Windows.ps1`, arte de marca en `Fuentes/Assets`. Resultado en `Windows`; SHA-256 en [SHA256.txt](Windows/SHA256.txt).

## Verificación realizada y pendiente

- Publicación Release x64 autocontenida y compilación Inno Setup correctas.
- Comprobación ejecutando el EXE publicado: .NET y WPF cargados desde su propia carpeta, proveedor nativo cargado y ADB localizado. No abre conexiones ni registra cámaras.
- ADB incluido ejecuta `version` correctamente.
- Falta probar instalación, actualización y desinstalación en un Windows 11 limpio. No se ejecutó el instalador en el equipo del usuario ni se alteraron sus cámaras actuales.
- Edición beta sin firma comercial de código. Wi-Fi continúa con desconexiones en revisión; el instalador no cambia ese estado.
