using System.Configuration;
using System.Data;
using System.Windows;

namespace MovilCamara
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private System.Threading.Mutex? instance;
        private bool ownsInstance;
        protected override async void OnStartup(StartupEventArgs e)
        {
            if (e.Args.Contains("--verify-installation") || e.Args.Contains("--prepare-uninstall"))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                base.OnStartup(e);
                var reportPath = e.Args.FirstOrDefault(arg => arg.StartsWith("--report="))?[9..];
                try
                {
                    if (e.Args.Contains("--verify-installation"))
                    {
                        _ = typeof(QRCoder.QRCodeGenerator).Assembly.GetName();
                        var root = System.IO.Path.GetFullPath(AppContext.BaseDirectory);
                        if (!typeof(object).Assembly.Location.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
                            !typeof(Application).Assembly.Location.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("El paquete no está cargando .NET/WPF desde su carpeta.");
                        var native = System.Runtime.InteropServices.NativeLibrary.Load(System.IO.Path.Combine(AppContext.BaseDirectory, "Native", "WTMultiCam.VirtualCamera.dll"));
                        try { _ = System.Runtime.InteropServices.NativeLibrary.GetExport(native, "WT_OpenPublisher"); }
                        finally { System.Runtime.InteropServices.NativeLibrary.Free(native); }
                        if (Services.UsbConnector.FindAdb() == null) throw new System.IO.FileNotFoundException("No se encuentra ADB");
                    }
                    else
                    {
                        using var cameras = new Services.VirtualCameraOutput();
                        foreach (var camera in cameras.Configurations) await cameras.DisableAsync(camera.DeviceId);
                    }
                    if (reportPath != null) System.IO.File.WriteAllText(reportPath, "OK: runtime y WPF locales; proveedor nativo cargado; ADB incluido.");
                    Shutdown(0);
                }
                catch (Exception ex)
                {
                    if (reportPath != null) System.IO.File.WriteAllText(reportPath, ex.ToString());
                    if (e.Args.Contains("--prepare-uninstall"))
                        MessageBox.Show("No se pudieron retirar las cámaras virtuales. Cierra OBS y vuelve a intentarlo.\n\n" + ex.Message, "WT MultiCam");
                    Shutdown(1);
                }
                return;
            }
            instance = new System.Threading.Mutex(false, "Local\\WTMultiCam.Desktop");
            try { ownsInstance = instance.WaitOne(0); }
            catch (System.Threading.AbandonedMutexException) { ownsInstance = true; }
            if (!ownsInstance)
            {
                MessageBox.Show("WT MultiCam ya está abierto. Revisa su ventana en la barra de tareas.", "WT MultiCam");
                Shutdown(); return;
            }
            base.OnStartup(e);
            MainWindow = new MainWindow();
            MainWindow.Show();
        }
        protected override void OnExit(ExitEventArgs e)
        {
            if (ownsInstance) instance?.ReleaseMutex();
            instance?.Dispose();
            base.OnExit(e);
        }
    }

}
