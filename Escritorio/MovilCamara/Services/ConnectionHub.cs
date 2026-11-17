using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace MovilCamara.Services;

public record DeviceInfo(string Id, string Name, string Address, DateTime Seen, bool Paired);
public sealed class ConnectionHub : IDisposable
{
    public const int DiscoveryPort = 47777, MetadataPort = 47778, PairPort = 47779;
    private readonly CancellationTokenSource stop = new();
    private readonly ConcurrentDictionary<string, DeviceInfo> devices = new();
    private readonly ConcurrentDictionary<string, string> sessions = new();
    private readonly ConcurrentDictionary<string, (string DeviceId, DateTime Expires, bool Usb)> invitations = new();
    private readonly SemaphoreSlim scanGate = new(1);
    private readonly object ticketLock = new();
    private readonly SemaphoreSlim clients = new(16);
    private readonly ConcurrentDictionary<string, Guid> videoStreams = new();
    private readonly ConcurrentDictionary<string, Guid> audioStreams = new();
    private readonly X509Certificate2 certificate;
    private readonly ReceiverSessionStore? sessionStore;
    private readonly RemoteCameraControls remoteControls = new();
    public AudioMixer AudioMixer { get; } = new();
    private readonly ConcurrentDictionary<string, (CameraControls Controls, DateTime Seen)> controlStates = new();
    public CameraControls? GetCameraControls(string id) => controlStates.TryGetValue(id, out var state) &&
        DateTime.UtcNow - state.Seen < TimeSpan.FromSeconds(10) ? state.Controls : null;
    public Task<CameraCommandResult> SendCameraCommandAsync(string id, string operation, double value, string? expectedCameraId = null)
    {
        var caps = GetCameraControls(id);
        if (!sessions.ContainsKey(id) || caps?.Active != true)
            return Task.FromResult(new CameraCommandResult(false, "Inicia la cámara en Android y espera el estado de controles."));
        if (expectedCameraId != null && caps.CameraId != expectedCameraId)
            return Task.FromResult(new CameraCommandResult(false, "La cámara seleccionada cambió. Ajusta de nuevo el zoom."));
        if (operation == "microphone" && (!caps.AudioControlSupported || value is not (0 or 1) || (value == 1 && !caps.MicrophoneAllowed)))
            return Task.FromResult(new CameraCommandResult(false, "Autoriza Enviar micrófono al iniciar la cámara en Android. Actualiza Android si no informa controles de audio."));
        if ((operation == "zoom" && (value < caps.MinZoom || value > caps.MaxZoom)) ||
            (operation == "torch" && (!caps.TorchSupported || value is not (0 or 1))) ||
            (operation == "exposure" && (value < caps.MinExposure || value > caps.MaxExposure || value != Math.Truncate(value))) ||
            (operation == "autofocus" && !caps.FocusSupported))
            return Task.FromResult(new CameraCommandResult(false, "El ajuste está fuera de las capacidades de esta cámara."));
        return remoteControls.SendAsync(id, caps.CameraId, operation, value, stop.Token);
    }
    private UdpClient? discovery;
    private TcpListener? listener;
    private string ticket = "";
    private DateTime ticketExpiry;
    public event Action? Changed;
    public event Action<string, VideoFrame>? FrameReceived;
    public event Action<string, AudioFrame>? AudioReceived;
    public event Action<string, bool>? AudioStateChanged;
    public event Action<string, bool, string>? VideoStateChanged;
    public event Action<string>? Error;
    public Func<string, string, string, Task<bool>>? ApproveConnection { get; set; }
    public event Action<string>? ScanStatus;
    public string Fingerprint => Convert.ToHexString(SHA256.HashData(certificate.RawData)).ToLowerInvariant();
    public DeviceInfo[] Devices => devices.Values.OrderBy(d => d.Name).ToArray();
    public bool IsAudioActive(string id) => audioStreams.ContainsKey(id);

