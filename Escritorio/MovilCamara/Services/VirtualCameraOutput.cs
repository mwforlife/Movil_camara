using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace MovilCamara.Services;

public sealed record VirtualCameraConfig(string DeviceId, Guid ClassId, string Name);

/// <summary>One stable Windows camera per phone. Audio remains available through the A/V OBS link.</summary>
public sealed class VirtualCameraOutput : IDisposable
{
    private readonly Dictionary<string, Output> outputs = new();
    private readonly Dictionary<string, VirtualCameraConfig> configured = new();
    private readonly SemaphoreSlim changes = new(1);
    private readonly string settingsPath;
    private bool disposed;
    public event Action<string, string>? Status;

    public VirtualCameraOutput()
    {
        settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WilkensTech", "WTMultiCam", "virtual-cameras.json");
        if (!File.Exists(settingsPath)) return;
        try
        {
            foreach (var c in JsonSerializer.Deserialize<VirtualCameraConfig[]>(File.ReadAllText(settingsPath)) ?? [])
                if (!string.IsNullOrWhiteSpace(c.DeviceId) && c.ClassId == Identity(c.DeviceId)) configured[c.DeviceId] = c;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
    }

    public static Guid Identity(string deviceId)
    {
        // The source registration is user-specific even when two Windows accounts pair the same phone.
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("No se pudo obtener el usuario Windows.");
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("WTMultiCam.VirtualCamera.v1:" + sid + ":" + deviceId)).AsSpan(0, 16));
    }
    public bool IsActive(string id) { lock (outputs) return outputs.ContainsKey(id); }
    public bool IsConfigured(string id) => configured.ContainsKey(id);
    public VirtualCameraConfig[] Configurations => configured.Values.ToArray();
    public async Task RestoreAsync()
    {
        foreach (var config in configured.Values.ToArray())
        {
            try { await EnableAsync(config.DeviceId, config.Name, allowInstall: false); }
            catch (Exception ex) { Status?.Invoke(config.DeviceId, "Cámara virtual pendiente: " + ex.Message); }
        }
    }
    public async Task EnableAsync(string id, string name, bool allowInstall = true)
    {
        await changes.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (IsActive(id)) return;
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) || !Environment.Is64BitProcess)
                throw new NotSupportedException("La cámara virtual requiere Windows 11 y receptor de 64 bits.");
            var config = configured.GetValueOrDefault(id) ?? new VirtualCameraConfig(id, Identity(id),
                "WT MultiCam — " + (name.StartsWith("WT MultiCam — ", StringComparison.Ordinal) ? name[14..] : name));
            var nativeFolder = Path.Combine(AppContext.BaseDirectory, "Native");
            var dllPath = Path.Combine(nativeFolder, Native.Dll);
            if (!File.Exists(dllPath)) throw new FileNotFoundException("Recompila Windows para generar el proveedor nativo de cámara virtual.");
            var sid = WindowsIdentity.GetCurrent().User!.Value;
            var expectedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "WilkensTech", "WTMultiCam", "VirtualCamera", "Frames", sid, config.ClassId.ToString("D") + ".bin");
            bool registered = Registered(config.ClassId, sid, dllPath, expectedPath);
            if (!registered)
            {
                if (!allowInstall) throw new InvalidOperationException("Pulsa Activar cámara virtual para registrar el proveedor.");
                Status?.Invoke(id, "Registrando proveedor; revisa la autorización de Windows…");
                var script = Path.Combine(nativeFolder, "Install-Provider.ps1");
                var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
                start.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" -CameraId " + config.ClassId.ToString("D") + " -UserSid " + sid;
                using var installer = Process.Start(start) ?? throw new IOException("No se pudo iniciar el registro del proveedor.");
                await installer.WaitForExitAsync();
                if (installer.ExitCode != 0 || !Registered(config.ClassId, sid, dllPath, expectedPath))
                    throw new IOException("No se pudo registrar el proveedor. Comprueba los permisos del instalador.");
            }
            var output = await Task.Run(() => new Output(config, expectedPath, message => Status?.Invoke(id, message)));
            if (disposed) { output.Dispose(); return; }
            lock (outputs) outputs.Add(id, output);
            bool existed = configured.ContainsKey(id);
            configured[id] = config;
            try { Save(); }
            catch
            {
                lock (outputs) outputs.Remove(id);
                if (!existed) configured.Remove(id);
                await Task.Run(output.Remove);
                throw;
            }
            Status?.Invoke(id, "Cámara virtual activa · 1080p30 · " + config.Name);
        }
        finally { changes.Release(); }
    }
    private static bool Registered(Guid clsid, string sid, string dllPath, string framePath)
    {
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = machine.OpenSubKey("SOFTWARE\\Classes\\CLSID\\" + clsid.ToString("B"));
        using var source = File.OpenRead(dllPath);
        var hash = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
        using var server = key?.OpenSubKey("InprocServer32");
        return key?.GetValue("WTMultiCamSource") is int marker && marker == 1 &&
            key.GetValue("OwnerSid") as string == sid && key.GetValue("ProviderHash") as string == hash &&
            string.Equals(key.GetValue("FramePath") as string, framePath, StringComparison.OrdinalIgnoreCase) &&
            server?.GetValue(null) is string installed && File.Exists(installed);
    }
    public async Task DisableAsync(string id)
    {
        await changes.WaitAsync();
        try
        {
            Output? output; lock (outputs) outputs.Remove(id, out output);
            if (output != null) await Task.Run(() => output.Remove());
            else if (configured.TryGetValue(id, out var config))
                await Task.Run(() => Marshal.ThrowExceptionForHR(Native.RemoveCamera(config.ClassId.ToString("B"), config.Name)));
            configured.Remove(id); Save();
            Status?.Invoke(id, "Cámara virtual desactivada");
        }
        finally { changes.Release(); }
    }
    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        var temporary = settingsPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(configured.Values.ToArray(), new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, settingsPath, true);
    }
    public void Offer(string id, BitmapSource bitmap)
    {
        lock (outputs) { if (outputs.TryGetValue(id, out var output)) output.Offer(bitmap); }
    }
    public void Clear(string id) { lock (outputs) { if (outputs.TryGetValue(id, out var output)) output.Clear(); } }
    public void Dispose()
    {
        disposed = true;
        Output[] all; lock (outputs) { all = outputs.Values.ToArray(); outputs.Clear(); }
        foreach (var output in all) output.Dispose();
    }

    private sealed class Output : IDisposable
    {
        private readonly object gate = new();
        private readonly AutoResetEvent signal = new(false);
        private readonly Thread worker;
        private readonly Action<string> status;
        private BitmapSource? pending;
        private IntPtr publisher, camera;
        private bool stopped, clear;
        public Output(VirtualCameraConfig config, string path, Action<string> status)
        {
            this.status = status;
            Marshal.ThrowExceptionForHR(Native.OpenPublisher(path, out publisher));
            try
            {
                int hr = Native.StartCamera(config.ClassId.ToString("B"), config.Name, out camera, out var stage);
                if (hr < 0)
                {
                    string operation = stage switch { 1 => "preparar COM", 2 => "preparar Media Foundation",
                        3 => "crear el dispositivo virtual", 4 => "iniciar el dispositivo en Windows", _ => "activar la cámara virtual" };
                    throw new COMException($"Error al {operation} (0x{hr:X8}): " +
                        Marshal.GetExceptionForHR(hr, new IntPtr(-1))?.Message, hr);
                }
            }
            catch { Native.ClosePublisher(publisher); publisher = IntPtr.Zero; throw; }
            worker = new Thread(Run) { IsBackground = true, Name = "WT virtual camera publisher" };
            worker.SetApartmentState(ApartmentState.MTA); worker.Start();
        }
        public void Offer(BitmapSource bitmap)
        {
            if (!bitmap.IsFrozen) throw new ArgumentException("La imagen compartida debe estar congelada.");
            lock (gate) { if (stopped) return; pending = bitmap; clear = false; signal.Set(); }
        }
        public void Clear() { lock (gate) { if (stopped) return; pending = null; clear = true; signal.Set(); } }
        private void Run()
        {
            byte[] bytes = [];
            var lastError = DateTime.MinValue;
            while (true)
            {
                signal.WaitOne();
                BitmapSource? image; bool erase;
                lock (gate) { if (stopped) return; image = pending; pending = null; erase = clear; clear = false; }
                try
                {
                    if (erase) { Native.Clear(publisher); continue; }
                    if (image == null) continue;
                    if (image.Format != PixelFormats.Bgr32) { image = new FormatConvertedBitmap(image, PixelFormats.Bgr32, null, 0); image.Freeze(); }
                    int stride = checked(image.PixelWidth * 4), length = checked(stride * image.PixelHeight);
                    if (bytes.Length != length) bytes = new byte[length];
                    image.CopyPixels(bytes, stride, 0);
                    int hr = Native.Publish(publisher, bytes, image.PixelWidth, image.PixelHeight, stride);
                    // Reader may hold the shared file for a frame copy. Skip, never accumulate old video.
                    if (hr != unchecked((int)0x80070021)) Marshal.ThrowExceptionForHR(hr);
                }
                catch (Exception ex)
                {
                    if (DateTime.UtcNow - lastError > TimeSpan.FromSeconds(5))
                    { lastError = DateTime.UtcNow; status("Error de salida virtual: " + ex.Message); }
                }
            }
        }
        public void Remove() { Stop(true); }
        public void Dispose() { Stop(false); }
        private void Stop(bool remove)
        {
            lock (gate) { if (stopped) return; stopped = true; pending = null; signal.Set(); }
            worker.Join(); signal.Dispose();
            Native.ClosePublisher(publisher); publisher = IntPtr.Zero;
            // Camera handles belong to the process MTA; closing WPF runs on an STA thread.
            var handle = camera;
            int hr = Task.Run(() => Native.StopCamera(handle, remove)).GetAwaiter().GetResult(); camera = IntPtr.Zero;
            if (remove) Marshal.ThrowExceptionForHR(hr);
        }
    }
    private static class Native
    {
        internal const string Dll = "WTMultiCam.VirtualCamera.dll";
        static Native()
        {
            NativeLibrary.SetDllImportResolver(typeof(Native).Assembly, (name, _, _) => name == Dll
                ? NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "Native", Dll)) : IntPtr.Zero);
        }
        [DllImport(Dll, EntryPoint = "WT_StartCameraEx", CharSet = CharSet.Unicode)] internal static extern int StartCamera(string clsid, string name, out IntPtr camera, out uint stage);
        [DllImport(Dll, EntryPoint = "WT_StopCamera")] internal static extern int StopCamera(IntPtr camera, [MarshalAs(UnmanagedType.Bool)] bool remove);
        [DllImport(Dll, EntryPoint = "WT_RemoveCamera", CharSet = CharSet.Unicode)] internal static extern int RemoveCamera(string clsid, string name);
        [DllImport(Dll, EntryPoint = "WT_OpenPublisher", CharSet = CharSet.Unicode)] internal static extern int OpenPublisher(string path, out IntPtr publisher);
        [DllImport(Dll, EntryPoint = "WT_Publish")] internal static extern int Publish(IntPtr publisher, byte[] bytes, int width, int height, int stride);
        [DllImport(Dll, EntryPoint = "WT_Clear")] internal static extern void Clear(IntPtr publisher);
        [DllImport(Dll, EntryPoint = "WT_ClosePublisher")] internal static extern void ClosePublisher(IntPtr publisher);
    }
}
