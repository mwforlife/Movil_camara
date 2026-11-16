namespace MovilCamara.Services;

public sealed class UsbAutoConnector : IDisposable
{
    private readonly ConnectionHub hub;
    private readonly CancellationTokenSource stop = new();
    private readonly Dictionary<string, DateTime> nextAttempt = new();
    public event Action<string>? Status;
    public UsbAutoConnector(ConnectionHub hub) => this.hub = hub;

    public void Start() => _ = Task.Run(Loop);
    private async Task Loop()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                var adb = UsbConnector.FindAdb();
                if (adb == null) Status?.Invoke("USB automático: instala Android SDK Platform-Tools para usar ADB.");
                else
                {
                    var listing = await UsbConnector.Run(adb, "devices");
                    var serials = UsbConnector.AuthorizedSerials(listing);
                    foreach (var serial in nextAttempt.Keys.Except(serials).ToArray()) nextAttempt.Remove(serial);
                    if (serials.Length == 0) Status?.Invoke("USB: esperando un teléfono con depuración autorizada.");
                    foreach (var serial in serials)
                    {
                        if (stop.IsCancellationRequested) break;
                        if (nextAttempt.TryGetValue(serial, out var after) && after > DateTime.UtcNow) continue;
                        try
                        {
                            var result = await UsbConnector.ConnectAsync(hub, adb, serial);
                            Status?.Invoke("USB · " + serial + ": " + result);
                            nextAttempt[serial] = DateTime.UtcNow.AddSeconds(result.StartsWith("Conectado") ? 5 : 20);
                        }
                        catch (Exception ex)
                        {
                            Status?.Invoke("USB · " + serial + ": abre WT MultiCam en el teléfono. " + ex.Message);
                            nextAttempt[serial] = DateTime.UtcNow.AddSeconds(10);
                        }
                    }
                }
                await Task.Delay(3000, stop.Token);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Status?.Invoke("USB: " + ex.Message);
                try { await Task.Delay(10000, stop.Token); } catch (OperationCanceledException) { break; }
            }
        }
    }
    public void Dispose() => stop.Cancel();
}
