using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MovilCamara.Services;
using QRCoder;

namespace MovilCamara;

public partial class PairingWindow : Window
{
    private readonly ConnectionHub hub;
    public PairingWindow(ConnectionHub hub)
    {
        this.hub = hub;
        InitializeComponent();
        HostAddress.ItemsSource = ConnectionHub.LocalAddresses();
        HostAddress.SelectedIndex = HostAddress.Items.Count > 0 ? 0 : -1;
        if (HostAddress.SelectedIndex < 0) TicketStatus.Text = "No hay una dirección de red activa.";
    }
    private void AddressChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QrImage != null && HostAddress.SelectedItem is string host) Generate(host);
    }
    private void Generate(string host)
    {
        try
        {
            var payload = hub.CreateTicket(host);
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
            using var code = new PngByteQRCode(data);
            using var stream = new MemoryStream(code.GetGraphic(6));
            var bitmap = new BitmapImage();
            bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit();
            QrImage.Source = bitmap;
            PairingCode.Text = payload;
            TicketStatus.Text = "Escanea con WT MultiCam. Caduca a las " + DateTime.Now.AddMinutes(2).ToString("HH:mm:ss") + ".";
        }
        catch (Exception ex) { Result.Text = ex.Message; }
    }
    private void Renew_Click(object sender, RoutedEventArgs e)
    {
        if (HostAddress.SelectedItem is string host) Generate(host);
    }
    private async void Probe_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.IsEnabled = false;
        try { await hub.ProbeAsync(PhoneAddress.Text.Trim()); Result.Text = "Dispositivo encontrado. Ya aparece en el panel. Escanea el QR para emparejar."; }
        catch (Exception ex) { Result.Text = "No se pudo detectar: " + ex.Message; }
        finally { button.IsEnabled = true; }
    }
    private async void UsbScan_Click(object sender, RoutedEventArgs e)
    {
        var adb = UsbConnector.FindAdb();
        if (adb == null) { Result.Text = "No se encontró ADB. Instala Android SDK Platform-Tools o usa anclaje de red USB."; return; }
        try
        {
            var output = await UsbConnector.Run(adb, "devices");
            var serials = output.Split('\n').Select(l => l.Trim().Split('\t')).Where(p => p.Length == 2 && p[1] == "device" && !p[0].Contains(':') && !p[0].StartsWith("emulator-")).Select(p => p[0]).ToArray();
            UsbDevices.ItemsSource = serials;
            if (serials.Length > 0) UsbDevices.SelectedIndex = 0;
            Result.Text = serials.Length > 0 ? "Selecciona el teléfono USB." : "No hay dispositivos USB autorizados. Revisa el cable y acepta la autorización en Android.";
        }
        catch (Exception ex) { Result.Text = ex.Message; }
    }
    private async void UsbConnect_Click(object sender, RoutedEventArgs e)
    {
        var adb = UsbConnector.FindAdb();
        if (adb == null || UsbDevices.SelectedItem is not string serial) { Result.Text = "Detecta y selecciona un teléfono USB primero."; return; }
        try
        {
            Result.Text = await UsbConnector.ConnectAsync(hub, adb, serial);
        }
        catch (Exception ex) { Result.Text = "USB: " + ex.Message; }
    }
    private void OnClosed(object? sender, EventArgs e) => hub.RevokeTicket();
}