    public ConnectionHub(bool persistIdentity = false)
    {
        if (persistIdentity && OperatingSystem.IsWindows())
        {
            certificate = ReceiverIdentity.LoadOrCreate();
            sessionStore = new ReceiverSessionStore(certificate, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WilkensTech", "WTMultiCam", "connections.json"));
            LoadSessions();
            return;
        }
        using var key = RSA.Create(2048);
        var req = new CertificateRequest("CN=WT MultiCam", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(30));
        certificate = new X509Certificate2(cert.Export(X509ContentType.Pfx));
    }

    private void LoadSessions()
    {
        if (sessionStore == null) return;
        foreach (var entry in sessionStore.Load()) sessions[entry.Key] = entry.Value;
    }

    private void SaveSessions()
    {
        try { sessionStore?.Save(sessions); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        { Error?.Invoke("No se pudo guardar el vínculo de los dispositivos: " + ex.Message); }
    }
    public void Start()
    {
        listener = new TcpListener(IPAddress.Any, PairPort);
        listener.Start();
        discovery = new UdpClient(new IPEndPoint(IPAddress.Any, 0)) { EnableBroadcast = true };
        _ = Task.Run(AcceptLoop);
        _ = Task.Run(DiscoverLoop);
        _ = Task.Run(ReceiveLoop);
        _ = Task.Run(ScanLoop);
    }

    public static string[] LocalAddresses() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .SelectMany(n => n.GetIPProperties().UnicastAddresses)
        .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
        .Select(a => a.Address.ToString()).Distinct().ToArray();

    public string CreateTicket(string host)
    {
        if (!IPAddress.TryParse(host, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("Selecciona una dirección IPv4 del PC.");
        lock (ticketLock)
        {
            ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            ticketExpiry = DateTime.UtcNow.AddMinutes(2);
            return JsonSerializer.Serialize(new { app = "wtmulticam", v = 1, host, port = PairPort, pin = Fingerprint, token = ticket });
        }
    }
    public void RevokeTicket() { lock (ticketLock) ticket = ""; }

    public async Task ProbeAsync(string address, int port = MetadataPort)
    {
        if (!IPAddress.TryParse(address, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("Escribe solamente una dirección IPv4 válida del teléfono.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(ip, port, timeout.Token);
        using var reader = new StreamReader(tcp.GetStream(), Encoding.UTF8);
        var line = await ReadLine(reader, timeout.Token);
        UpdateMetadata(line, address);
    }

    public static string VerificationCode(string pin, string secret)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(pin + "|" + secret));
        return (System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(hash) % 1000000).ToString("D6");
    }

    public async Task RequestConnectionAsync(string deviceId)
    {
        if (!devices.TryGetValue(deviceId, out var device) || device.Paired)
            throw new InvalidOperationException("El dispositivo ya está emparejado o dejó de estar disponible.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Parse(device.Address), MetadataPort, timeout.Token);
        var host = ((IPEndPoint)tcp.Client.LocalEndPoint!).Address.ToString();
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        invitations[nonce] = (deviceId, DateTime.UtcNow.AddSeconds(90), false);
        using var reader = new StreamReader(tcp.GetStream(), Encoding.UTF8, false, 1024, true);
        using var writer = new StreamWriter(tcp.GetStream(), new UTF8Encoding(false), 1024, true) { AutoFlush = true };
        await ReadLine(reader, timeout.Token); // Initial metadata, compatible with older apps.
        try
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(new { action = "invite", app = "wtmulticam", v = 1,
                host, port = PairPort, pin = Fingerprint, invitation = nonce, pc = Environment.MachineName }));
            using var reply = JsonDocument.Parse(await ReadLine(reader, timeout.Token));
            if (!reply.RootElement.GetProperty("received").GetBoolean())
                throw new IOException("El teléfono no aceptó la solicitud. Actualiza y abre la app Android.");
        }
        catch { invitations.TryRemove(nonce, out _); throw; }
    }

    public async Task<string> PairUsbAsync(int forwardedPort)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, forwardedPort, timeout.Token);
        using var reader = new StreamReader(tcp.GetStream(), Encoding.UTF8, false, 1024, true);
        using var writer = new StreamWriter(tcp.GetStream(), new UTF8Encoding(false), 1024, true) { AutoFlush = true };
        var metadata = await ReadLine(reader, timeout.Token);
        using var json = JsonDocument.Parse(metadata);
        var root = json.RootElement;
        if (root.GetProperty("app").GetString() != "wtmulticam" || root.GetProperty("v").GetInt32() != 1)
            throw new IOException("El servicio USB no es WT MultiCam.");
        var id = root.GetProperty("id").GetString() ?? "";
        if (id.Length is < 8 or > 80) throw new IOException("Identidad USB inválida.");
        UpdateMetadata(metadata, "127.0.0.1");
        if (root.TryGetProperty("usbReceiverPin", out var pin) && pin.GetString() == Fingerprint &&
            devices.TryGetValue(id, out var device) && device.Paired && DateTime.UtcNow - device.Seen < TimeSpan.FromSeconds(12))
            return "Conectado por cable";
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        invitations[nonce] = (id, DateTime.UtcNow.AddSeconds(90), true);
        try
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(new { action = "usbInvite", app = "wtmulticam", v = 1,
                host = "127.0.0.1", port = PairPort, pin = Fingerprint, invitation = nonce }));
            using var reply = JsonDocument.Parse(await ReadLine(reader, timeout.Token));
            if (!reply.RootElement.GetProperty("received").GetBoolean())
                throw new IOException("USB pendiente. Actualiza Android, mantén la app abierta y autoriza este PC si lo solicita.");
            return "Emparejamiento sin QR solicitado; un PC nuevo pide confirmación en Android.";
        }
        catch { invitations.TryRemove(nonce, out _); throw; }
    }

