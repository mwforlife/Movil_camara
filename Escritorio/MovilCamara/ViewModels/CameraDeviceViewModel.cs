using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace MovilCamara.ViewModels;

public sealed class CameraDeviceViewModel : INotifyPropertyChanged
{
    public string Id { get; }
    private string virtualCameraStatus = "Cámara virtual desactivada";
    private bool virtualCameraActive;
    public string VirtualCameraStatus { get => virtualCameraStatus; set { virtualCameraStatus = value; Changed(); } }
    public bool VirtualCameraActive { get => virtualCameraActive; set { virtualCameraActive = value; Changed(); Changed(nameof(VirtualCameraAction)); } }
    public string VirtualCameraAction => virtualCameraActive ? "Desactivar cámara virtual" : "Activar cámara virtual";
    private string name = "", address = "", status = "", videoStatus = "Empareja por QR para enviar video";
    private bool paired;
    private string audioStatus = "Sin audio recibido";
    public string AudioStatus { get => audioStatus; set { if (audioStatus == value) return; audioStatus = value; Changed(); } }
    public bool HasBeenPaired { get; private set; }
    public int RotationOffset { get; set; }
    private BitmapSource? source;
    private DateTime lastFrame = DateTime.MinValue;
    private DateTime sampleStart = DateTime.UtcNow;
    private int sampleCount;
    public CameraDeviceViewModel(string id) => Id = id;
    public string Name { get => name; set { name = value; Changed(); } }
    public string Address { get => address; set { address = value; Changed(); } }
    public string Status { get => status; set { status = value; Changed(); } }
    public string VideoStatus { get => videoStatus; set { videoStatus = value; Changed(); Changed(nameof(WaitingMessage)); } }
    public bool IsPaired
    {
        get => paired;
        set
        {
            if (paired == value) return;
            paired = value;
            if (paired) { HasBeenPaired = true; Changed(nameof(HasBeenPaired)); }
            Clear(paired ? "Pulsa Iniciar cámara y enviar al PC en Android" : "Empareja por QR para enviar video");
            Changed(); Changed(nameof(CanConnect)); Changed(nameof(WaitingMessage));
        }
    }
    public string WaitingMessage => paired
        ? "Sin imagen todavía.\n\nEn Android pulsa Iniciar cámara y enviar al PC.\n" + VideoStatus
        : "Fuente en espera.\n\nEl teléfono está detectado, pero no está emparejado.\nEn el panel pulsa Emparejar por QR y escanea desde Android.\nDespués inicia la cámara en el teléfono.";
    public bool CanConnect => !paired;
    public BitmapSource? Source { get => source; private set { source = value; Changed(); Changed(nameof(HasFrame)); } }
    public bool HasFrame => source != null;
    public DateTime LastFrame => lastFrame;
    public void Display(BitmapSource frame, DateTime received)
    {
        if (lastFrame == DateTime.MinValue)
        {
            sampleStart = DateTime.UtcNow; sampleCount = 0;
            VideoStatus = frame.PixelWidth + " × " + frame.PixelHeight + " · recibiendo video";
        }
        Source = frame;
        lastFrame = received;
        sampleCount++;
        var elapsed = (DateTime.UtcNow - sampleStart).TotalSeconds;
        if (elapsed >= 1)
        {
            VideoStatus = frame.PixelWidth + " × " + frame.PixelHeight + " · " + Math.Round(sampleCount / elapsed) + " FPS en pantalla";
            sampleStart = DateTime.UtcNow; sampleCount = 0;
        }
    }
    public void Clear(string message)
    {
        Source = null; lastFrame = DateTime.MinValue;
        VideoStatus = message; sampleCount = 0; sampleStart = DateTime.UtcNow;
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
}
