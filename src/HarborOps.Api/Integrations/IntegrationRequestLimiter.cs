using System.Collections.Concurrent;

namespace HarborOps.Api.Integrations;

public sealed class IntegrationRequestLimiter(TimeProvider clock)
{
    private sealed class Window
    {
        public DateTimeOffset Starts;
        public int Count;
    }
    private readonly ConcurrentDictionary<string, Window> windows = new();

    public bool Allow(string partition, int perMinute)
    {
        var now = clock.GetUtcNow();
        var window = windows.GetOrAdd(partition, _ => new Window { Starts = now });
        lock (window)
        {
            if (now - window.Starts >= TimeSpan.FromMinutes(1))
            {
                window.Starts = now;
                window.Count = 0;
            }
            if (window.Count >= perMinute) return false;
            window.Count++;
        }
        if (windows.Count > 4096)
            foreach (var entry in windows)
                if (now - entry.Value.Starts > TimeSpan.FromMinutes(2))
                    windows.TryRemove(entry.Key, out _);
        return true;
    }
}