    private async Task ScanLoop()
    {
        while (!stop.IsCancellationRequested)
        {
            try { await ScanNowAsync(); await Task.Delay(20000, stop.Token); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task ScanNowAsync()
    {
        if (!await scanGate.WaitAsync(0, stop.Token)) return;
        try
        {
            var addresses = LocalNetworkScanner.ActiveCandidates();
            ScanStatus?.Invoke("Buscando WT MultiCam en " + addresses.Length + " direcciones de la red local…");
            await Parallel.ForEachAsync(addresses, new ParallelOptions { MaxDegreeOfParallelism = 32, CancellationToken = stop.Token },
                async (ip, cancellation) =>
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    timeout.CancelAfter(800);
                    try
                    {
                        using var tcp = new TcpClient();
                        await tcp.ConnectAsync(ip, MetadataPort, timeout.Token);
                        using var reader = new StreamReader(tcp.GetStream(), Encoding.UTF8);
                        UpdateMetadata(await ReadLine(reader, timeout.Token), ip.ToString());
                    }
                    catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { }
                });
            ScanStatus?.Invoke("Búsqueda finalizada. Se repite automáticamente; deja la app Android abierta.");
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception ex) { Error?.Invoke("Búsqueda LAN: " + ex.Message); }
        finally { scanGate.Release(); }
    }

    private void UpdateMetadata(string line, string address)
    {
        using var json = JsonDocument.Parse(line);
        var r = json.RootElement;
        if (r.GetProperty("app").GetString() != "wtmulticam" || r.GetProperty("v").GetInt32() != 1) return;
        var id = r.GetProperty("id").GetString() ?? "";
        var name = r.GetProperty("name").GetString() ?? "Android";
        if (id.Length is < 8 or > 80 || name.Length > 100) return;
        // Discovery is untrusted: it cannot promote or refresh an authenticated session.
        if (devices.TryGetValue(id, out var prior) && prior.Paired) return;
        devices[id] = new(id, name, address, DateTime.UtcNow, false);
        Changed?.Invoke();
    }

    private async Task DiscoverLoop()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                var targets = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && a.IPv4Mask != null)
                    .Select(a => new IPAddress(a.Address.GetAddressBytes().Zip(a.IPv4Mask.GetAddressBytes(),
                        (b, m) => (byte)(b | ~m)).ToArray())).Append(IPAddress.Broadcast).Distinct();
                var bytes = Encoding.UTF8.GetBytes("WTMULTICAM_DISCOVER_V1");
                foreach (var ip in targets)
                    try { await discovery!.SendAsync(bytes, new IPEndPoint(ip, DiscoveryPort), stop.Token); }
                    catch (SocketException) { }
                foreach (var d in devices.Values)
                    if (DateTime.UtcNow - d.Seen > TimeSpan.FromSeconds(d.Paired ? 16 : 90))
                    {
                        devices.TryRemove(d.Id, out _);
                        Changed?.Invoke();
                    }
                foreach (var invite in invitations)
                    if (invite.Value.Expires < DateTime.UtcNow) invitations.TryRemove(invite.Key, out _);
                await Task.Delay(3000, stop.Token);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Error?.Invoke("Descubrimiento: " + ex.Message); await Task.Delay(3000, stop.Token); }
        }
    }
    private async Task ReceiveLoop()
    {
        while (!stop.IsCancellationRequested)
            try
            {
                var packet = await discovery!.ReceiveAsync(stop.Token);
                if (packet.Buffer.Length <= 2048)
                    UpdateMetadata(Encoding.UTF8.GetString(packet.Buffer), packet.RemoteEndPoint.Address.ToString());
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { }
            catch (Exception ex) { Error?.Invoke("Recepción de descubrimiento: " + ex.Message); }
    }
    private async Task AcceptLoop()
    {
        while (!stop.IsCancellationRequested)
            try
            {
                var client = await listener!.AcceptTcpClientAsync(stop.Token);
                if (!clients.Wait(0)) { client.Dispose(); continue; }
                _ = HandleClient(client);
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException) when (stop.IsCancellationRequested) { break; }
            catch (Exception ex) { Error?.Invoke("Receptor: " + ex.Message); break; }
    }

    private async Task HandleClient(TcpClient client)
    {
        using (client)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(7));
            try
            {
                using var ssl = new SslStream(client.GetStream(), false);
                await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions {
                    ServerCertificate = certificate, EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                }, timeout.Token);
                using var reader = new StreamReader(ssl, Encoding.UTF8, false, 1024, true);
                using var writer = new StreamWriter(ssl, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
                using var json = JsonDocument.Parse(await ReadLine(reader, timeout.Token));
                var r = json.RootElement;
                var id = r.GetProperty("id").GetString() ?? "";
                var name = r.GetProperty("name").GetString() ?? "Android";
                var secret = r.GetProperty("secret").GetString() ?? "";
                var action = r.GetProperty("action").GetString();
                if (id.Length is < 8 or > 80 || name.Length > 100) return;
                bool allowed = false;
                if (action == "requestPair")
                {
                    var nonce = r.GetProperty("invitation").GetString() ?? "";
                    if (secret.Length == 64 && invitations.TryRemove(nonce, out var invitation) &&
                        invitation.DeviceId == id && invitation.Expires > DateTime.UtcNow)
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(60));
                        var addressText = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();
                        if (invitation.Usb && IPAddress.IsLoopback(((IPEndPoint)client.Client.RemoteEndPoint!).Address)) allowed = true;
                        else if (!invitation.Usb && ApproveConnection != null)
                            allowed = await ApproveConnection(name, addressText, VerificationCode(Fingerprint, secret)).WaitAsync(timeout.Token);
                        if (allowed)
                        {
                            secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
                            sessions[id] = secret;
                        }
                    }
                }
                else if (action == "pair")
                {
                    lock (ticketLock)
                    {
                        if (ticket.Length > 0 && ticketExpiry > DateTime.UtcNow && Equal(secret, ticket))
                        {
                            ticket = "";
                            secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
                            sessions[id] = secret;
                            allowed = true;
                        }
                    }
                }
                else if (action is "heartbeat" or "video" or "control" or "audio" && sessions.TryGetValue(id, out var expected))
                    allowed = Equal(secret, expected);
                if (!allowed) { await writer.WriteLineAsync("{\"ok\":false}"); return; }
                if (action == "audio")
                {
                    if (r.GetProperty("audioProtocol").GetInt32() != 1 || r.GetProperty("codec").GetString() != "pcm_s16le" ||
                        r.GetProperty("sampleRate").GetInt32() != 48000 || r.GetProperty("channels").GetInt32() != 1 ||
                        !audioStreams.TryAdd(id, Guid.NewGuid()))
                    { await writer.WriteLineAsync("{\"ok\":false}"); return; }
                    try
                    {
                        client.NoDelay = true;
                        await writer.WriteLineAsync("{\"ok\":true,\"audioProtocol\":1}");
                        AudioStateChanged?.Invoke(id, true);
                        long lastTimestamp = -1;
                        while (!stop.IsCancellationRequested && sessions.TryGetValue(id, out var current) && Equal(secret, current))
                        {
                            timeout.CancelAfter(TimeSpan.FromSeconds(5));
                            var audio = await AudioWireProtocol.ReadAsync(ssl, timeout.Token);
                            if (audio.TimestampUs <= lastTimestamp) throw new InvalidDataException("Reloj de audio no creciente");
                            lastTimestamp = audio.TimestampUs;
                            AudioMixer.Observe(id, audio);
                            AudioReceived?.Invoke(id, audio);
                        }
                    }
                    finally { audioStreams.TryRemove(id, out _); AudioMixer.Clear(id); AudioStateChanged?.Invoke(id, false); }
                    return;
                }
                if (action == "control")
                {
                    if (r.TryGetProperty("ack", out var ack) && ack.ValueKind == JsonValueKind.Object)
                        remoteControls.Acknowledge(id, ack.GetProperty("id").GetString() ?? "", ack.GetProperty("ok").GetBoolean(),
                            ack.TryGetProperty("message", out var message) ? message.GetString() ?? "" : "");
                    if (r.TryGetProperty("controls", out var controls) && controls.ValueKind == JsonValueKind.Object)
                    {
                        var caps = JsonSerializer.Deserialize<CameraControls>(controls.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (caps != null && double.IsFinite(caps.MinZoom) && double.IsFinite(caps.MaxZoom) &&
                            double.IsFinite(caps.Zoom) && caps.MinZoom > 0 && caps.MaxZoom >= caps.MinZoom && caps.MaxZoom <= 200 &&
                            caps.Zoom >= caps.MinZoom && caps.Zoom <= caps.MaxZoom && caps.MinExposure <= caps.MaxExposure)
                        {
                            var changed = !controlStates.TryGetValue(id, out var previous) || previous.Controls != caps;
                            controlStates[id] = (caps, DateTime.UtcNow);
                            if (changed) Changed?.Invoke();
                        }
                    }
                    timeout.CancelAfter(TimeSpan.FromSeconds(5));
                    using var poll = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
                    poll.CancelAfter(TimeSpan.FromSeconds(3));
                    CameraCommand? command = null;
                    try { command = await remoteControls.NextAsync(id, poll.Token); }
                    catch (OperationCanceledException) when (!timeout.IsCancellationRequested) { }
                    // A re-pair invalidates any outstanding poll as well as video credentials.
                    if (!sessions.TryGetValue(id, out var currentSecret) || !Equal(secret, currentSecret))
                    { await writer.WriteLineAsync("{\"ok\":false}"); return; }
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new { ok = true, command = command == null ? null :
                        new { id = command.Id, cameraId = command.CameraId, operation = command.Operation, value = command.Value } }));
                    return;
                }
                if (action == "video")
                {
                    var protocol = r.GetProperty("videoProtocol").GetInt32();
                    var codec = r.GetProperty("codec").GetString();
                    var h264 = protocol == 2 && codec == "h264";
                    if (!h264 && (protocol != 1 || codec != "jpeg"))
                    {
                        await writer.WriteLineAsync("{\"ok\":false}"); return;
                    }
                    var streamId = Guid.NewGuid();
                    if (!videoStreams.TryAdd(id, streamId))
                    {
                        await writer.WriteLineAsync("{\"ok\":false}"); return;
                    }
                    try
                    {
                        H264Decoder? decoder = null;
                        var width = 0; var height = 0;
                        if (h264)
                        {
                            width = r.GetProperty("width").GetInt32(); height = r.GetProperty("height").GetInt32();
                            try { decoder = new H264Decoder(width, height, r.GetProperty("fps").GetInt32()); }
                            catch (Exception ex)
                            {
                                await writer.WriteLineAsync(JsonSerializer.Serialize(new { ok = false, error = "Decodificador Windows H.264 no disponible: " + ex.Message + ". Selecciona JPEG para compatibilidad." }));
                                VideoStateChanged?.Invoke(id, false, "H.264 no disponible: " + ex.Message);
                                return;
                            }
                        }
                        using var decoderScope = decoder;
                        client.NoDelay = true;
                        await writer.WriteLineAsync(JsonSerializer.Serialize(new { ok = true, videoProtocol = protocol }));
                        VideoStateChanged?.Invoke(id, true, "Esperando imagen del teléfono…");
                        var videoAddress = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();
                        var notified = false;
                        while (!stop.IsCancellationRequested && sessions.TryGetValue(id, out var sessionSecret) && Equal(secret, sessionSecret))
                        {
                            timeout.CancelAfter(TimeSpan.FromSeconds(8));
                            VideoFrame frame;
                            if (h264)
                            {
                                var packet = await VideoWireProtocol.ReadH264Async(ssl, timeout.Token);
                                var pixels = decoder!.Decode(packet.Data, packet.TimestampUs);
                                if (pixels == null) continue;
                                frame = new VideoFrame(Array.Empty<byte>(), packet.Rotation, packet.Mirrored, DateTime.UtcNow, pixels, width, height);
                            }
                            else frame = await VideoWireProtocol.ReadFrameAsync(ssl, timeout.Token);
                            devices[id] = new(id, name, videoAddress, DateTime.UtcNow, true);
                            FrameReceived?.Invoke(id, frame);
                            if (!notified) { Changed?.Invoke(); notified = true; }
                        }
                    }
                    finally
                    {
                        videoStreams.TryRemove(id, out _);
                        VideoStateChanged?.Invoke(id, false, "Video detenido o conexión interrumpida");
                    }
                    return;
                }
                var address = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();
                devices[id] = new(id, name, address, DateTime.UtcNow, true);
                if (action is "pair" or "requestPair") SaveSessions();
                await writer.WriteLineAsync(JsonSerializer.Serialize(new { ok = true, secret }));
                Changed?.Invoke();
            }
            catch (Exception ex) when (ex is IOException or SocketException or AuthenticationException or OperationCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { }
            finally { clients.Release(); }
        }
    }
    private static bool Equal(string a, string b) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
    private static async Task<string> ReadLine(StreamReader reader, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var ch = new char[1];
        while (sb.Length < 4096)
        {
            if (await reader.ReadAsync(ch.AsMemory(), ct) == 0) throw new IOException("Conexión cerrada.");
            if (ch[0] == '\n') return sb.ToString().TrimEnd('\r');
            sb.Append(ch[0]);
        }
        throw new IOException("Mensaje demasiado largo.");
    }
    public void Dispose()
    {
        stop.Cancel();
        listener?.Stop();
        discovery?.Dispose();
        // Active handshakes finish through the cancellation token before process exit.
    }
}
