using System.IO;
using System.Net.Http;
using System.Text;
using MovilCamara.Services;

internal static class AudioOutputChecks
{
    private static void Check(bool result, string name)
    { if (!result) throw new Exception(name); Console.WriteLine("PASS: " + name); }

    public static async Task Run()
    {
        var keyPath = Path.Combine(Path.GetTempPath(), "wt-audio-http-" + Guid.NewGuid() + ".key");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        Task? producer = null;
        int encoded = 0, sendA = 1;
        try
        {
            using var output = new ObsDirectServer(f => { Interlocked.Increment(ref encoded); return f.Jpeg; }, [], keyPath);
            output.Start();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
            var urlA = output.GetAudioUrl("phone-audio-a");
            using var a = await http.GetAsync(urlA, HttpCompletionOption.ResponseHeadersRead, stop.Token);
            using var b = await http.GetAsync(output.GetAudioUrl("phone-audio-b"), HttpCompletionOption.ResponseHeadersRead, stop.Token);
            Check(a.IsSuccessStatusCode && b.IsSuccessStatusCode && a.Content.Headers.ContentType?.MediaType == "audio/x-matroska",
                "dos fuentes HTTP de sólo audio abiertas");
            using var streamA = await a.Content.ReadAsStreamAsync(stop.Token);
            using var streamB = await b.Content.ReadAsStreamAsync(stop.Token);
            await ReadHeader(streamA, stop.Token); await ReadHeader(streamB, stop.Token);
            var pcmA = Enumerable.Repeat((byte)0x12, AudioWireProtocol.PacketBytes).ToArray();
            var pcmB = Enumerable.Repeat((byte)0x34, AudioWireProtocol.PacketBytes).ToArray();
            producer = Task.Run(async () =>
            {
                long timestamp = 0;
                while (!stop.IsCancellationRequested)
                {
                    if (Volatile.Read(ref sendA) == 1) output.OfferAudio("phone-audio-a", new(pcmA, timestamp, DateTime.UtcNow));
                    output.OfferAudio("phone-audio-b", new(pcmB, timestamp, DateTime.UtcNow));
                    output.Offer("phone-audio-a", new([], 0, false, DateTime.UtcNow));
                    output.Offer("phone-audio-b", new([], 0, false, DateTime.UtcNow));
                    timestamp += 20000;
                    await Task.Delay(20, stop.Token);
                }
            });
            await Until(streamA, pcmA, stop.Token); await Until(streamB, pcmB, stop.Token);
            Check(Volatile.Read(ref encoded) == 0, "lectores de sólo audio no codifican imágenes");
            Volatile.Write(ref sendA, 0); output.ClearAudio("phone-audio-a");
            await Until(streamA, new byte[AudioWireProtocol.PacketBytes], stop.Token);
            await Until(streamB, pcmB, stop.Token);
            Check(true, "apagar A produce silencio y B conserva su audio");
            a.Dispose();
            using var reopened = await http.GetAsync(urlA, HttpCompletionOption.ResponseHeadersRead, stop.Token);
            using var newA = await reopened.Content.ReadAsStreamAsync(stop.Token);
            await ReadHeader(newA, stop.Token);
            await Until(newA, new byte[AudioWireProtocol.PacketBytes], stop.Token);
            Volatile.Write(ref sendA, 1);
            await Until(newA, pcmA, stop.Token); await Until(streamB, pcmB, stop.Token);
            Check(true, "reconectar A recupera su audio sin interrumpir B");
        }
        finally
        {
            stop.Cancel();
            if (producer != null) try { await producer; } catch (OperationCanceledException) { }
            if (File.Exists(keyPath)) File.Delete(keyPath);
        }
    }
    private static async Task<(ulong Id, ulong Size)> Header(Stream stream, CancellationToken ct)
    {
        async Task<ulong> Vint(bool identifier)
        {
            var one = new byte[1]; await stream.ReadExactlyAsync(one, ct);
            int width = 1; while (width <= 8 && (one[0] & (0x80 >> (width - 1))) == 0) width++;
            if (width > (identifier ? 4 : 8)) throw new IOException("EBML inválido");
            ulong value = identifier ? one[0] : (ulong)(one[0] & (0xff >> width));
            for (int i = 1; i < width; i++) { await stream.ReadExactlyAsync(one, ct); value = (value << 8) | one[0]; }
            return value;
        }
        return (await Vint(true), await Vint(false));
    }
    private static async Task<byte[]> Payload(Stream stream, ulong size, CancellationToken ct)
    {
        if (size > 2097152) throw new IOException("Elemento EBML excesivo");
        var bytes = new byte[(int)size]; await stream.ReadExactlyAsync(bytes, ct); return bytes;
    }
    private static async Task ReadHeader(Stream stream, CancellationToken ct)
    {
        var ebml = await Header(stream, ct); Check(ebml.Id == 0x1A45DFA3, "cabecera EBML válida");
        await Payload(stream, ebml.Size, ct);
        var segment = await Header(stream, ct); Check(segment.Id == 0x18538067, "segmento Matroska válido");
        var info = await Header(stream, ct); Check(info.Id == 0x1549A966, "reloj de salida Matroska presente");
        await Payload(stream, info.Size, ct);
        var tracks = await Header(stream, ct); Check(tracks.Id == 0x1654AE6B, "lista de pistas presente");
        var text = Encoding.ASCII.GetString(await Payload(stream, tracks.Size, ct));
        Check(text.Contains("A_PCM/INT/LIT") && !text.Contains("V_MJPEG"), "cabecera de audio sin pista de video");
    }
    private static async Task Until(Stream stream, byte[] expected, CancellationToken ct)
    {
        long previous = -1;
        for (int i = 0; i < 100; i++)
        {
            var cluster = await Header(stream, ct);
            if (cluster.Id != 0x1F43B675) throw new IOException("No se recibió un cluster Matroska");
            using var body = new MemoryStream(await Payload(stream, cluster.Size, ct));
            var time = await Header(body, ct); if (time.Id != 0xE7) throw new IOException("Timestamp ausente");
            ulong milliseconds = 0; foreach (var value in await Payload(body, time.Size, ct)) milliseconds = (milliseconds << 8) | value;
            if ((long)milliseconds <= previous) throw new IOException("Timestamp no creciente"); previous = (long)milliseconds;
            var block = await Header(body, ct); if (block.Id != 0xA3) throw new IOException("Muestra ausente");
            var bytes = await Payload(body, block.Size, ct);
            if (bytes.Length != AudioWireProtocol.PacketBytes + 4 || bytes[0] != 0x82) throw new IOException("Se recibió otro track o tamaño PCM");
            if (bytes.AsSpan(4).SequenceEqual(expected)) return;
        }
        throw new IOException("No llegó el audio esperado o no se limpió el audio anterior");
    }
}
