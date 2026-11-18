using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace MovilCamara.Services;

/// <summary>Authenticated loopback feeds: legacy MJPEG, Matroska A/V and independent PCM audio.</summary>
public sealed class ObsDirectServer : IDisposable
{
    public const int Port = 47780;
    private readonly TcpListener listener = new(IPAddress.Loopback, Port);
    private readonly CancellationTokenSource stop = new();
    private readonly SemaphoreSlim clients = new(16);
    private readonly ConcurrentDictionary<string, Feed> feeds = new();
    private readonly Func<VideoFrame, byte[]> encode;
    private readonly byte[] placeholder;
    private readonly byte[] key;
    private readonly AudioMixer? mixer;
    private readonly SemaphoreSlim encodeWake = new(0, 1);
    private sealed class Feed
    {
        public readonly object Gate = new();
        public int Generation;
        public VideoFrame? Pending;
        public Encoded? Current;
        public int VideoReaders, AudioReaders;
        public long AudioSequence;
        public int AudioGeneration;
        public readonly Queue<(long Sequence, AudioFrame Frame)> Audio = new();
    }
    private sealed record Encoded(byte[] Jpeg, DateTime Received);
    public event Action<string>? Error;
    public ObsDirectServer(Func<VideoFrame, byte[]> encoder, byte[] noSignal, string keyFile, AudioMixer? audioMixer = null)
    {
        mixer = audioMixer;
        encode = encoder; placeholder = noSignal;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(keyFile))!);
        if (File.Exists(keyFile))
        {
            key = File.ReadAllBytes(keyFile);
            if (key.Length != 32) throw new IOException("Clave de salida OBS inválida. Revisa la configuración local.");
        }
        else
        {
            key = RandomNumberGenerator.GetBytes(32);
            using var file = new FileStream(keyFile, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            file.Write(key);
        }
    }
    public void Start()
    {
        listener.Start();
        _ = AcceptLoop();
        _ = Task.Run(EncodeLoop);
    }
    public string GetUrl(string id)
    {
        if (!ValidId(id)) throw new ArgumentException("Identificador de cámara inválido");
        feeds.GetOrAdd(id, _ => new Feed());
        return $"http://127.0.0.1:{Port}/camera/{id}?key={Token(id)}&format=matroska";
    }
    public string GetAudioUrl(string id)
    {
        if (!ValidId(id)) throw new ArgumentException("Identificador de cámara inválido");
        feeds.GetOrAdd(id, _ => new Feed());
        return $"http://127.0.0.1:{Port}/audio/{id}?key={Token(id, true)}&format=matroska";
    }
    private static bool ValidId(string id) => id.Length is >= 8 and <= 80 && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
    private string Token(string id, bool audioOnly = false) => Convert.ToHexString(HMACSHA256.HashData(key,
        Encoding.UTF8.GetBytes(audioOnly ? "audio:" + id : id))).ToLowerInvariant();
    internal bool Authorize(string target, out string id) => Authorize(target, out id, out _);
    internal bool Authorize(string target, out string id, out bool audioOnly)
    {
        id = ""; audioOnly = false;
        var parts = target.Split('?');
        if (parts.Length != 2 || !parts[1].StartsWith("key=", StringComparison.Ordinal)) return false;
        var audioRoute = parts[0].StartsWith("/audio/", StringComparison.Ordinal);
        if (!audioRoute && !parts[0].StartsWith("/camera/", StringComparison.Ordinal)) return false;
        var candidate = parts[0][(audioRoute ? 7 : 8)..];
        var fields = parts[1].Split('&');
        if (fields.Length > 2 || (fields.Length == 2 && fields[1] != "format=matroska")) return false;
        if (audioRoute && fields.Length != 2) return false;
        var token = fields[0][4..];
        if (!ValidId(candidate) || token.Length != 64) return false;
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(token), Encoding.ASCII.GetBytes(Token(candidate, audioRoute)))) return false;
        id = candidate; audioOnly = audioRoute;
        return true;
    }
    public void Offer(string id, VideoFrame frame)
    {
        if (feeds.TryGetValue(id, out var feed) && Volatile.Read(ref feed.VideoReaders) > 0)
        {
            Interlocked.Exchange(ref feed.Pending, frame);
            if (encodeWake.CurrentCount == 0) try { encodeWake.Release(); } catch (SemaphoreFullException) { }
        }
    }
    public void OfferAudio(string id, AudioFrame frame)
    {
        if (!feeds.TryGetValue(id, out var feed) || Volatile.Read(ref feed.AudioReaders) == 0) return;
        lock (feed.Gate)
        {
            feed.Audio.Enqueue((++feed.AudioSequence, frame));
            while (feed.Audio.Count > 5) feed.Audio.Dequeue();
        }
    }
    public void ClearAudio(string id)
    {
        if (feeds.TryGetValue(id, out var feed)) lock (feed.Gate) { feed.AudioGeneration++; feed.Audio.Clear(); }
    }
    public void Clear(string id)
    {
        if (!feeds.TryGetValue(id, out var feed)) return;
        lock (feed.Gate)
        {
            feed.Generation++;
            Interlocked.Exchange(ref feed.Pending, null);
            Volatile.Write(ref feed.Current, null);
        }
    }
    private async Task EncodeLoop()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                await encodeWake.WaitAsync(stop.Token);
                foreach (var feed in feeds.Values)
                {
                    VideoFrame? frame;
                    int generation;
                    lock (feed.Gate) { generation = feed.Generation; frame = Interlocked.Exchange(ref feed.Pending, null); }
                    if (frame == null || Volatile.Read(ref feed.VideoReaders) == 0) continue;
                    try
                    {
                        var encoded = new Encoded(encode(frame), frame.Received);
                        lock (feed.Gate) { if (generation == feed.Generation) Volatile.Write(ref feed.Current, encoded); }
                    }
                    catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or System.Runtime.InteropServices.COMException)
                    { Volatile.Write(ref feed.Current, null); Error?.Invoke("No se pudo preparar un cuadro para OBS"); }
                }
            }
        }
        catch (OperationCanceledException) { }
    }
    private async Task AcceptLoop()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stop.Token);
                if (!clients.Wait(0)) { client.Dispose(); continue; }
                _ = Serve(client);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException) { }
    }
    internal static async Task WritePart(Stream stream, byte[] jpeg, CancellationToken ct)
    {
        var header = Encoding.ASCII.GetBytes($"--wtframe\r\nContent-Type: image/jpeg\r\nContent-Length: {jpeg.Length}\r\n\r\n");
        await stream.WriteAsync(header, ct);
        await stream.WriteAsync(jpeg, ct);
        await stream.WriteAsync("\r\n"u8.ToArray(), ct);
    }
    private async Task Serve(TcpClient client)
    {
        Feed? feed = null;
        bool readVideo = false, readAudio = false;
        using (client)
        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
        {
            try
            {
                client.NoDelay = true;
                client.SendBufferSize = 65536;
                var stream = client.GetStream();
                deadline.CancelAfter(TimeSpan.FromSeconds(3));
                var bytes = new List<byte>();
                var one = new byte[1];
                while (bytes.Count < 4096)
                {
                    if (await stream.ReadAsync(one, deadline.Token) == 0) return;
                    bytes.Add(one[0]);
                    if (bytes.Count >= 4 && bytes[^4] == 13 && bytes[^3] == 10 && bytes[^2] == 13 && bytes[^1] == 10) break;
                }
                var request = Encoding.ASCII.GetString(bytes.ToArray()).Split("\r\n")[0].Split(' ');
                if (bytes.Count >= 4096 || request.Length != 3 || request[0] != "GET" ||
                    !Authorize(request[1], out var id, out var audioOnly))
                {
                    await stream.WriteAsync("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray(), deadline.Token);
                    return;
                }
                feed = feeds.GetOrAdd(id, _ => new Feed());
                var matroska = request[1].EndsWith("&format=matroska", StringComparison.Ordinal);
                readVideo = !audioOnly; readAudio = matroska;
                if (readVideo) Interlocked.Increment(ref feed.VideoReaders);
                if (readAudio) Interlocked.Increment(ref feed.AudioReaders);
                if (matroska)
                {
                    await ServeMatroska(stream, feed, deadline, audioOnly, id);
                    return;
                }
                await stream.WriteAsync("HTTP/1.1 200 OK\r\nContent-Type: multipart/x-mixed-replace; boundary=wtframe\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n"u8.ToArray(), deadline.Token);
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / 30));
                do
                {
                    deadline.CancelAfter(TimeSpan.FromSeconds(3));
                    var current = Volatile.Read(ref feed.Current);
                    var jpeg = current != null && DateTime.UtcNow - current.Received < TimeSpan.FromSeconds(3) ? current.Jpeg : placeholder;
                    await WritePart(stream, jpeg, deadline.Token);
                } while (await timer.WaitForNextTickAsync(stop.Token));
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
            finally
            {
                if (feed != null)
                {
                    if (readVideo && Interlocked.Decrement(ref feed.VideoReaders) == 0)
                        lock (feed.Gate) { feed.Generation++; Interlocked.Exchange(ref feed.Pending, null); Volatile.Write(ref feed.Current, null); }
                    if (readAudio && Interlocked.Decrement(ref feed.AudioReaders) == 0)
                        lock (feed.Gate) { feed.AudioGeneration++; feed.Audio.Clear(); }
                }
                clients.Release();
            }
        }
    }
    private async Task ServeMatroska(Stream stream, Feed feed, CancellationTokenSource deadline, bool audioOnly, string id)
    {
        var contentType = audioOnly ? "audio/x-matroska" : "video/x-matroska";
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n"), deadline.Token);
        if (audioOnly) await stream.WriteAsync(LiveMatroska.AudioOnlyHeader(), deadline.Token);
        else
        {
            var first = Volatile.Read(ref feed.Current)?.Jpeg ?? placeholder;
            var size = VideoWireProtocol.ValidateJpeg(first);
            await stream.WriteAsync(LiveMatroska.Header(size.Width, size.Height), deadline.Token);
        }
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var silence = new byte[AudioWireProtocol.PacketBytes];
        long audioSequence;
        lock (feed.Gate) audioSequence = feed.AudioSequence;
        long nextAudio = 0, nextVideo = 0;
        var delayLine = new AudioDelayLine();
        int audioGeneration;
        lock (feed.Gate) audioGeneration = feed.AudioGeneration;
        Encoded? previous = null;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));
        do
        {
            // A blocked OBS reader is disconnected instead of accumulating seconds of old video.
            deadline.CancelAfter(TimeSpan.FromMilliseconds(750));
            var now = clock.ElapsedMilliseconds;
            if (now >= nextAudio)
            {
                AudioFrame? audio = null;
                lock (feed.Gate)
                {
                    if (audioGeneration != feed.AudioGeneration) { delayLine.Reset(); audioGeneration = feed.AudioGeneration; }
                    foreach (var item in feed.Audio)
                        if (item.Sequence > audioSequence && DateTime.UtcNow - item.Frame.Received < TimeSpan.FromMilliseconds(100))
                        { audio = item.Frame; audioSequence = item.Sequence; break; }
                }
                if (now - nextAudio > 60) { nextAudio = now / 20 * 20; delayLine.Reset(); }
                var delayed = delayLine.Push(audio?.Pcm ?? silence, mixer?.Get(id).DelayMs ?? 0);
                var pcm = mixer?.Apply(id, delayed) ?? delayed;
                await LiveMatroska.WriteSample(stream, 2, nextAudio, pcm, deadline.Token);
                nextAudio += 20;
            }
            if (!audioOnly)
            {
                var current = Volatile.Read(ref feed.Current);
                if (current != null && DateTime.UtcNow - current.Received > TimeSpan.FromMilliseconds(500)) current = null;
                if (now >= nextVideo && (!ReferenceEquals(current, previous) || now - nextVideo > 500 || previous == null))
                {
                    await LiveMatroska.WriteSample(stream, 1, now, current?.Jpeg ?? placeholder, deadline.Token);
                    previous = current; nextVideo = now + 30;
                }
            }
        } while (await timer.WaitForNextTickAsync(stop.Token));
    }
    public void Dispose() { stop.Cancel(); listener.Stop(); }
}
