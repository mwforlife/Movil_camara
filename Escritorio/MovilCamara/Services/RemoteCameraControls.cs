using System.Collections.Concurrent;
using System.Threading.Channels;

namespace MovilCamara.Services;

public sealed record CameraControls(string CameraId, bool Active, double MinZoom, double MaxZoom, double Zoom,
    bool TorchSupported, bool Torch, int MinExposure, int MaxExposure, int Exposure, bool FocusSupported);
public sealed record CameraCommand(string Id, string CameraId, string Operation, double Value, DateTime Expires);
public sealed record CameraCommandResult(bool Ok, string Message);

public sealed class RemoteCameraControls
{
    private sealed record Pending(string DeviceId, CameraCommand Command, TaskCompletionSource<CameraCommandResult> Completion);
    private readonly ConcurrentDictionary<string, Channel<CameraCommand>> queues = new();
    private readonly ConcurrentDictionary<string, Pending> pending = new();
    private Channel<CameraCommand> Queue(string deviceId) => queues.GetOrAdd(deviceId, _ => Channel.CreateBounded<CameraCommand>(8));
    public async Task<CameraCommandResult> SendAsync(string deviceId, string cameraId, string operation, double value, CancellationToken ct = default)
    {
        if (operation is not ("zoom" or "torch" or "exposure" or "autofocus") || !double.IsFinite(value))
            throw new ArgumentException("Control no permitido");
        var command = new CameraCommand(Guid.NewGuid().ToString("N"), cameraId, operation, value, DateTime.UtcNow.AddSeconds(12));
        var completion = new TaskCompletionSource<CameraCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[command.Id] = new(deviceId, command, completion);
        try
        {
            if (!Queue(deviceId).Writer.TryWrite(command)) return new(false, "La cola de controles está ocupada");
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(12), ct);
        }
        catch (TimeoutException) { return new(false, "El teléfono no confirmó el ajuste. Revisa su conexión y cámara."); }
        finally { pending.TryRemove(command.Id, out _); }
    }
    public async Task<CameraCommand?> NextAsync(string deviceId, CancellationToken ct)
    {
        var reader = Queue(deviceId).Reader;
        while (await reader.WaitToReadAsync(ct))
            while (reader.TryRead(out var command))
                if (command.Expires > DateTime.UtcNow && pending.ContainsKey(command.Id)) return command;
        return null;
    }
    public bool Acknowledge(string deviceId, string commandId, bool ok, string message)
    {
        if (!pending.TryGetValue(commandId, out var item) || item.DeviceId != deviceId) return false;
        if (!pending.TryRemove(commandId, out _)) return false;
        return item.Completion.TrySetResult(new(ok, message.Length > 200 ? message[..200] : message));
    }
}
