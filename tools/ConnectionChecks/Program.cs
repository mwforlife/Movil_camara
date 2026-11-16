using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MovilCamara.Services;

static void Check(bool result, string name)
{
    if (!result) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}
using var hub = new ConnectionHub();
if (args.Contains("--obs-output"))
{
    var keyPath = Path.Combine(Path.GetTempPath(), "wt-obs-live-check-" + Guid.NewGuid() + ".key");
    try
    {
        var imageA = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "frame.jpg"));
        var imageB = imageA.Take(2).Concat(new byte[] { 255, 254, 0, 3, 66 }).Concat(imageA.Skip(2)).ToArray();
        using var output = new ObsDirectServer(f => f.Jpeg, imageA, keyPath);
        output.Start();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        using var a = await http.GetAsync(output.GetUrl("phone-test-a").Replace("&format=matroska", ""), HttpCompletionOption.ResponseHeadersRead);
        using var b = await http.GetAsync(output.GetUrl("phone-test-b").Replace("&format=matroska", ""), HttpCompletionOption.ResponseHeadersRead);
        Check(a.IsSuccessStatusCode && b.IsSuccessStatusCode, "dos fuentes OBS HTTP simultáneas");
        using var streamA = await a.Content.ReadAsStreamAsync();
        using var streamB = await b.Content.ReadAsStreamAsync();
        async Task<byte[]> ReadPart(Stream stream)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var header = new List<byte>(); var one = new byte[1];
            while (header.Count < 4096)
            {
                if (await stream.ReadAsync(one, deadline.Token) == 0) throw new IOException("Salida OBS cerrada");
                header.Add(one[0]);
                if (header.Count >= 4 && header[^4] == 13 && header[^3] == 10 && header[^2] == 13 && header[^1] == 10) break;
            }
            var lengthLine = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n").Single(l => l.StartsWith("Content-Length:"));
            var length = int.Parse(lengthLine.Split(':')[1]);
            if (length is < 4 or > 2097152) throw new IOException("Longitud OBS inválida");
            var image = new byte[length];
            await stream.ReadExactlyAsync(image, deadline.Token);
            await stream.ReadExactlyAsync(new byte[2], deadline.Token);
            return image;
        }
        output.Offer("phone-test-a", new(imageA, 0, false, DateTime.UtcNow));
        output.Offer("phone-test-b", new(imageB, 0, false, DateTime.UtcNow));
        var foundB = false;
        for (var i = 0; i < 20 && !foundB; i++) foundB = (await ReadPart(streamB)).SequenceEqual(imageB);
        Check(foundB && (await ReadPart(streamA)).SequenceEqual(imageA), "fuentes HTTP no mezclan cámaras");
        output.Clear("phone-test-b");
        var cleared = false;
        for (var i = 0; i < 20 && !cleared; i++) cleared = (await ReadPart(streamB)).SequenceEqual(imageA);
        Check(cleared, "detener una cámara sustituye el cuadro retenido");
        using var denied = await http.GetAsync($"http://127.0.0.1:{ObsDirectServer.Port}/camera/phone-test-a?key=invalid");
        Check(denied.StatusCode == HttpStatusCode.Forbidden, "salida HTTP rechaza claves incorrectas");
    }
    finally { if (File.Exists(keyPath)) File.Delete(keyPath); }
    return;
}
if (args.Contains("--offline"))
{
    var pcmPacket = new byte[12 + AudioWireProtocol.PacketBytes];
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(pcmPacket.AsSpan(0, 4), AudioWireProtocol.PacketBytes);
    System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(pcmPacket.AsSpan(4, 8), 123456);
    pcmPacket[12] = 42;
    var pcmRead = await AudioWireProtocol.ReadAsync(new MemoryStream(pcmPacket), CancellationToken.None);
    Check(pcmRead.Pcm.Length == 1920 && pcmRead.Pcm[0] == 42 && pcmRead.TimestampUs == 123456, "audio PCM conserva muestras y timestamp");
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(pcmPacket.AsSpan(0, 4), 1000000);
    try { await AudioWireProtocol.ReadAsync(new MemoryStream(pcmPacket), CancellationToken.None); throw new Exception("Audio excesivo aceptado"); }
    catch (InvalidDataException) { Console.WriteLine("PASS: rechazar tamaño de audio inválido antes de reservar memoria"); }
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(pcmPacket.AsSpan(0, 4), AudioWireProtocol.PacketBytes);
    try { await AudioWireProtocol.ReadAsync(new MemoryStream(pcmPacket[..20]), CancellationToken.None); throw new Exception("Audio truncado aceptado"); }
    catch (EndOfStreamException) { Console.WriteLine("PASS: rechazar paquete de audio truncado"); }
    var latestZoom = new LatestValueDispatcher<Tuple<string, double>>();
    var valuesSent = new List<double>();
    var releaseZoom = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    latestZoom.Offer(Tuple.Create("camera-a", 1.0));
    var firstZoom = latestZoom.FlushAsync(async value => { valuesSent.Add(value.Item2); await releaseZoom.Task; });
    latestZoom.Offer(Tuple.Create("camera-a", 2.0));
    latestZoom.Offer(Tuple.Create("camera-a", 3.0));
    await latestZoom.FlushAsync(value => { valuesSent.Add(value.Item2); return Task.CompletedTask; });
    Check(valuesSent.SequenceEqual(new[] { 1.0 }), "zoom continuo no envía órdenes en paralelo");
    releaseZoom.SetResult(); await firstZoom;
    await latestZoom.FlushAsync(value => { valuesSent.Add(value.Item2); return Task.CompletedTask; });
    Check(valuesSent.SequenceEqual(new[] { 1.0, 3.0 }), "zoom continuo sustituye posiciones intermedias por la última");
    latestZoom.Offer(Tuple.Create("camera-a", 4.0)); latestZoom.Clear();
    await latestZoom.FlushAsync(value => { valuesSent.Add(value.Item2); return Task.CompletedTask; });
    Check(valuesSent.Count == 2 && !latestZoom.IsBusy, "cambiar cámara descarta el zoom pendiente");
    latestZoom.Offer(Tuple.Create("camera-a", 5.0));
    try { await latestZoom.FlushAsync(_ => throw new IOException("desconectado")); }
    catch (IOException) { }
    Check(!latestZoom.IsBusy, "fallo de envío libera el control de zoom");
    var remote = new RemoteCameraControls();
    using var remoteDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    var adjustA = remote.SendAsync("device-a", "rear-0", "zoom", 2, remoteDeadline.Token);
    var commandA = await remote.NextAsync("device-a", remoteDeadline.Token);
    Check(commandA?.CameraId == "rear-0" && commandA.Operation == "zoom" && commandA.Value == 2,
        "control remoto conserva cámara, operación y valor");
    Check(!remote.Acknowledge("device-b", commandA!.Id, true, ""), "otra cámara no puede confirmar un control ajeno");
    Check(!adjustA.IsCompleted, "un control no se marca aplicado antes de confirmación Android");
    Check(remote.Acknowledge("device-a", commandA.Id, true, "aplicado") && (await adjustA).Ok,
        "control termina al recibir confirmación del teléfono correcto");
    var rejected = remote.SendAsync("device-b", "front-1", "torch", 1, remoteDeadline.Token);
    var commandB = await remote.NextAsync("device-b", remoteDeadline.Token);
    remote.Acknowledge("device-b", commandB!.Id, false, "No disponible");
    Check(!(await rejected).Ok, "rechazo del hardware se conserva como fallo");
    try { await remote.SendAsync("device-a", "rear-0", "zoom", double.NaN); throw new Exception("NaN aceptado"); }
    catch (ArgumentException) { Console.WriteLine("PASS: rechazar valores de control no finitos"); }
    var outputKeyPath = Path.Combine(Path.GetTempPath(), "wt-obs-check-" + Guid.NewGuid() + ".key");
    try
    {
        using var direct = new ObsDirectServer(f => f.Jpeg, new byte[] { 255, 216, 255, 217 }, outputKeyPath);
        var cameraA = new Uri(direct.GetUrl("phone-test-a"));
        var cameraB = new Uri(direct.GetUrl("phone-test-b"));
        Check(cameraA.Host == "127.0.0.1" && direct.Authorize(cameraA.PathAndQuery, out var selected) && selected == "phone-test-a", "salida OBS local autentica la cámara seleccionada");
        Check(!direct.Authorize(cameraA.AbsolutePath + cameraB.Query, out _), "el enlace de otra cámara no autoriza esta fuente");
        Check(!direct.Authorize("/camera/phone-test-a?key=invalid", out _) && !direct.Authorize("/camera/../secret?key=" + new string('a', 64), out _), "rechazar rutas y claves OBS inválidas");
        using var reopened = new ObsDirectServer(f => f.Jpeg, Array.Empty<byte>(), outputKeyPath);
        Check(reopened.GetUrl("phone-test-a") == cameraA.AbsoluteUri, "enlace OBS estable al reiniciar el receptor");
        using var multipart = new MemoryStream();
        var multipartImage = new byte[] { 255, 216, 255, 217 };
        await ObsDirectServer.WritePart(multipart, multipartImage, CancellationToken.None);
        var expectedHeader = Encoding.ASCII.GetBytes("--wtframe\r\nContent-Type: image/jpeg\r\nContent-Length: 4\r\n\r\n");
        Check(multipart.ToArray().SequenceEqual(expectedHeader.Concat(multipartImage).Concat("\r\n"u8.ToArray())), "MJPEG conserva límite, longitud y bytes del cuadro");
    }
    finally { if (File.Exists(outputKeyPath)) File.Delete(outputKeyPath); }
    var statePath = Path.Combine(Path.GetTempPath(), "wt-session-check-" + Guid.NewGuid() + ".json");
    using var stateKey = RSA.Create(2048);
    using var stateIdentity = new System.Security.Cryptography.X509Certificates.CertificateRequest(
        "CN=WT Test", stateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
        .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
    try
    {
        var saved = new Dictionary<string, string> { ["device-test-1"] = new string('a', 64), ["device-test-2"] = new string('b', 64) };
        new ReceiverSessionStore(stateIdentity, statePath).Save(saved);
        Check(!File.ReadAllText(statePath).Contains(saved["device-test-1"]), "credenciales persistidas cifradas");
        var restored = new ReceiverSessionStore(stateIdentity, statePath).Load();
        Check(restored.Count == 2 && restored.All(p => saved[p.Key] == p.Value), "recuperar dos vínculos tras recrear el almacén");
        using var otherKey = RSA.Create(2048);
        using var otherIdentity = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=Other PC", otherKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        Check(new ReceiverSessionStore(otherIdentity, statePath).Load().Count == 0, "otra identidad de PC no recupera los vínculos");
        File.WriteAllText(statePath, "{invalid");
        Check(new ReceiverSessionStore(stateIdentity, statePath).Load().Count == 0, "archivo corrupto no autoriza dispositivos");
    }
    finally { if (File.Exists(statePath)) File.Delete(statePath); }
    var avc = new byte[24];
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(avc.AsSpan(0, 4), 4);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(avc.AsSpan(4, 4), 270);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(avc.AsSpan(8, 4), 3);
    System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(avc.AsSpan(12, 8), 123456);
    avc[23] = 1;
    var h264 = await VideoWireProtocol.ReadH264Async(new MemoryStream(avc), CancellationToken.None);
    Check(h264.KeyFrame && h264.Mirrored && h264.Rotation == 270 && h264.TimestampUs == 123456 && h264.Data.Length == 4, "paquete H.264 conserva timestamp, keyframe, espejo y orientación");
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(avc.AsSpan(0, 4), 2097153);
    try { await VideoWireProtocol.ReadH264Async(new MemoryStream(avc), CancellationToken.None); throw new Exception("H264 gigante aceptado"); }
    catch (InvalidDataException) { Console.WriteLine("PASS: rechazar paquetes H.264 excesivos antes de reservar payload"); }
    var serials = UsbConnector.AuthorizedSerials("List of devices attached\nUSB123\tdevice\nUSB_DENIED\tunauthorized\n192.168.1.10:5555\tdevice\nemulator-5554\tdevice\nUSB_OFF\toffline\nUSB456\tdevice\n");
    Check(serials.SequenceEqual(new[] { "USB123", "USB456" }), "USB automático usa solamente dispositivos físicos ADB autorizados");
    var jpeg = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "frame.jpg"));
    Check(VideoWireProtocol.ValidateJpeg(jpeg) == (8, 4), "leer dimensiones del JPEG de prueba");
    static byte[] Header(int size, int rotation, int flags)
    {
        var data = new byte[12];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(0, 4), size);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4, 4), rotation);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(8, 4), flags);
        return data;
    }
    using (var videoBytes = new MemoryStream(Header(jpeg.Length, 90, 1).Concat(jpeg).ToArray()))
    {
        var frame = await VideoWireProtocol.ReadFrameAsync(videoBytes, CancellationToken.None);
        Check(frame.Rotation == 90 && frame.Mirrored && frame.Jpeg.SequenceEqual(jpeg), "frame conserva imagen, rotación y espejo");
    }
    foreach (var invalid in new[] { Header(VideoWireProtocol.MaxFrameBytes + 1, 0, 0), Header(500, 45, 0), Header(500, 0, 2), Header(-1, 0, 0) })
    {
        try { await VideoWireProtocol.ReadFrameAsync(new MemoryStream(invalid), CancellationToken.None); throw new Exception("Frame inválido aceptado"); }
        catch (InvalidDataException) { }
    }
    Console.WriteLine("PASS: rechazar tamaño, rotación o flags de video inválidos antes de reservar payload");
    try { await VideoWireProtocol.ReadFrameAsync(new MemoryStream(Header(jpeg.Length, 0, 0).Concat(jpeg[..10]).ToArray()), CancellationToken.None); throw new Exception("Frame truncado aceptado"); }
    catch (EndOfStreamException) { Console.WriteLine("PASS: rechazar frame truncado"); }
    try { VideoWireProtocol.ValidateJpeg(new byte[] { 255,216,255,192,0,8,8,32,0,32,0,3,255,217 }); throw new Exception("JPEG gigante aceptado"); }
    catch (InvalidDataException) { Console.WriteLine("PASS: rechazar dimensiones JPEG fuera del perfil"); }
    var smallSubnet = LocalNetworkScanner.Candidates(IPAddress.Parse("192.168.10.1"), IPAddress.Parse("255.255.255.252")).Select(ip => ip.ToString()).ToArray();
    Check(smallSubnet.SequenceEqual(new[] { "192.168.10.2" }), "respetar máscara /30 sin buscar red, broadcast ni IP propia");
    var largeSubnet = LocalNetworkScanner.Candidates(IPAddress.Parse("10.20.30.40"), IPAddress.Parse("255.255.0.0")).Select(ip => ip.ToString()).ToArray();
    Check(largeSubnet.Length == 253 && largeSubnet.All(ip => ip.StartsWith("10.20.30.")) && !largeSubnet.Contains("10.20.30.40"), "limitar redes grandes al bloque /24 local");
    Check(!LocalNetworkScanner.Candidates(IPAddress.Parse("192.168.10.1"), IPAddress.Parse("255.255.255.255")).Any(), "no escanear una ruta /32");
    Check(ConnectionHub.VerificationCode(new string('a', 64), new string('b', 64)).Length == 6 &&
        ConnectionHub.VerificationCode(new string('a', 64), new string('b', 64)) != ConnectionHub.VerificationCode(new string('c', 64), new string('b', 64)), "código de comparación vinculado al certificado y secreto del teléfono");
    using var firstQr = JsonDocument.Parse(hub.CreateTicket("127.0.0.1"));
    using var secondQr = JsonDocument.Parse(hub.CreateTicket("127.0.0.1"));
    Check(firstQr.RootElement.GetProperty("app").GetString() == "wtmulticam", "identificador del protocolo QR");
    Check(firstQr.RootElement.GetProperty("pin").GetString() == hub.Fingerprint && hub.Fingerprint.Length == 64, "QR contiene la huella SHA-256 del receptor");
    Check(firstQr.RootElement.GetProperty("token").GetString() != secondQr.RootElement.GetProperty("token").GetString(), "cada QR recibe una credencial aleatoria distinta");
    try { hub.CreateTicket("http://invalid-host"); throw new Exception("Dirección inválida aceptada"); }
    catch (ArgumentException) { Console.WriteLine("PASS: rechazar dirección QR inválida"); }
    Console.WriteLine("Comprobaciones sin sockets completadas; no prueban transporte ni autenticación.");
    return;
}
hub.Start();
async Task<JsonElement> Request(string id, string action, string secret, string? pin = null, string? invitation = null)
{
    using var timeout = new CancellationTokenSource(5000);
    using var client = new TcpClient();
    await client.ConnectAsync(IPAddress.Loopback, ConnectionHub.PairPort, timeout.Token);
    using var ssl = new SslStream(client.GetStream(), false, (_, cert, _, _) =>
        cert != null && Convert.ToHexString(SHA256.HashData(cert.GetRawCertData())).ToLowerInvariant() == (pin ?? hub.Fingerprint));
    await ssl.AuthenticateAsClientAsync("WT MultiCam");
    using var writer = new StreamWriter(ssl, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
    using var reader = new StreamReader(ssl, Encoding.UTF8, false, 1024, true);
    await writer.WriteLineAsync(JsonSerializer.Serialize(new { id, name = id, action, secret, invitation }));
    using var json = JsonDocument.Parse((await reader.ReadLineAsync(timeout.Token))!);
    return json.RootElement.Clone();
}
string Ticket()
{
    using var json = JsonDocument.Parse(hub.CreateTicket("127.0.0.1"));
    return json.RootElement.GetProperty("token").GetString()!;
}
var token = Ticket();
Check(!(await Request("android-01", "pair", "incorrect")).GetProperty("ok").GetBoolean(), "rechazar token incorrecto");
var first = await Request("android-01", "pair", token);
Check(first.GetProperty("ok").GetBoolean(), "emparejar primer dispositivo por TLS");
Check(!(await Request("android-02", "pair", token)).GetProperty("ok").GetBoolean(), "QR de un solo uso");
var second = await Request("android-02", "pair", Ticket());
Check(second.GetProperty("ok").GetBoolean() && hub.Devices.Count(d => d.Paired) == 2, "dos dispositivos independientes");
Check((await Request("android-01", "heartbeat", first.GetProperty("secret").GetString()!)).GetProperty("ok").GetBoolean(), "heartbeat autenticado");
Check(!(await Request("android-01", "heartbeat", "incorrect")).GetProperty("ok").GetBoolean(), "rechazar heartbeat falso");
var revoked = Ticket();
hub.RevokeTicket();
Check(!(await Request("android-03", "pair", revoked)).GetProperty("ok").GetBoolean(), "QR revocado al cerrar diálogo");
var obsolete = Ticket();
Ticket();
Check(!(await Request("android-03", "pair", obsolete)).GetProperty("ok").GetBoolean(), "renovar QR invalida el anterior");
try { await Request("android-03", "heartbeat", "invalid", new string('0', 64)); throw new Exception("Pin falso aceptado"); }
catch (System.Security.Authentication.AuthenticationException) { Console.WriteLine("PASS: rechazar certificado distinto al QR"); }
var metadata = new TcpListener(IPAddress.Loopback, 0);
metadata.Start();
var port = ((IPEndPoint)metadata.LocalEndpoint).Port;
var sender = Task.Run(async () => {
 using var c = await metadata.AcceptTcpClientAsync();
 using var w = new StreamWriter(c.GetStream()) { AutoFlush = true };
 await w.WriteLineAsync("{\"app\":\"wtmulticam\",\"v\":1,\"id\":\"manual-01\",\"name\":\"Android manual\"}");
});
await hub.ProbeAsync("127.0.0.1", port);
await sender;
metadata.Stop();
Check(hub.Devices.Any(d => d.Id == "manual-01" && !d.Paired), "detección por IP sin autorización implícita");
async Task<string> InvitePhone()
{
    var phone = new TcpListener(IPAddress.Loopback, ConnectionHub.MetadataPort);
    phone.Start();
    try
    {
        var fakeAndroid = Task.Run(async () =>
        {
            string nonce = "";
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var c = await phone.AcceptTcpClientAsync();
                using var w = new StreamWriter(c.GetStream(), new UTF8Encoding(false), 1024, true) { AutoFlush = true };
                using var r = new StreamReader(c.GetStream(), Encoding.UTF8, false, 1024, true);
                await w.WriteLineAsync("{\"app\":\"wtmulticam\",\"v\":1,\"id\":\"invite-01\",\"name\":\"Android invitado\"}");
                var line = await r.ReadLineAsync();
                if (line != null)
                {
                    using var request = JsonDocument.Parse(line);
                    Check(!request.RootElement.TryGetProperty("token", out _) && !request.RootElement.TryGetProperty("secret", out _), "invitación LAN no transporta credencial de sesión ni token QR");
                    nonce = request.RootElement.GetProperty("invitation").GetString()!;
                    await w.WriteLineAsync("{\"received\":true}");
                }
            }
            return nonce;
        });
        await hub.ProbeAsync("127.0.0.1");
        await hub.RequestConnectionAsync("invite-01");
        return await fakeAndroid;
    }
    finally { phone.Stop(); }
}
hub.ApproveConnection = (_, _, _) => Task.FromResult(false);
var invitationId = await InvitePhone();
Check(!(await Request("invite-01", "requestPair", new string('d', 64), invitation: invitationId)).GetProperty("ok").GetBoolean(), "rechazo en PC impide autorizar teléfono");
hub.ApproveConnection = (_, _, code) =>
{
    Check(code == ConnectionHub.VerificationCode(hub.Fingerprint, new string('d', 64)), "PC recibe el código de comparación correcto");
    return Task.FromResult(true);
};
invitationId = await InvitePhone();
Check((await Request("invite-01", "requestPair", new string('d', 64), invitation: invitationId)).GetProperty("ok").GetBoolean(), "solicitud aceptada en ambos equipos autoriza sesión");
Check(!(await Request("invite-01", "requestPair", new string('d', 64), invitation: invitationId)).GetProperty("ok").GetBoolean(), "invitación consumida no se puede reutilizar");
Check(!(await Request("android-01", "video", "incorrect")).GetProperty("ok").GetBoolean(), "rechazar video sin credencial de sesión válida");
async Task SendVideoFrame(string id, string secret)
{
    var received = new TaskCompletionSource<VideoFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
    void OnFrame(string deviceId, VideoFrame frame) { if (deviceId == id) received.TrySetResult(frame); }
    hub.FrameReceived += OnFrame;
    try
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, ConnectionHub.PairPort);
        using var ssl = new SslStream(tcp.GetStream(), false, (_, cert, _, _) => cert != null && Convert.ToHexString(SHA256.HashData(cert.GetRawCertData())).ToLowerInvariant() == hub.Fingerprint);
        await ssl.AuthenticateAsClientAsync("WT MultiCam");
        using var writer = new StreamWriter(ssl, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
        using var reader = new StreamReader(ssl, Encoding.UTF8, false, 1024, true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(new { id, name = id, secret, action = "video", videoProtocol = 1, codec = "jpeg" }));
        using var reply = JsonDocument.Parse((await reader.ReadLineAsync())!);
        Check(reply.RootElement.GetProperty("ok").GetBoolean(), "video autenticado: " + id);
        var jpeg = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "frame.jpg"));
        var header = new byte[12];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), jpeg.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), 90);
        await ssl.WriteAsync(header); await ssl.WriteAsync(jpeg); await ssl.FlushAsync();
        var frame = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Check(frame.Jpeg.SequenceEqual(jpeg) && frame.Rotation == 90, "imagen recibida sin mezcla entre dispositivos: " + id);
    }
    finally { hub.FrameReceived -= OnFrame; }
}
await Task.WhenAll(SendVideoFrame("android-01", first.GetProperty("secret").GetString()!), SendVideoFrame("android-02", second.GetProperty("secret").GetString()!));
Console.WriteLine("Todas las comprobaciones completadas.");
