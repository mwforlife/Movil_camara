using System.Buffers.Binary;
using System.IO;

namespace MovilCamara.Services;

public sealed record VideoFrame(byte[] Jpeg, int Rotation, bool Mirrored, DateTime Received,
    byte[]? Bgr32 = null, int Width = 0, int Height = 0);
public sealed record H264Packet(byte[] Data, int Rotation, bool Mirrored, bool KeyFrame, long TimestampUs);
public static class VideoWireProtocol
{
    public const int MaxFrameBytes = 2097152;
    public static async Task<H264Packet> ReadH264Async(Stream stream, CancellationToken cancellation)
    {
        var header = new byte[20];
        await stream.ReadExactlyAsync(header, cancellation);
        var length = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(0, 4));
        var rotation = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4, 4));
        var flags = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(8, 4));
        var timestamp = BinaryPrimitives.ReadInt64BigEndian(header.AsSpan(12, 8));
        if (length is < 4 or > 2097152 || rotation is not (0 or 90 or 180 or 270) || flags is < 0 or > 3 || timestamp < 0)
            throw new InvalidDataException("Paquete H.264 fuera de límites.");
        var data = new byte[length];
        await stream.ReadExactlyAsync(data, cancellation);
        return new(data, rotation, (flags & 1) != 0, (flags & 2) != 0, timestamp);
    }
    public static async Task<VideoFrame> ReadFrameAsync(Stream stream, CancellationToken cancellation)
    {
        var header = new byte[12];
        await stream.ReadExactlyAsync(header, cancellation);
        var length = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(0, 4));
        var rotation = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4, 4));
        var flags = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(8, 4));
        if (length is < 4 or > MaxFrameBytes || rotation is not (0 or 90 or 180 or 270) || flags is not (0 or 1))
            throw new InvalidDataException("Cabecera de video inválida.");
        var jpeg = new byte[length];
        await stream.ReadExactlyAsync(jpeg, cancellation);
        ValidateJpeg(jpeg);
        return new VideoFrame(jpeg, rotation, flags == 1, DateTime.UtcNow);
    }

    public static (int Width, int Height) ValidateJpeg(ReadOnlySpan<byte> jpeg)
    {
        if (jpeg.Length < 4 || jpeg[0] != 255 || jpeg[1] != 216 || jpeg[^2] != 255 || jpeg[^1] != 217)
            throw new InvalidDataException("El frame no es JPEG.");
        var index = 2;
        while (index + 3 < jpeg.Length)
        {
            if (jpeg[index++] != 255) throw new InvalidDataException("Segmento JPEG inválido.");
            while (index < jpeg.Length && jpeg[index] == 255) index++;
            if (index >= jpeg.Length) break;
            var marker = jpeg[index++];
            if (marker is 0x01 or >= 0xd0 and <= 0xd9) continue;
            if (marker is 0xda or 0x00 || index + 2 > jpeg.Length) break;
            var length = (jpeg[index] << 8) | jpeg[index + 1];
            if (length < 2 || index + length > jpeg.Length) throw new InvalidDataException("Segmento JPEG truncado.");
            if (marker is 0xc0 or 0xc1 or 0xc2)
            {
                if (length < 8) throw new InvalidDataException("Dimensiones JPEG ausentes.");
                var height = (jpeg[index + 3] << 8) | jpeg[index + 4];
                var width = (jpeg[index + 5] << 8) | jpeg[index + 6];
                if (width <= 0 || height <= 0 || width > 1920 || height > 1920 || (long)width * height > 1920 * 1080)
                    throw new InvalidDataException("Resolución fuera del perfil de video.");
                return (width, height);
            }
            index += length;
        }
        throw new InvalidDataException("No se encontraron dimensiones JPEG compatibles.");
    }
}
