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
        Closed += (_, _) => { displayTimer.Stop(); usbAuto?.Dispose(); hub?.Dispose(); foreach (var view in outputWindows.Values.ToArray()) view.Close(); };
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
            hub.FrameReceived += (id, frame) => pendingFrames[id] = frame;
            hub.VideoStateChanged += (id, active, message) => Dispatcher.BeginInvoke(() =>
            {
                var camera = cameraDevices.FirstOrDefault(c => c.Id == id);
                if (!active) { pendingFrames.TryRemove(id, out _); camera?.Clear(message); }
                else if (camera != null) camera.VideoStatus = message;
            });
            hub.ApproveConnection = (name, address, code) => Dispatcher.InvokeAsync(() =>
                MessageBox.Show(this, "El teléfono " + name + " (" + address + ") aceptó la solicitud.\n\nCódigo: " + code +
                    "\n\n¿Coincide con el código que muestra ese teléfono? Acepta sólo si coincide.",
                    "Confirmar conexión · WT MultiCam", MessageBoxButton.YesNo, MessageBoxImage.Question,
                    MessageBoxResult.No) == MessageBoxResult.Yes).Task;
            hub.Start();
            usbAuto = new UsbAutoConnector(hub);
            usbAuto.Status += message => Dispatcher.BeginInvoke(() => UsbProgress.Text = message);
            usbAuto.Start();
            ReceiverStatus.Text = "Activo";
            RefreshDevices();
        }
        catch (Exception ex)
        {
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
            camera.Status = d.Paired ? "Emparejado" : "Disponible para conectar";
        }
        foreach (var old in cameraDevices.Where(c => !devices.Any(d => d.Id == c.Id)).ToArray())
        {
            old.IsPaired = false;
            old.Status = "Teléfono desconectado";
            old.Clear("Teléfono desconectado");
            pendingFrames.TryRemove(old.Id, out _);
            // Keep an opened OBS source alive and reuse the same view model on reconnection.
            if (outputWindows.ContainsKey(old.Id)) continue;
            cameraDevices.Remove(old);
        }
        DeviceCount.Text = devices.Count(d => d.Paired) + " emparejados";
        DiscoveryStatus.Text = devices.Length == 0
            ? "Buscando teléfonos con WT MultiCam abierta. Si no aparecen, usa Agregar teléfono para IP, USB o QR."
            : devices.Length + " dispositivo(s). Empareja por QR y pulsa Iniciar cámara en Android. Abre la fuente para usar Captura de ventana en OBS.";
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
                }
                catch (Exception ex) when (ex is IOException or FormatException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException)
                { camera.Clear("Frame no compatible"); }
            }
            else if (camera.HasFrame && DateTime.UtcNow - camera.LastFrame > TimeSpan.FromSeconds(3)) camera.Clear("Sin señal de video");
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
}
