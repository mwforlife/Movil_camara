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
if (args.Contains("--offline"))
{
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
