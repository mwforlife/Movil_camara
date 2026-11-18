namespace MovilCamara.Services;

/// <summary>Bounded compressed input. After a gap, discard dependent frames until the next IDR.</summary>
public sealed class H264PacketQueue : IDisposable
{
    private readonly object gate = new();
    private readonly Queue<H264Packet> packets = new();
    private readonly SemaphoreSlim ready = new(0, 1);
    private bool waitingForKey = true;
    public bool HasPending { get { lock (gate) return packets.Count != 0; } }
    public int Dropped { get; private set; }
    public void Offer(H264Packet packet)
    {
        lock (gate)
        {
            if (packets.Count >= 8)
            { Dropped += packets.Count; packets.Clear(); waitingForKey = true; }
            if (waitingForKey && !packet.KeyFrame) { Dropped++; return; }
            packets.Enqueue(packet); waitingForKey = false;
            if (ready.CurrentCount == 0) ready.Release();
        }
    }
    public bool TryTake(out H264Packet? packet)
    {
        lock (gate)
        {
            if (packets.Count == 0) { packet = null; return false; }
            packet = packets.Dequeue(); return true;
        }
    }
    public async Task<H264Packet> ReadAsync(CancellationToken cancellation)
    {
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            if (TryTake(out var packet)) return packet!;
            await ready.WaitAsync(cancellation);
        }
    }
    public void Dispose() => ready.Dispose();
}
