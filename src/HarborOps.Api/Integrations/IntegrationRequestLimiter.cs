namespace HarborOps.Api.Integrations;

// Bounded local limiter. The remote peer is derived from the trusted transport boundary,
// never from a caller-supplied tenant or credential identifier.
public sealed class IntegrationRequestLimiter(TimeProvider clock)
{
    private sealed class Window
    {
        public DateTimeOffset Starts;
        public int Count;
    }

    private const int MaximumPartitions = 4096;
    private readonly object gate = new();
    private readonly Dictionary<string, Window> windows = new(StringComparer.Ordinal);
    private DateTimeOffset nextSweep = DateTimeOffset.MinValue;

    public bool Allow(string partition, int perMinute)
    {
        if (string.IsNullOrEmpty(partition) || perMinute <= 0) return false;
        lock (gate)
        {
            var now = clock.GetUtcNow();
            if (now >= nextSweep)
            {
                foreach (var key in windows.Where(entry =>
                    now - entry.Value.Starts >= TimeSpan.FromMinutes(2))
                    .Select(entry => entry.Key).ToArray())
                    windows.Remove(key);
                nextSweep = now + TimeSpan.FromSeconds(15);
            }

            if (!windows.TryGetValue(partition, out var window))
            {
                // Never allocate without a bound; deny overflow rather than increasing memory
                // under a flood of distinct peers or making a client-controlled overflow key.
                if (windows.Count >= MaximumPartitions) return false;
                window = new Window { Starts = now };
                windows.Add(partition, window);
            }

            if (now - window.Starts >= TimeSpan.FromMinutes(1) || now < window.Starts)
            {
                window.Starts = now;
                window.Count = 0;
            }
            if (window.Count >= perMinute) return false;
            window.Count++;
            return true;
        }
    }
}
