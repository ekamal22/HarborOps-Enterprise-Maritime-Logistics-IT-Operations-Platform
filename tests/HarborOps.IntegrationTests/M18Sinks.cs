using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace HarborOps.IntegrationTests;

// Captures every server log record, including structured values and exceptions, and every
// metric measurement with its tags, so a privacy canary can be searched across all sinks.
internal sealed class M18Sinks : ILoggerProvider
{
    private readonly ConcurrentQueue<string> lines = new();
    private readonly MeterListener listener = new();

    public M18Sinks()
    {
        listener.InstrumentPublished = (instrument, l) => l.EnableMeasurementEvents(instrument);
        listener.SetMeasurementEventCallback<long>((i, v, tags, _) => Record(i, v, tags));
        listener.SetMeasurementEventCallback<int>((i, v, tags, _) => Record(i, v, tags));
        listener.SetMeasurementEventCallback<double>((i, v, tags, _) => Record(i, v, tags));
        listener.SetMeasurementEventCallback<float>((i, v, tags, _) => Record(i, v, tags));
        listener.SetMeasurementEventCallback<short>((i, v, tags, _) => Record(i, v, tags));
        listener.SetMeasurementEventCallback<byte>((i, v, tags, _) => Record(i, v, tags));
        listener.SetMeasurementEventCallback<decimal>((i, v, tags, _) => Record(i, v, tags));
        listener.Start();
    }

    private void Record<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var text = instrument.Meter.Name + "/" + instrument.Name + "=" + value;
        foreach (var tag in tags) text += " " + tag.Key + "=" + tag.Value;
        lines.Enqueue("metric " + text);
    }

    public string All => string.Join('\n', lines);
    public int Count => lines.Count;

    public ILogger CreateLogger(string categoryName) => new Sink(this, categoryName);

    public void Dispose() => listener.Dispose();

    private sealed class Sink(M18Sinks owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var text = $"log {category} {logLevel} {formatter(state, exception)}";
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
                foreach (var pair in values) text += $" {pair.Key}={pair.Value}";
            if (exception is not null) text += " " + exception;
            owner.lines.Enqueue(text);
        }
    }
}
