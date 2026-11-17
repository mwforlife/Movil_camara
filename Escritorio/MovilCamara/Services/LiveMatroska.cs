using System.IO;
using System.Text;

namespace MovilCamara.Services;

/// <summary>Streaming EBML: timestamped MJPEG + optional PCM (silence when the microphone is off).</summary>
public static class LiveMatroska
{
    private static byte[] Join(params byte[][] parts) => parts.SelectMany(p => p).ToArray();
    private static byte[] Id(uint id) => Convert.FromHexString(id.ToString(id <= 255 ? "X2" : id <= 65535 ? "X4" : id <= 16777215 ? "X6" : "X8"));
    private static byte[] Size(int size)
    {
        if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
        for (var width = 1; width <= 8; width++)
        {
            if ((ulong)size >= ((1UL << (width * 7)) - 1)) continue;
            var result = new byte[width]; var value = (ulong)(uint)size | (1UL << (width * 7));
            for (var i = width - 1; i >= 0; i--) { result[i] = (byte)value; value >>= 8; }
            return result;
        }
        throw new ArgumentOutOfRangeException(nameof(size));
    }
    private static byte[] Element(uint id, byte[] value) => Join(Id(id), Size(value.Length), value);
    private static byte[] UInt(uint id, ulong value)
    {
        var bytes = new byte[8];
        for (var i = 7; i >= 0; i--) { bytes[i] = (byte)value; value >>= 8; }
        var first = 0; while (first < 7 && bytes[first] == 0) first++;
        return Element(id, bytes[first..]);
    }
    private static byte[] Text(uint id, string value) => Element(id, Encoding.UTF8.GetBytes(value));
    public static byte[] Header(int width, int height)
    {
        if (width < 1 || height < 1 || (long)width * height > 1920 * 1080) throw new ArgumentException("Dimensiones Matroska inválidas");
        return HeaderCore(width, height, false);
    }
    public static byte[] AudioOnlyHeader() => HeaderCore(0, 0, true);
    private static byte[] HeaderCore(int width, int height, bool audioOnly)
    {
        var ebml = Element(0x1A45DFA3, Join(UInt(0x4286, 1), UInt(0x42F7, 1), UInt(0x42F2, 4), UInt(0x42F3, 8),
            Text(0x4282, "matroska"), UInt(0x4287, 4), UInt(0x4285, 2)));
        var info = Element(0x1549A966, Join(UInt(0x2AD7B1, 1_000_000), Text(0x4D80, "WT MultiCam"), Text(0x5741, "WT MultiCam")));
        var video = audioOnly ? Array.Empty<byte>() : Element(0xAE, Join(UInt(0xD7, 1), UInt(0x73C5, 1), UInt(0x83, 1), UInt(0x9C, 0),
            UInt(0x23E383, 33_333_333), Text(0x86, "V_MJPEG"), Element(0xE0, Join(UInt(0xB0, (ulong)width), UInt(0xBA, (ulong)height)))));
        var rate = BitConverter.GetBytes(48000.0); if (BitConverter.IsLittleEndian) Array.Reverse(rate);
        var audio = Element(0xAE, Join(UInt(0xD7, 2), UInt(0x73C5, 2), UInt(0x83, 2), UInt(0x9C, 0),
            Text(0x86, "A_PCM/INT/LIT"), Element(0xE1, Join(Element(0xB5, rate), UInt(0x9F, 1), UInt(0x6264, 16)))));
        // Unknown-length Segment; finite clusters are emitted as soon as each sample is available.
        return Join(ebml, Id(0x18538067), new byte[] { 1, 255, 255, 255, 255, 255, 255, 255 }, info, Element(0x1654AE6B, Join(video, audio)));
    }
    public static async Task WriteSample(Stream stream, int track, long timestampMs, byte[] sample, CancellationToken ct)
    {
        if (track is not (1 or 2) || timestampMs < 0 || sample.Length is < 1 or > 2097152) throw new ArgumentException("Muestra Matroska inválida");
        var timecode = UInt(0xE7, (ulong)timestampMs);
        var blockSize = Size(4 + sample.Length);
        var clusterSize = timecode.Length + 1 + blockSize.Length + 4 + sample.Length;
        var prefix = Join(Id(0x1F43B675), Size(clusterSize), timecode, Id(0xA3), blockSize,
            new byte[] { (byte)(0x80 | track), 0, 0, 0x80 });
        // Keep the JPEG/PCM payload as-is; avoid copying every video frame into temporary EBML arrays.
        await stream.WriteAsync(prefix, ct);
        await stream.WriteAsync(sample, ct);
    }
}
