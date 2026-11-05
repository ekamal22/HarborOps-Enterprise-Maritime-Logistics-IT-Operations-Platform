namespace HarborOps.Infrastructure.Audit;

// Async-flow-local request correlation. A disposable scope always restores
// the previous value, including when the request pipeline throws.
public static class AuditCorrelation
{
    private static readonly AsyncLocal<string?> ValueSlot = new();
    public static string Current => ValueSlot.Value ?? "database";

    public static IDisposable Begin(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 64)
            throw new ArgumentException("Invalid correlation identifier", nameof(correlationId));
        var previous = ValueSlot.Value;
        ValueSlot.Value = correlationId;
        return new Restore(previous);
    }

    private sealed class Restore(string? previous) : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            if (disposed) return;
            ValueSlot.Value = previous;
            disposed = true;
        }
    }
}
