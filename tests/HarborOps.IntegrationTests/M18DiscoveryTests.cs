using System.Net;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace HarborOps.IntegrationTests;

// Exploratory observation of the real candidate behaviour. Assertions here only fail on
// unexpected infrastructure faults; the rows they print are the input to the audit.
[Collection("M12ServerRoleIsolation")]
public sealed class M18DiscoveryTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ObserveCreateAndAuditShape()
    {
        using var world = await M18World.CreateAsync();
        using var support = await world.Client("support");
        var (id, version) = await world.CreateIncident(support);
        output.WriteLine($"created {id} v{version}");
        await using var connection = new NpgsqlConnection(world.Roles.Admin);
        await connection.OpenAsync();
        await using var audit = new NpgsqlCommand("""
            SELECT event_type,outcome,actor_kind,actor_user_id IS NOT NULL,actor_role,resource_type,
                   metadata::text
            FROM audit_entries WHERE organization_id=@o AND resource_type LIKE 'it_incident%'
            ORDER BY occurred_at_utc,id
            """, connection);
        audit.Parameters.AddWithValue("o", world.A);
        await using var reader = await audit.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            output.WriteLine($"audit {reader.GetString(0)} {reader.GetString(1)} {reader.GetString(2)} " +
                $"actor={reader.GetBoolean(3)} role={(reader.IsDBNull(4) ? "-" : reader.GetString(4))} " +
                $"{reader.GetString(5)} {reader.GetString(6)}");
        using var detail = await support.GetAsync(world.Route(null, "/" + id));
        output.WriteLine($"detail {(int)detail.StatusCode} {await detail.Content.ReadAsStringAsync()}");
        using var list = await support.GetAsync(world.Route());
        output.WriteLine($"list {(int)list.StatusCode} {await list.Content.ReadAsStringAsync()}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    }
}
