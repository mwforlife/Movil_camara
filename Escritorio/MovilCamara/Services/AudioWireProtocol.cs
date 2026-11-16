using System.Buffers.Binary;
using System.IO;

namespace MovilCamara.Services;

public sealed record AudioFrame(byte[] Pcm, long TimestampUs, DateTime Received);
public static class AudioWireProtocol
{
    public const int SampleRate = 48000, PacketBytes = 1920;
    public static async Task<AudioFrame> ReadAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[12];
        await stream.ReadExactlyAsync(header, ct);
        var length = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(0, 4));
        var timestamp = BinaryPrimitives.ReadInt64BigEndian(header.AsSpan(4, 8));
        if (length != PacketBytes || timestamp < 0) throw new InvalidDataException("Paquete de micrófono inválido");
        var pcm = new byte[length];
        await stream.ReadExactlyAsync(pcm, ct);
        return new(pcm, timestamp, DateTime.UtcNow);
    }
}
