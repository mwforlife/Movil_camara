using System.Windows;
using MovilCamara.Services;
using MovilCamara.ViewModels;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MovilCamara;

public partial class MainWindow : Window
{
    private ConnectionHub? hub;
    private UsbAutoConnector? usbAuto;
    private ObsDirectServer? obsOutput;
    private MultiViewWindow? multiView;
    private VirtualCameraOutput? virtualCameras;
    private readonly ObservableCollection<CameraDeviceViewModel> cameraDevices = new();
    private readonly ConcurrentDictionary<string, VideoFrame> pendingFrames = new();
    private readonly Dictionary<string, VideoOutputWindow> outputWindows = new();
    private readonly DispatcherTimer displayTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    public MainWindow()
    {
        InitializeComponent();
        DeviceList.ItemsSource = cameraDevices;
        displayTimer.Tick += (_, _) => DisplayFrames();
        displayTimer.Start();
        Loaded += (_, _) => StartReceiver();
        Closed += (_, _) => { multiView?.Close(); displayTimer.Stop(); virtualCameras?.Dispose(); obsOutput?.Dispose(); usbAuto?.Dispose(); hub?.Dispose(); foreach (var view in outputWindows.Values.ToArray()) view.Close(); };
    }

    private void StartReceiver()
    {
        if (hub != null) return;
        try
        {
            hub = new ConnectionHub(persistIdentity: true);
            hub.Changed += () => Dispatcher.BeginInvoke(RefreshDevices);
            hub.Error += message => Dispatcher.BeginInvoke(() => DiscoveryStatus.Text = message);
            hub.ScanStatus += message => Dispatcher.BeginInvoke(() => SearchProgress.Text = message);
            hub.FrameReceived += (id, frame) => { pendingFrames[id] = frame; obsOutput?.Offer(id, frame); };
            hub.AudioReceived += (id, frame) => obsOutput?.OfferAudio(id, frame);
            hub.AudioStateChanged += (id, active) =>
            {
                if (!active) obsOutput?.ClearAudio(id);
                Dispatcher.BeginInvoke(() =>
                {
                    var camera = cameraDevices.FirstOrDefault(c => c.Id == id);
                    if (camera != null) camera.AudioStatus = active ? "Micrófono recibido · 48 kHz mono" : "Micrófono apagado";
                });
            };
            hub.VideoStateChanged += (id, active, message) => Dispatcher.BeginInvoke(() =>
            {
                var camera = cameraDevices.FirstOrDefault(c => c.Id == id);
                if (!active) { pendingFrames.TryRemove(id, out _); obsOutput?.Clear(id); virtualCameras?.Clear(id); camera?.Clear(message); }
                else if (camera != null) camera.VideoStatus = message;
            });
            hub.ApproveConnection = (name, address, code) => Dispatcher.InvokeAsync(() =>
                MessageBox.Show(this, "El teléfono " + name + " (" + address + ") aceptó la solicitud.\n\nCódigo: " + code +
                    "\n\n¿Coincide con el código que muestra ese teléfono? Acepta sólo si coincide.",
                    "Confirmar conexión · WT MultiCam", MessageBoxButton.YesNo, MessageBoxImage.Question,
                    MessageBoxResult.No) == MessageBoxResult.Yes).Task;
            hub.Start();
            virtualCameras = new VirtualCameraOutput();
            foreach (var config in virtualCameras.Configurations)
            {
                if (cameraDevices.Any(c => c.Id == config.DeviceId)) continue;
                cameraDevices.Add(new CameraDeviceViewModel(config.DeviceId) { Name = config.Name.Replace("WT MultiCam — ", ""),
                    Status = "Teléfono desconectado", VirtualCameraActive = true, VirtualCameraStatus = "Recuperando cámara virtual…" });
            }
            virtualCameras.Status += (id, message) => Dispatcher.BeginInvoke(() =>
            {
                var camera = cameraDevices.FirstOrDefault(c => c.Id == id);
                if (camera != null) { camera.VirtualCameraStatus = message; camera.VirtualCameraActive = virtualCameras.IsConfigured(id); }
                SearchProgress.Text = message;
            });
            _ = virtualCameras.RestoreAsync();
            try
            {
                var keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WilkensTech", "WTMultiCam", "obs-output.key");
                obsOutput = new ObsDirectServer(ObsFrameEncoder.Encode, ObsFrameEncoder.NoSignal(), keyPath);
                obsOutput.Error += message => Dispatcher.BeginInvoke(() => SearchProgress.Text = message);
                obsOutput.Start();
                ObsOutputStatus.Text = "Directa local";
            }
            catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or UnauthorizedAccessException)
            {
                obsOutput?.Dispose(); obsOutput = null;
                ObsOutputStatus.Text = "No disponible";
                SearchProgress.Text = "No se pudo iniciar la salida OBS: " + ex.Message;
            }
            usbAuto = new UsbAutoConnector(hub);
            usbAuto.Status += message => Dispatcher.BeginInvoke(() => UsbProgress.Text = message);
            usbAuto.Start();
            ReceiverStatus.Text = "Activo";
            RefreshDevices();
        }
        catch (Exception ex)
        {
            virtualCameras?.Dispose(); virtualCameras = null;
            hub?.Dispose(); hub = null;
            ReceiverStatus.Text = "No disponible";
            DiscoveryStatus.Text = "No se pudo iniciar el receptor: " + ex.Message;
        }
    }

    private void RefreshDevices()
    {
        if (hub == null) return;
        var devices = hub.Devices;
        foreach (var d in devices)
        {
            var camera = cameraDevices.FirstOrDefault(c => c.Id == d.Id);
            if (camera == null) { camera = new CameraDeviceViewModel(d.Id); cameraDevices.Add(camera); }
            camera.Name = d.Name; camera.Address = d.Address; camera.IsPaired = d.Paired;
            camera.AudioStatus = hub.IsAudioActive(d.Id) ? "Micrófono recibido · 48 kHz mono" : "Micrófono apagado";
            camera.Status = d.Paired ? "Emparejado" : "Disponible para conectar";
            camera.VirtualCameraActive = virtualCameras?.IsConfigured(d.Id) == true;
            if (virtualCameras?.IsActive(d.Id) == true) camera.VirtualCameraStatus = "Cámara virtual activa · 1080p30";
        }
        foreach (var old in cameraDevices.Where(c => !devices.Any(d => d.Id == c.Id)).ToArray())
        {
            old.IsPaired = false;
            old.Status = "Teléfono desconectado";
            old.Clear("Teléfono desconectado");
            pendingFrames.TryRemove(old.Id, out _);
            virtualCameras?.Clear(old.Id);
            // Keep an opened OBS source alive and reuse the same view model on reconnection.
            if (outputWindows.ContainsKey(old.Id)) continue;
            if (old.HasBeenPaired) continue;
            if (virtualCameras?.IsConfigured(old.Id) == true) continue;
            cameraDevices.Remove(old);
        }
        DeviceCount.Text = devices.Count(d => d.Paired) + " emparejados";
        DiscoveryStatus.Text = devices.Length == 0
            ? "Buscando teléfonos con WT MultiCam abierta. Si no aparecen, usa Agregar teléfono para IP, USB o QR."
            : devices.Length + " dispositivo(s). Inicia la cámara en Android y copia su enlace OBS para añadir una Fuente multimedia.";
    }

    private void DisplayFrames()
    {
        foreach (var camera in cameraDevices)
        {
            if (pendingFrames.TryRemove(camera.Id, out var frame))
            {
                try
                {
                    BitmapSource source;
                    if (frame.Bgr32 != null)
                        source = BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Bgr32, null, frame.Bgr32, frame.Width * 4);
                    else
                    {
                        using var stream = new MemoryStream(frame.Jpeg, false);
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit();
                        source = bitmap;
                    }
                    source.Freeze();
                    var rotation = (frame.Rotation + camera.RotationOffset) % 360;
                    if (rotation != 0) { source = new TransformedBitmap(source, new RotateTransform(rotation)); source.Freeze(); }
                    if (frame.Mirrored) { source = new TransformedBitmap(source, new ScaleTransform(-1, 1)); source.Freeze(); }
                    camera.Display(source, frame.Received);
                    virtualCameras?.Offer(camera.Id, source);
                }
                catch (Exception ex) when (ex is IOException or FormatException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException)
                { camera.Clear("Frame no compatible"); }
            }
            else if (camera.HasFrame && DateTime.UtcNow - camera.LastFrame > TimeSpan.FromSeconds(3)) { camera.Clear("Sin señal de video"); virtualCameras?.Clear(camera.Id); }
        }
    }

    private void OpenVideo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string id) return;
        var camera = cameraDevices.FirstOrDefault(c => c.Id == id);
        if (camera == null) return;
        if (outputWindows.TryGetValue(id, out var existing))
        {
            if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
            existing.Show(); existing.Activate(); return;
        }
        var output = new VideoOutputWindow(camera);
        outputWindows[id] = output;
        output.Closed += (_, _) => outputWindows.Remove(id);
        output.Show();
    }

    private void CopyObsLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string id) return;
        if (obsOutput == null)
        {
            MessageBox.Show(this, "La salida directa no está disponible. Revisa el estado del receptor.", "WT MultiCam");
            return;
        }
        try
        {
            Clipboard.SetText(obsOutput.GetUrl(id));
            MessageBox.Show(this,
                "Enlace copiado. En OBS:\n\n1. Añade una Fuente multimedia.\n2. Desmarca Archivo local.\n3. Pega el enlace en Entrada.\n4. En Formato de entrada escribe matroska.\n5. Ajusta Buffering de la red a 0 MB.\n\nActiva Enviar micrófono en Android para recibir audio.\n\nCrea una fuente por teléfono y mantén WT MultiCam abierto. No necesitas abrir ni capturar una ventana.",
                "Conectar cámara a OBS", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or ArgumentException)
        { SearchProgress.Text = "No se pudo copiar el enlace OBS: " + ex.Message; }
    }

    private async void VirtualCamera_Click(object sender, RoutedEventArgs e)
    {
        if (virtualCameras == null || sender is not System.Windows.Controls.Button button || button.Tag is not string id) return;
        var camera = cameraDevices.FirstOrDefault(c => c.Id == id);
        if (camera == null) return;
        if (!camera.HasBeenPaired && !virtualCameras.IsConfigured(id))
        { SearchProgress.Text = "Empareja el teléfono antes de activar su cámara virtual."; return; }
        button.IsEnabled = false;
        try
        {
            if (virtualCameras.IsConfigured(id)) await virtualCameras.DisableAsync(id);
            else
            {
                await virtualCameras.EnableAsync(id, camera.Name);
                if (camera.Source != null) virtualCameras.Offer(id, camera.Source);
                MessageBox.Show(this, "En OBS añade Dispositivo de captura de video y selecciona WT MultiCam — " + camera.Name +
                    ".\n\nUsa 1920 × 1080, 30 FPS y NV12. Mantén el receptor abierto.\nPara el micrófono conserva la Fuente multimedia activa, debajo de la cámara virtual en la lista de fuentes.",
                    "Cámara virtual", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        { camera.VirtualCameraStatus = "Registro cancelado. Puedes activarla de nuevo cuando quieras."; }
        catch (Exception ex)
        { camera.VirtualCameraStatus = "No se pudo activar la cámara virtual: " + ex.Message; SearchProgress.Text = camera.VirtualCameraStatus; }
        finally { camera.VirtualCameraActive = virtualCameras.IsConfigured(id); button.IsEnabled = true; }
    }

    private async void ScanNow_Click(object sender, RoutedEventArgs e)
    {
        if (hub != null) await hub.ScanNowAsync();
    }

    private async void RequestConnection_Click(object sender, RoutedEventArgs e)
    {
        if (hub == null || sender is not System.Windows.Controls.Button button || button.Tag is not string id) return;
        button.IsEnabled = false;
        try
        {
            await hub.RequestConnectionAsync(id);
            SearchProgress.Text = "Solicitud enviada. Revisa el teléfono y compara el código de verificación en ambos equipos.";
        }
        catch (Exception ex) { SearchProgress.Text = "No se pudo solicitar conexión: " + ex.Message; }
        finally { button.IsEnabled = true; }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            SystemCommands.RestoreWindow(this);
        else
            SystemCommands.MaximizeWindow(this);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (MaximizeButton is null) return;
        var maximized = WindowState == WindowState.Maximized;
        MaximizeButton.Content = maximized ? "\uE923" : "\uE922";
        MaximizeButton.ToolTip = maximized ? "Restaurar" : "Maximizar";
        System.Windows.Automation.AutomationProperties.SetName(MaximizeButton, maximized ? "Restaurar" : "Maximizar");
        // Reserve the invisible resize frame when Windows maximizes the window.
        WindowLayout.Margin = maximized ? new Thickness(SystemParameters.WindowResizeBorderThickness.Left + 4) : new Thickness(0);
    }

    private void AddPhone_Click(object sender, RoutedEventArgs e)
    {
        if (hub == null) { StartReceiver(); if (hub == null) return; }
        new PairingWindow(hub) { Owner = this }.ShowDialog();
    }
    private void OpenMultiview_Click(object sender, RoutedEventArgs e)
    {
        if (hub == null) { StartReceiver(); if (hub == null) return; }
        if (multiView != null)
        {
            if (multiView.WindowState == WindowState.Minimized) multiView.WindowState = WindowState.Normal;
            multiView.Show(); multiView.Activate(); return;
        }
        multiView = new MultiViewWindow(cameraDevices, hub);
        multiView.Closed += (_, _) => multiView = null;
        multiView.Show();
    }
}
