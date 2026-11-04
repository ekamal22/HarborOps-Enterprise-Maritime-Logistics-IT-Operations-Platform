namespace HarborOps.Infrastructure.Integrations;

// Controlled test seam for deterministic transaction failure-window verification.
// The application registers a no-op implementation; no request can select a hook.
public interface ITrackingReceiptCommitHook
{
    Task BeforeCommitAsync(Guid receiptId, CancellationToken cancellationToken);
    Task AfterCommitAsync(Guid receiptId, CancellationToken cancellationToken);
}

public sealed class NoopTrackingReceiptCommitHook : ITrackingReceiptCommitHook
{
    public Task BeforeCommitAsync(Guid receiptId, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task AfterCommitAsync(Guid receiptId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
