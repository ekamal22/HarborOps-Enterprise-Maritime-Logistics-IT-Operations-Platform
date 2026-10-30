using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace HarborOps.ContractTests;

public sealed record CapturedLog(LogLevel Level, string Message, IReadOnlyList<object?> Scopes);

/// <summary>Collects log entries together with the scopes active when they were written.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider scopes = new LoggerExternalScopeProvider();

    public ConcurrentQueue<CapturedLog> Entries { get; } = new();

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => scopes = scopeProvider;

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            owner.scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var active = new List<object?>();
            owner.scopes.ForEachScope((scope, list) => list.Add(scope), active);
            owner.Entries.Enqueue(new CapturedLog(logLevel, formatter(state, exception), active));
        }
    }
}
