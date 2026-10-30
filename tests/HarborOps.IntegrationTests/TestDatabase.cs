using Npgsql;

namespace HarborOps.IntegrationTests;

/// <summary>
/// Resolves the integration database and fails closed before any connection is opened,
/// because these tests run EF migrations against whatever database they are given.
/// </summary>
internal static class TestDatabase
{
    public static string ConnectionString()
    {
        var raw = Environment.GetEnvironmentVariable("HARBOROPS_TEST_DB")
            ?? throw new InvalidOperationException(
                "HARBOROPS_TEST_DB must point to a disposable local harborops integration database");

        var connection = new NpgsqlConnectionStringBuilder(raw);
        if (!string.Equals(connection.Database, "harborops_integration", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(connection.Database, "harborops_test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Integration tests require database harborops_integration or harborops_test");
        }

        var host = connection.Host?.Trim();
        if (host is not ("localhost" or "127.0.0.1" or "::1"))
        {
            throw new InvalidOperationException(
                "Integration tests require a local PostgreSQL host");
        }

        return connection.ConnectionString;
    }
}
