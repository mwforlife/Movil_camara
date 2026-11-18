namespace MovilCamara.Services;

/// <summary>Per-reader PCM delay. One slot is exactly 20 ms; no shared dequeue between OBS readers.</summary>
public sealed class AudioDelayLine
{
    private readonly Queue<byte[]> packets = new();
    private readonly byte[] silence = new byte[AudioWireProtocol.PacketBytes];
    private int delay = -1;
    public void Reset() => packets.Clear();
    public byte[] Push(byte[] pcm, int milliseconds)
    {
        if (pcm.Length != AudioWireProtocol.PacketBytes) throw new ArgumentException("Se requiere un paquete PCM de 20 ms");
        if (milliseconds is < 0 or > 1000 || milliseconds % 20 != 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        if (delay != milliseconds) { Reset(); delay = milliseconds; }
        if (delay == 0) return pcm;
        // Take ownership of a copy, so an input buffer reused by its producer cannot alter delayed audio.
        packets.Enqueue((byte[])pcm.Clone());
        return packets.Count > delay / 20 ? packets.Dequeue() : silence;
    }
}
