using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace HarborOps.Api.Authentication;

public sealed class IdentityAttemptLimiter(TimeProvider clock)
{
    private sealed class Bucket { public int Count; public DateTimeOffset Until; }
    private readonly ConcurrentDictionary<string, Bucket> buckets = new();

    public bool Permit(string identifier, string source)
    {
        if (buckets.Count > 10000)
        {
            foreach (var candidate in buckets)
                if (candidate.Value.Until < clock.GetUtcNow()) buckets.TryRemove(candidate.Key, out _);
            if (buckets.Count > 10000) return false;
        }
        return Check("id:" + Hash(identifier), 10) && Check("src:" + Hash(source), 20);
    }

    private bool Check(string key, int limit)
    {
        var now = clock.GetUtcNow();
        var bucket = buckets.GetOrAdd(key, _ => new Bucket { Until = now.AddMinutes(5) });
        lock (bucket)
        {
            if (bucket.Until <= now) { bucket.Count = 0; bucket.Until = now.AddMinutes(5); }
            return ++bucket.Count <= limit;
        }
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
