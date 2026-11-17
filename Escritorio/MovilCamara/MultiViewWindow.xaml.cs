using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using MovilCamara.Services;
using MovilCamara.ViewModels;

namespace MovilCamara;

public partial class MultiViewWindow : Window
{
    public static readonly DependencyProperty TileWidthProperty = DependencyProperty.Register(
        nameof(TileWidth), typeof(double), typeof(MultiViewWindow), new PropertyMetadata(180.0));
    public double TileWidth { get => (double)GetValue(TileWidthProperty); set => SetValue(TileWidthProperty, value); }
    public static readonly DependencyProperty TileHeightProperty = DependencyProperty.Register(
        nameof(TileHeight), typeof(double), typeof(MultiViewWindow), new PropertyMetadata(150.0));
    public double TileHeight { get => (double)GetValue(TileHeightProperty); set => SetValue(TileHeightProperty, value); }
    private readonly ConnectionHub hub;
    private readonly ListCollectionView cameras;
    private readonly ObservableCollection<CameraDeviceViewModel> devices;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer zoomTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private readonly DispatcherTimer audioTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private bool syncingAudio;
    private sealed record ZoomUpdate(string DeviceId, string Name, string CameraId, long Selection, double Value);
    private readonly LatestValueDispatcher<ZoomUpdate> liveZoom = new();
    private long selectionVersion;
    private bool syncingControls;
    private bool zoomDirty;
    private DateTime lastZoomInput = DateTime.MinValue;
    private CameraControls? lastControls;
    private string? lastId;
    private bool busy;
    private bool closed;
    public MultiViewWindow(ObservableCollection<CameraDeviceViewModel> devices, ConnectionHub connection)
    {
        InitializeComponent();
        hub = connection;
        this.devices = devices;
        cameras = new ListCollectionView(devices) { Filter = item => item is CameraDeviceViewModel camera && camera.HasBeenPaired };
        CameraTiles.ItemsSource = cameras;
        hub.Changed += DevicesChanged;
        timer.Tick += (_, _) => RefreshControls();
        timer.Start();
        zoomTimer.Tick += async (_, _) => await FlushZoom();
        zoomTimer.Start();
        audioTimer.Tick += (_, _) => RefreshAudio();
        audioTimer.Start();
        Loaded += (_, _) => RefreshCameras();
        Closed += (_, _) => { closed = true; selectionVersion++; liveZoom.Clear(); audioTimer.Stop(); zoomTimer.Stop(); timer.Stop(); hub.Changed -= DevicesChanged; };
    }
    private void DevicesChanged() => Dispatcher.BeginInvoke(() => { if (!closed) RefreshCameras(); });
    private void RefreshCameras()
    {
        var selectedId = (CameraTiles.SelectedItem as CameraDeviceViewModel)?.Id;
        var expected = devices.Where(c => c.HasBeenPaired).Select(c => c.Id).ToHashSet();
        if (!expected.SetEquals(cameras.Cast<CameraDeviceViewModel>().Select(c => c.Id))) cameras.Refresh();
        if (CameraTiles.SelectedItem == null)
            CameraTiles.SelectedItem = cameras.Cast<CameraDeviceViewModel>().FirstOrDefault(c => c.Id == selectedId)
                ?? cameras.Cast<CameraDeviceViewModel>().FirstOrDefault();
        CameraCount.Text = $"Cámaras vinculadas · {cameras.Count}";
        UpdateTileHeight();
        RefreshControls();
    }
    private void TileAreaChanged(object sender, SizeChangedEventArgs e) => UpdateTileHeight();
    private void UpdateTileHeight()
    {
        if (CameraTiles == null || CameraTiles.ActualHeight <= 0) return;
        TileHeight = Math.Clamp(CameraTiles.ActualHeight - 38, 40, 130);
        TileWidth = Math.Clamp((TileHeight - 32) * 16 / 9 + 18, 140, 220);
    }
    private void CameraSelected(object sender, SelectionChangedEventArgs e)
    { if (hub != null) { selectionVersion++; liveZoom.Clear(); zoomDirty = false; lastZoomInput = DateTime.MinValue; lastId = null; lastControls = null; RefreshControls(); } }
    private void RefreshControls()
    {
        if (closed) return;
        var camera = CameraTiles.SelectedItem as CameraDeviceViewModel;
        var caps = camera == null ? null : hub.GetCameraControls(camera.Id);
        ControlsPanel.IsEnabled = !busy && camera?.IsPaired == true && caps?.Active == true;
        RemoteStatus.Text = camera == null ? "No hay cámaras vinculadas" : caps == null ? "Esperando controles de Android actualizado" :
            caps.Active && camera.IsPaired ? "Conectado · ajustes disponibles" : "Cámara detenida o desconectada";
        RefreshAudio();
        if (caps == null) return;
        if (lastId == camera!.Id && lastControls != null && lastControls.CameraId != caps.CameraId)
        { selectionVersion++; liveZoom.Clear(); zoomDirty = false; lastZoomInput = DateTime.MinValue; }
        ZoomReset.IsEnabled = ZoomSlider.IsEnabled = caps.MaxZoom > caps.MinZoom;
        ExposureSlider.IsEnabled = caps.MaxExposure > caps.MinExposure;
        ExposureApply.IsEnabled = !liveZoom.IsBusy && caps.MaxExposure > caps.MinExposure;
        FocusButton.IsEnabled = !liveZoom.IsBusy && caps.FocusSupported;
        TorchButton.IsEnabled = !liveZoom.IsBusy && caps.TorchSupported;
        TorchButton.Content = caps.Torch ? "Apagar linterna" : "Encender linterna";
        var canReconcileZoom = !liveZoom.IsBusy && !ZoomSlider.IsMouseCaptureWithin && DateTime.UtcNow - lastZoomInput > TimeSpan.FromMilliseconds(350);
        if (!busy && (lastId != camera!.Id || lastControls != caps || (zoomDirty && canReconcileZoom)))
        {
            syncingControls = true;
            try
            {
                var changedCamera = lastId != camera.Id || lastControls?.CameraId != caps.CameraId;
                if (changedCamera || canReconcileZoom)
                {
                    ZoomSlider.Minimum = .01; ZoomSlider.Maximum = caps.MaxZoom; ZoomSlider.Minimum = caps.MinZoom;
                    ZoomSlider.Value = caps.Zoom;
                    zoomDirty = false;
                }
                if (changedCamera || !ExposureSlider.IsMouseCaptureWithin)
                {
                    ExposureSlider.Minimum = Math.Min(0, caps.MinExposure); ExposureSlider.Maximum = caps.MaxExposure;
                    ExposureSlider.Minimum = caps.MinExposure; ExposureSlider.Value = caps.Exposure;
                }
                lastId = camera.Id; lastControls = caps;
            }
            finally { syncingControls = false; }
        }
    }
    private async Task Send(string operation, double value)
    {
        if (busy || liveZoom.IsBusy || CameraTiles.SelectedItem is not CameraDeviceViewModel camera) return;
        busy = true; RefreshControls();
        CommandStatus.Text = $"Enviando ajuste a {camera.Name}…";
        try
        {
            var result = await hub.SendCameraCommandAsync(camera.Id, operation, value, hub.GetCameraControls(camera.Id)?.CameraId);
            if (!closed) CommandStatus.Text = result.Ok ? $"{camera.Name}: ajuste confirmado por Android." : $"{camera.Name}: {result.Message}";
        }
        catch (Exception ex) when (ex is OperationCanceledException or ArgumentException)
        { if (!closed) CommandStatus.Text = "Ajuste cancelado: " + ex.Message; }
        finally { busy = false; if (!closed) RefreshControls(); }
    }
    private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (syncingControls || closed || busy || hub == null || CameraTiles?.SelectedItem is not CameraDeviceViewModel camera || !camera.IsPaired) return;
        var caps = hub.GetCameraControls(camera.Id);
        if (caps?.Active != true || caps.MaxZoom <= caps.MinZoom) return;
        var value = Math.Clamp(Math.Round(e.NewValue, 2), caps.MinZoom, caps.MaxZoom);
        lastZoomInput = DateTime.UtcNow; zoomDirty = true;
        liveZoom.Offer(new ZoomUpdate(camera.Id, camera.Name, caps.CameraId, selectionVersion, value));
    }
    private async Task FlushZoom()
    {
        if (closed || busy) return;
        await liveZoom.FlushAsync(async update =>
        {
            if (closed || update.Selection != selectionVersion || CameraTiles.SelectedItem is not CameraDeviceViewModel selected || selected.Id != update.DeviceId) return;
            try
            {
                CommandStatus.Text = $"{update.Name}: zoom {update.Value:F2}×…";
                var result = await hub.SendCameraCommandAsync(update.DeviceId, "zoom", update.Value, update.CameraId);
                if (!closed && update.Selection == selectionVersion)
                {
                    if (!result.Ok) liveZoom.Clear();
                    CommandStatus.Text = result.Ok ? $"{update.Name}: zoom confirmado por Android." : $"{update.Name}: {result.Message}";
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or ArgumentException)
            { if (!closed && update.Selection == selectionVersion) { liveZoom.Clear(); CommandStatus.Text = "Zoom interrumpido: " + ex.Message; } }
        });
        if (!closed) RefreshControls();
    }
    private void ResetZoom(object sender, RoutedEventArgs e) => ZoomSlider.Value = Math.Clamp(1, ZoomSlider.Minimum, ZoomSlider.Maximum);
    private async void ApplyExposure(object sender, RoutedEventArgs e) => await Send("exposure", Math.Round(ExposureSlider.Value));
    private async void AutoFocus(object sender, RoutedEventArgs e) => await Send("autofocus", 0);
    private async void ToggleTorch(object sender, RoutedEventArgs e)
    {
        if (CameraTiles.SelectedItem is CameraDeviceViewModel camera && hub.GetCameraControls(camera.Id) is { } caps)
            await Send("torch", caps.Torch ? 0 : 1);
    }
    private void RefreshAudio()
    {
        if (closed || AudioPanel == null) return;
        var camera = CameraTiles.SelectedItem as CameraDeviceViewModel;
        AudioPanel.IsEnabled = camera != null;
        if (camera == null)
        { AudioMeter.Value = 0; AudioLevelLabel.Text = "Sin señal · nivel de salida"; MicrophoneState.Text = "Selecciona un teléfono"; return; }
        var mix = hub.AudioMixer.Get(camera.Id);
        var caps = hub.GetCameraControls(camera.Id);
        syncingAudio = true;
        try { AudioVolume.Value = mix.Volume * 100; AudioMute.IsChecked = mix.Muted; }
        finally { syncingAudio = false; }
        AudioVolumeLabel.Text = $"Volumen de salida: {mix.Volume * 100:F0} %";
        AudioMeter.Value = double.IsNegativeInfinity(mix.OutputDb) ? 0 : Math.Clamp((mix.OutputDb + 60) / 60 * 100, 0, 100);
        AudioLevelLabel.Text = (mix.Muted ? "Silenciado" : mix.Fresh ?
            (double.IsNegativeInfinity(mix.OutputDb) ? "−∞ dBFS" : $"{mix.OutputDb:F1} dBFS") : "Sin muestras recientes") + " · nivel de salida";
        MicrophoneState.Text = caps?.MicrophoneActive == true ? "Captura activa en Android" :
            hub.IsAudioActive(camera.Id) ? "Audio conectado" : "Micrófono detenido o sin conexión";
        MicrophoneToggle.Content = caps?.MicrophoneActive == true ? "Detener micrófono" : "Activar micrófono";
        MicrophoneToggle.IsEnabled = !busy && !liveZoom.IsBusy && camera.IsPaired && caps?.Active == true &&
            caps.AudioControlSupported && (caps.MicrophoneActive || caps.MicrophoneAllowed);
        AudioHelp.Text = caps?.AudioControlSupported != true ? "Actualiza Android para activar o detener el micrófono desde el PC. Volumen y silencio controlan la salida, sin detener la captura." :
            !caps.MicrophoneAllowed ? "Para autorizar el micrófono, marca Enviar micrófono en Android y vuelve a iniciar la cámara. Silenciar no detiene la captura." :
            "Volumen y silencio afectan al audio de este teléfono en OBS. Detener micrófono cierra la captura en Android. El medidor muestra el pico de salida antes del volumen de OBS.";
    }
    private void AudioVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (syncingAudio || hub == null || CameraTiles?.SelectedItem is not CameraDeviceViewModel camera) return;
        hub.AudioMixer.SetVolume(camera.Id, Math.Clamp(e.NewValue / 100, 0, 1)); RefreshAudio();
    }
    private void AudioMuteChanged(object sender, RoutedEventArgs e)
    {
        if (syncingAudio || hub == null || CameraTiles?.SelectedItem is not CameraDeviceViewModel camera) return;
        hub.AudioMixer.SetMuted(camera.Id, AudioMute.IsChecked == true); RefreshAudio();
    }
    private async void ToggleMicrophone(object sender, RoutedEventArgs e)
    {
        if (CameraTiles.SelectedItem is CameraDeviceViewModel camera && hub.GetCameraControls(camera.Id) is { } caps)
            await Send("microphone", caps.MicrophoneActive ? 0 : 1);
    }
}
