using System.Net;
using System.Net.Sockets;
using System.Text;
using Npgsql;

namespace HarborOps.IntegrationTests;

// Disposable PostgreSQL wire proxy. Its only fault trigger is an actual
// receipt-work INSERT followed by COMMIT on the same database connection.
// No production service registers or invokes this type.
public sealed class TrackingCommitSocketProxy : IAsyncDisposable
{
    public enum CutPoint { BeforeCommitForward, AfterCommitServerReady }

    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly string targetHost;
    private readonly int targetPort;
    private readonly CutPoint cut;
    private readonly string armingStatement;
    private readonly Task accepting;
    private readonly List<Task> connections = [];
    private int triggered;

    public bool Triggered => Volatile.Read(ref triggered) != 0;
    public int Port { get; }

    public TrackingCommitSocketProxy(string originalConnection, CutPoint cutPoint,
        string armingStatement = "INSERT INTO tracking_inbox_work_items")
    {
        var original = new NpgsqlConnectionStringBuilder(originalConnection);
        targetHost = original.Host ?? throw new ArgumentException("A database host is required", nameof(originalConnection));
        targetPort = original.Port;
        cut = cutPoint;
        this.armingStatement = armingStatement;
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        accepting = Task.Run(AcceptAsync);
    }

    public string ProxiedConnectionString(string originalConnection)
    {
        var builder = new NpgsqlConnectionStringBuilder(originalConnection)
        {
            Host = "127.0.0.1",
            Port = Port,
            Pooling = false,
            SslMode = SslMode.Disable
        };
        return builder.ConnectionString;
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stop.Token);
                lock (connections) connections.Add(HandleAsync(client));
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) when (stop.IsCancellationRequested) { }
    }

    private async Task HandleAsync(TcpClient client)
    {
        using (client)
        using (var backend = new TcpClient())
        {
            try
            {
                await backend.ConnectAsync(targetHost, targetPort, stop.Token);
                using var outgoing = client.GetStream();
                using var incoming = backend.GetStream();
                var armed = 0;
                var commitForwarded = 0;
                var dropRequested = 0;

                async Task FrontendToBackend()
                {
                    var buffer = new byte[32768];
                    var trailing = "";
                    while (!stop.IsCancellationRequested)
                    {
                        var count = await outgoing.ReadAsync(buffer, stop.Token);
                        if (count == 0) break;
                        // PostgreSQL command text is carried in the Query/Parse
                        // payload. Keep a short tail for split TCP segments.
                        var text = trailing + Encoding.UTF8.GetString(buffer, 0, count);
                        if (text.Contains(armingStatement,
                                StringComparison.Ordinal))
                            Volatile.Write(ref armed, 1);
                        var commit = Volatile.Read(ref armed) == 1 &&
                            text.Contains("COMMIT", StringComparison.Ordinal);
                        trailing = text.Length > 512 ? text[^512..] : text;

                        if (commit && Interlocked.CompareExchange(ref triggered, 1, 0) == 0)
                        {
                            if (cut == CutPoint.BeforeCommitForward)
                            {
                                Volatile.Write(ref dropRequested, 1);
                                return;
                            }
                            Volatile.Write(ref commitForwarded, 1);
                        }
                        await incoming.WriteAsync(buffer.AsMemory(0, count), stop.Token);
                    }
                }

                async Task BackendToFrontend()
                {
                    var buffer = new byte[32768];
                    var trailing = Array.Empty<byte>();
                    while (!stop.IsCancellationRequested)
                    {
                        var count = await incoming.ReadAsync(buffer, stop.Token);
                        if (count == 0) break;
                        if (cut == CutPoint.AfterCommitServerReady &&
                            Volatile.Read(ref commitForwarded) == 1)
                        {
                            // The server emitted ReadyForQuery (Z, length=5, idle=I)
                            // after processing COMMIT. Drop the acknowledgment,
                            // not the durable transaction.
                            var combined = new byte[trailing.Length + count];
                            trailing.CopyTo(combined, 0);
                            Buffer.BlockCopy(buffer, 0, combined, trailing.Length, count);
                            var ready = false;
                            for (var i = 0; i <= combined.Length - 6; i++)
                            {
                                if (combined[i] == (byte)'Z' &&
                                    combined[i + 1] == 0 && combined[i + 2] == 0 &&
                                    combined[i + 3] == 0 && combined[i + 4] == 5 &&
                                    combined[i + 5] == (byte)'I')
                                {
                                    ready = true;
                                    break;
                                }
                            }
                            trailing = combined.Length > 5 ? combined[^5..] : combined;
                            if (ready)
                            {
                                Volatile.Write(ref dropRequested, 1);
                                return;
                            }
                            continue;
                        }
                        await outgoing.WriteAsync(buffer.AsMemory(0, count), stop.Token);
                    }
                }

                var front = FrontendToBackend();
                var back = BackendToFrontend();
                await Task.WhenAny(front, back);
                // Closing the sockets causes a real Npgsql transport failure.
                client.Close();
                backend.Close();
                try { await Task.WhenAll(front, back).WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (Exception) { /* Socket teardown is the intended fault. */ }
                if (Volatile.Read(ref dropRequested) != 0)
                    Volatile.Write(ref triggered, 1);
            }
            catch (Exception) when (!stop.IsCancellationRequested)
            {
                client.Close();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        listener.Stop();
        try { await accepting.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { }
        Task[] tasks;
        lock (connections) tasks = [.. connections];
        try { await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception) { }
        stop.Dispose();
    }
}
