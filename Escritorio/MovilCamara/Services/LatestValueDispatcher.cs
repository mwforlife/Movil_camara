namespace MovilCamara.Services;

/// <summary>At most one operation in flight and one latest desired value, never a backlog of slider positions.</summary>
public sealed class LatestValueDispatcher<T> where T : class
{
    private readonly object gate = new();
    private T? latest;
    private bool sending;
    public bool IsBusy { get { lock (gate) return sending || latest != null; } }
    public void Offer(T value) { lock (gate) latest = value; }
    public void Clear() { lock (gate) latest = null; }
    public async Task FlushAsync(Func<T, Task> send)
    {
        T value;
        lock (gate)
        {
            if (sending || latest == null) return;
            value = latest; latest = null; sending = true;
        }
        try { await send(value); }
        finally { lock (gate) sending = false; }
    }
}
