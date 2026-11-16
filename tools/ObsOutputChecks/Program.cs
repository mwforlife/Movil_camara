using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MovilCamara.Services;

static void Check(bool result, string description)
{
    if (!result) throw new Exception("FAIL: " + description);
    Console.WriteLine("PASS: " + description);
}
static BitmapImage Decode(byte[] bytes)
{
    using var stream = new MemoryStream(bytes);
    var image = new BitmapImage();
    image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit();
    image.Freeze(); return image;
}
var raw = new byte[80 * 40 * 4];
for (var y = 0; y < 40; y++)
for (var x = 0; x < 80; x++)
{
    var offset = (y * 80 + x) * 4;
    var (b, g, r) = (x < 40, y < 20) switch
    {
        (true, true) => (0, 0, 255),
        (false, true) => (0, 255, 0),
        (true, false) => (255, 0, 0),
        _ => (255, 255, 255)
    };
    raw[offset] = (byte)b; raw[offset + 1] = (byte)g; raw[offset + 2] = (byte)r;
}
var frame = new VideoFrame(Array.Empty<byte>(), 90, true, DateTime.UtcNow, raw, 80, 40);
var encoded = ObsFrameEncoder.Encode(frame);
var decoded = Decode(encoded);
Check(decoded.PixelWidth == 40 && decoded.PixelHeight == 80, "salida OBS conserva dimensiones verticales tras girar");
var pixels = new FormatConvertedBitmap(decoded, PixelFormats.Bgr24, null, 0);
var bytes = new byte[40 * 80 * 3]; pixels.CopyPixels(bytes, 40 * 3, 0);
bool Color(int x, int y, int b, int g, int r)
{
    var offset = (y * 40 + x) * 3;
    return Math.Abs(bytes[offset] - b) < 50 && Math.Abs(bytes[offset + 1] - g) < 50 && Math.Abs(bytes[offset + 2] - r) < 50;
}
Check(Color(10, 20, 0, 0, 255) && Color(30, 20, 255, 0, 0) &&
    Color(10, 60, 0, 255, 0) && Color(30, 60, 255, 255, 255), "salida OBS aplica giro y espejo en el orden correcto");
var unchanged = new VideoFrame(encoded, 0, false, DateTime.UtcNow);
Check(ReferenceEquals(ObsFrameEncoder.Encode(unchanged), encoded), "JPEG sin transformaciones evita recodificación");
Check(Decode(ObsFrameEncoder.NoSignal()).PixelWidth == 640, "imagen sin señal es JPEG válido");
var mkvPath = Path.Combine(Path.GetTempPath(), "wt-av-check-" + Guid.NewGuid() + ".mkv");
try
{
    await using (var file = File.Create(mkvPath))
    {
        await file.WriteAsync(LiveMatroska.Header(40, 80));
        for (var i = 0; i < 50; i++)
        {
            var pcm = new byte[1920];
            for (var j = 0; j < 960; j++)
            {
                var sample = (short)(Math.Sin((i * 960 + j) * 2 * Math.PI * 440 / 48000) * 5000);
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(j * 2, 2), sample);
            }
            await LiveMatroska.WriteSample(file, 2, i * 20, pcm, CancellationToken.None);
            if (i % 2 == 0) await LiveMatroska.WriteSample(file, 1, i * 20, encoded, CancellationToken.None);
        }
    }
    if (File.Exists("C:\\Program Files\\obs-studio\\bin\\64bit\\avformat-62.dll"))
    {
        NativeMedia.Check(mkvPath);
        Console.WriteLine("PASS: FFmpeg de OBS abre Matroska, lee 25 cuadros y 50 paquetes PCM con timestamps crecientes");
    }
    else Console.WriteLine("SKIP: FFmpeg de OBS no encontrado; validar el contenedor con una instalación compatible");
}
finally { if (File.Exists(mkvPath)) File.Delete(mkvPath); }

internal static class NativeMedia
{
    [System.Runtime.InteropServices.DllImport("kernel32", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool SetDllDirectory(string path);
    [System.Runtime.InteropServices.DllImport("avformat-62", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern int avformat_open_input(ref IntPtr context, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string path, IntPtr format, IntPtr options);
    [System.Runtime.InteropServices.DllImport("avformat-62", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern int avformat_find_stream_info(IntPtr context, IntPtr options);
    [System.Runtime.InteropServices.DllImport("avformat-62", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern int av_read_frame(IntPtr context, IntPtr packet);
    [System.Runtime.InteropServices.DllImport("avformat-62", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern void avformat_close_input(ref IntPtr context);
    [System.Runtime.InteropServices.DllImport("avcodec-62", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern IntPtr av_packet_alloc();
    [System.Runtime.InteropServices.DllImport("avcodec-62", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern void av_packet_unref(IntPtr packet);
    [System.Runtime.InteropServices.DllImport("avcodec-62", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern void av_packet_free(ref IntPtr packet);
    public static void Check(string path)
    {
        if (IntPtr.Size != 8) throw new NotSupportedException("La prueba FFmpeg requiere x64");
        SetDllDirectory("C:\\Program Files\\obs-studio\\bin\\64bit");
        IntPtr context = IntPtr.Zero, packet = IntPtr.Zero;
        try
        {
            if (avformat_open_input(ref context, path, IntPtr.Zero, IntPtr.Zero) < 0 || avformat_find_stream_info(context, IntPtr.Zero) < 0)
                throw new Exception("FFmpeg no pudo abrir el Matroska generado");
            packet = av_packet_alloc();
            var counts = new int[2]; var pts = new long[] { -1, -1 };
            while (av_read_frame(context, packet) >= 0)
            {
                var track = System.Runtime.InteropServices.Marshal.ReadInt32(packet, 36);
                var time = System.Runtime.InteropServices.Marshal.ReadInt64(packet, 8);
                if (track is < 0 or > 1 || time <= pts[track]) throw new Exception("Tracks/timestamps Matroska inválidos");
                if (track == 1 && System.Runtime.InteropServices.Marshal.ReadInt32(packet, 32) != 1920) throw new Exception("PCM alterado");
                counts[track]++; pts[track] = time; av_packet_unref(packet);
            }
            if (counts[0] != 25 || counts[1] != 50 || pts[0] != 960 || pts[1] != 980) throw new Exception("Demultiplexado A/V incompleto");
        }
        finally { if (packet != IntPtr.Zero) av_packet_free(ref packet); if (context != IntPtr.Zero) avformat_close_input(ref context); SetDllDirectory(""); }
    }
}
