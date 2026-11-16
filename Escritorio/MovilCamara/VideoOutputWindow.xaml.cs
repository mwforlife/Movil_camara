using System.Windows;
using System.Windows.Input;
using MovilCamara.ViewModels;

namespace MovilCamara;
public partial class VideoOutputWindow : Window
{
    public VideoOutputWindow(CameraDeviceViewModel device)
    {
        InitializeComponent();
        DataContext = device;
        Title = "WT MultiCam - " + device.Name + " - " + device.Id[..Math.Min(8, device.Id.Length)];
    }
    private void DragWindow(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else if (WindowState == WindowState.Normal) DragMove();
    }
    private void CloseView(object sender, RoutedEventArgs e) => Close();
    private void ToggleClean(object sender, RoutedEventArgs e) => Toolbar.Visibility = Toolbar.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    private void RotateView(object sender, RoutedEventArgs e)
    {
        if (DataContext is CameraDeviceViewModel camera) camera.RotationOffset = (camera.RotationOffset + 90) % 360;
    }
    private void ResetRotation(object sender, RoutedEventArgs e)
    {
        if (DataContext is CameraDeviceViewModel camera) camera.RotationOffset = 0;
    }
    private void OnKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) Close(); }
}
