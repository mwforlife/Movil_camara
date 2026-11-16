using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MovilCamara;
using MovilCamara.Services;
using MovilCamara.ViewModels;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        using var hub = new ConnectionHub();
        var devices = new ObservableCollection<CameraDeviceViewModel>();
        for (var i = 1; i <= 5; i++)
        {
            var camera = new CameraDeviceViewModel("mock-camera-" + i) { Name = "Cámara de prueba " + i, IsPaired = true, Status = "Emparejada · simulación" };
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawRectangle(new LinearGradientBrush(Color.FromRgb((byte)(20 * i), 28, 70), Color.FromRgb(10, (byte)(30 * i), 95), 45), null, new Rect(0, 0, 640, 360));
                drawing.DrawEllipse(new SolidColorBrush(Color.FromArgb(100, 80, 170, 220)), null, new Point(320, 180), 100 + i * 10, 100);
                drawing.DrawText(new FormattedText("Imagen sintética " + i, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 32, Brushes.White, 1), new Point(45, 45));
            }
            var image = new RenderTargetBitmap(640, 360, 96, 96, PixelFormats.Pbgra32); image.Render(visual); image.Freeze();
            camera.Display(image, DateTime.UtcNow); camera.VideoStatus = "Imagen de prueba · sin teléfono";
            devices.Add(camera);
        }
        devices.Add(new CameraDeviceViewModel("unpaired-test") { Name = "Detectada sin emparejar" });
        var window = new MultiViewWindow(devices, hub);
        var root = (FrameworkElement)window.Content;
        window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        app.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        var tiles = (ListBox)window.FindName("CameraTiles");
        if (tiles.Items.Count != 5 || tiles.SelectedItem == null) throw new Exception("El multiview no muestra las cinco cámaras");
        if (((Slider)window.FindName("ZoomSlider")).IsEnabled) throw new Exception("Se habilitó zoom sin capacidades Android");
        if (window.FindName("ZoomApply") != null) throw new Exception("El zoom todavía requiere botón Aplicar");
        tiles.SelectedItem = devices[2];
        window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        if (!ReferenceEquals(tiles.SelectedItem, devices[2])) throw new Exception("Se perdió la cámara seleccionada al actualizar");
        var output = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "wt-multiview-preview");
        Directory.CreateDirectory(output);
        foreach (var cameraTotal in new[] { 5, 2 })
        {
        if (cameraTotal == 2)
        {
            foreach (var extra in devices.Where(c => c.HasBeenPaired).Skip(2).ToArray()) devices.Remove(extra);
            window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            app.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        }
        foreach (var (width, height) in new[] { (1400, 900), (1000, 720) })
        {
            root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
            app.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            var monitor = (Border)window.FindName("MainMonitor");
            if (monitor.ActualHeight < tiles.ActualHeight * 3.9 || monitor.ActualHeight > tiles.ActualHeight * 4.1)
                throw new Exception("No se respeta la proporción 80/20 del multiview");
            if (window.TileWidth > 220 || window.TileHeight + 18 > tiles.ActualHeight)
                throw new Exception("Las miniaturas ocupan demasiado espacio");
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var path = Path.Combine(output, $"multiview-{cameraTotal}-{width}.png");
            using var stream = File.Create(path); encoder.Save(stream);
            Console.WriteLine(path);
        }
        }
        Console.WriteLine("PASS: cinco cámaras, selección persistente y controles deshabilitados sin capacidades.");
        window.Close(); app.Shutdown();
    }
}
