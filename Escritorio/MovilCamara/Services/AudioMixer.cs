using System.Buffers.Binary;
using System.Collections.Concurrent;

namespace MovilCamara.Services;

public sealed record AudioMixSnapshot(double Volume, bool Muted, double InputPeak, double OutputPeak, bool Fresh, int DelayMs = 0)
{
    public double OutputDb => OutputPeak <= 0 ? double.NegativeInfinity : 20 * Math.Log10(OutputPeak);
}

/// <summary>Per-phone output attenuation/mute. Never starts or stops microphone capture.</summary>
public sealed class AudioMixer
{
    private sealed class State
    {
        public readonly object Gate = new();
        public double Volume = 1, Peak;
        public bool Muted;
        public int DelayMs;
        public DateTime Received;
    }
    private readonly ConcurrentDictionary<string, State> states = new();
    private State For(string id) => states.GetOrAdd(id, _ => new());
    public void SetVolume(string id, double volume)
    {
        if (!double.IsFinite(volume) || volume is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(volume));
        var state = For(id); lock (state.Gate) state.Volume = volume;
    }
    public void SetMuted(string id, bool muted) { var state = For(id); lock (state.Gate) state.Muted = muted; }
    public void SetDelay(string id, int milliseconds)
    {
        if (milliseconds < 0 || milliseconds > 1000 || milliseconds % 20 != 0)
            throw new ArgumentOutOfRangeException(nameof(milliseconds), "Usa 0-1000 ms en pasos de 20 ms");
        var state = For(id); lock (state.Gate) state.DelayMs = milliseconds;
    }
    public void Observe(string id, AudioFrame frame)
    {
        int peak = 0;
        for (var i = 0; i + 1 < frame.Pcm.Length; i += 2)
            peak = Math.Max(peak, Math.Abs((int)BinaryPrimitives.ReadInt16LittleEndian(frame.Pcm.AsSpan(i, 2))));
        var state = For(id); lock (state.Gate) { state.Peak = peak / 32768.0; state.Received = frame.Received; }
    }
    public void Clear(string id) { var state = For(id); lock (state.Gate) { state.Peak = 0; state.Received = default; } }
    public AudioMixSnapshot Get(string id)
    {
        var state = For(id);
        lock (state.Gate)
        {
            bool fresh = DateTime.UtcNow - state.Received < TimeSpan.FromMilliseconds(300);
            var peak = fresh ? state.Peak : 0;
            return new(state.Volume, state.Muted, peak, state.Muted ? 0 : peak * state.Volume, fresh, state.DelayMs);
        }
    }
    public byte[] Apply(string id, byte[] pcm)
    {
        if (pcm.Length % 2 != 0) throw new ArgumentException("PCM de 16 bits incompleto", nameof(pcm));
        var state = For(id); double gain;
        lock (state.Gate) gain = state.Muted ? 0 : state.Volume;
        if (gain == 1) return pcm;
        var output = new byte[pcm.Length];
        if (gain != 0) for (var i = 0; i < pcm.Length; i += 2)
            BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(i, 2),
                (short)Math.Round(BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(i, 2)) * gain));
        return output;
    }
}
