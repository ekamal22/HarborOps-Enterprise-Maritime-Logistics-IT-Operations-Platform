using System.Security.Cryptography;
using System.Text;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// G18 migration witnesses: an upgrade from the last accepted schema keeps every existing row and
// audit record, the downgrade guard refuses to destroy retained incident evidence, and the
// migration is re-applicable after a clean downgrade. Scratch databases are created on the owned
// disposable server and dropped afterwards.
[Collection("M12ServerRoleIsolation")]
public sealed class M18MigrationTests
{
    private const string Previous = "20261112090000_M15MembershipFeedIndexes";
    private const string Head = "20261118190000_M18IncidentLifecycle";

    private sealed class Scratch : IAsyncDisposable
    {
        public required string Name { get; init; }
        public required string Connection { get; init; }
        private readonly string admin;
        public Scratch(string adminConnection) => admin = adminConnection;

        public static async Task<Scratch> Create()
        {
            M18World.RequireExclusive();
            var baseConnection = new NpgsqlConnectionStringBuilder(TestDatabase.ConnectionString());
            var name = "ho_g18_" + Guid.NewGuid().ToString("N")[..12];
            await using (var connection = new NpgsqlConnection(baseConnection.ConnectionString))
            {
                await connection.OpenAsync();
                await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
                await create.ExecuteNonQueryAsync();
            }
            baseConnection.Database = name;
            baseConnection.Pooling = false;
            return new Scratch(TestDatabase.ConnectionString()) { Name = name, Connection = baseConnection.ConnectionString };
        }

        public HarborOpsDbContext Context() =>
            new(new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(Connection).Options);

        public async Task<T> Scalar<T>(string sql)
        {
            await using var connection = new NpgsqlConnection(Connection);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            var result = await command.ExecuteScalarAsync();
            return result is null or DBNull ? default! : (T)Convert.ChangeType(result, typeof(T));
        }

        public async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)", connection);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<string> Fingerprint(Scratch db)
    {
        // Row content hash of every pre-existing business and audit table that M18 must leave alone.
        var tables = await db.Scalar<string>("""
            SELECT string_agg(table_name, ',' ORDER BY table_name) FROM information_schema.tables
            WHERE table_schema='public' AND table_type='BASE TABLE' AND table_name NOT LIKE 'it\_incident%'
              AND table_name <> '__EFMigrationsHistory'
            """);
        var builder = new StringBuilder();
        foreach (var table in tables.Split(','))
        {
            var hash = await db.Scalar<string>($"SELECT coalesce(md5(string_agg(t::text, '|' ORDER BY t::text)), 'empty') FROM \"{table}\" t");
            builder.Append(table).Append('=').Append(await db.Scalar<long>($"SELECT count(*) FROM \"{table}\"")).Append(':').Append(hash).Append(';');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))) + "|" + builder;
    }

    [Fact]
    public async Task MigrationOrderIsUnchangedBeforeM18AndM18IsTheOnlyNewMigration()
    {
        await using var scratch = await Scratch.Create();
        await using var db = scratch.Context();
        var all = db.Database.GetMigrations().ToList();
        Assert.Equal(Head, all[^1]);
        Assert.Equal(Previous, all[^2]);
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.Equal(all.OrderBy(x => x, StringComparer.Ordinal), all);
    }

    [Fact]
    public async Task UpgradeFromTheLastAcceptedSchemaKeepsEveryExistingRowAndAuditRecord()
    {
        await using var scratch = await Scratch.Create();
        var settings = new Dictionary<string, string> { ["M18:EnableIncidents"] = "true" };
        using var factory = new ReferenceDataHttpTests.ApiFactory(scratch.Connection, settings: settings);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            await db.Database.GetService<IMigrator>().MigrateAsync(Previous);
            Assert.Null(await scratch.Scalar<string?>("SELECT to_regclass('public.it_incidents')::text"));
            await BootstrapSeeder.SeedAsync(db, CancellationToken.None);
            var ids = scope.ServiceProvider.GetRequiredService<IdentityStore>();
            var organizations = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
            var nonce = Guid.NewGuid().ToString("N");
            var email = $"upgrade-{nonce}@example.test";
            await ids.ProvisionAsync(email, "Upgrade Admin", M18World.Password, CancellationToken.None);
            await organizations.BootstrapAsync("up-" + nonce[..10], "Upgrade Org", email, CancellationToken.None);
            var session = (await ids.LoginAsync(email, M18World.Password, "upgrade", CancellationToken.None))!;
            Assert.NotNull(session);
        }
        var auditBefore = await scratch.Scalar<long>("SELECT count(*) FROM audit_entries");
        Assert.True(auditBefore > 0, "the accepted schema fixture must hold audit evidence");
        var before = await Fingerprint(scratch);

        await using (var db = scratch.Context())
            await db.Database.MigrateAsync();

        Assert.Equal(Head, await scratch.Scalar<string>("SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 1"));
        Assert.Equal(before, await Fingerprint(scratch));
        Assert.Equal(auditBefore, await scratch.Scalar<long>("SELECT count(*) FROM audit_entries"));
        foreach (var table in M18DatabaseTests.Tables)
        {
            Assert.Equal(1L, await scratch.Scalar<long>($"SELECT count(*) FROM pg_class WHERE relname='{table}' AND relrowsecurity AND relforcerowsecurity"));
            Assert.Equal(0L, await scratch.Scalar<long>($"SELECT count(*) FROM {table}"));
        }
        // the accepted M16 capture function and guard are untouched
        Assert.Equal(1L, await scratch.Scalar<long>("SELECT count(*) FROM pg_trigger WHERE tgname='m16_audit_no_change'"));
        Assert.Equal(1L, await scratch.Scalar<long>("SELECT count(*) FROM pg_proc WHERE proname='m16_audit_capture'"));
        // a second application is a no-op
        await using (var again = scratch.Context()) await again.Database.MigrateAsync();
        Assert.Equal(before, await Fingerprint(scratch));
    }

    [Fact]
    public async Task DowngradeIsRefusedWhileIncidentEvidenceExistsAndIsReapplicableWhenEmpty()
    {
        await using var scratch = await Scratch.Create();
        await using (var db = scratch.Context()) await db.Database.MigrateAsync();
        var objects = """
            SELECT (SELECT count(*) FROM pg_class WHERE relname LIKE 'it\_incident%' AND relkind='r')
                 + (SELECT count(*) FROM pg_proc WHERE proname IN ('m18_scoped_session','m18_history_immutable','m18_audit_capture','m18_incident_guard'))
            """;
        Assert.Equal(8L + 4L, await scratch.Scalar<long>(objects));

        // retained evidence: the real Down script raises and its own transaction removes nothing
        var down = new M18IncidentLifecycle().DownOperations.OfType<SqlOperation>().Select(x => x.Sql).ToList();
        Assert.NotEmpty(down);
        var ids = await SeedIncident(scratch);
        var before = await scratch.Scalar<long>(objects);
        await using (var connection = new NpgsqlConnection(scratch.Connection))
        {
            await connection.OpenAsync();
            foreach (var sql in down)
            {
                await using var tx = await connection.BeginTransactionAsync();
                await using var script = new NpgsqlCommand(sql, connection, tx);
                var failure = await Assert.ThrowsAsync<PostgresException>(() => script.ExecuteNonQueryAsync());
                Assert.Contains("Retained M18 incident evidence prevents downgrade", failure.MessageText);
                Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
                await tx.RollbackAsync();
            }
        }
        Assert.Equal(before, await scratch.Scalar<long>(objects));
        Assert.Equal(1L, await scratch.Scalar<long>($"SELECT count(*) FROM it_incidents WHERE id='{ids}'"));

        // without retained evidence the downgrade is clean and migration can be applied again
        await using (var connection = new NpgsqlConnection(scratch.Connection))
        {
            await connection.OpenAsync();
            await using var clear = new NpgsqlCommand("""
                ALTER TABLE it_incident_events DISABLE TRIGGER ALL;
                TRUNCATE it_incident_events;
                DELETE FROM it_incident_operation_ids;
                ALTER TABLE it_incident_events ENABLE TRIGGER ALL;
                ALTER TABLE it_incidents DISABLE TRIGGER ALL;
                DELETE FROM it_incidents;
                ALTER TABLE it_incidents ENABLE TRIGGER ALL;
                """, connection);
            await clear.ExecuteNonQueryAsync();
        }
        await using (var db = scratch.Context())
            await db.Database.GetService<IMigrator>().MigrateAsync(Previous);
        Assert.Equal(0L, await scratch.Scalar<long>(objects));
        Assert.Equal(1L, await scratch.Scalar<long>("SELECT count(*) FROM pg_proc WHERE proname='m16_audit_capture'"));
        await using (var db = scratch.Context()) await db.Database.MigrateAsync();
        Assert.Equal(8L + 4L, await scratch.Scalar<long>(objects));
    }

    private static async Task<Guid> SeedIncident(Scratch scratch)
    {
        var settings = new Dictionary<string, string> { ["M18:EnableIncidents"] = "true" };
        using var factory = new ReferenceDataHttpTests.ApiFactory(scratch.Connection, settings: settings);
        await using var scope = factory.Services.CreateAsyncScope();
        var ids = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var organizations = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var nonce = Guid.NewGuid().ToString("N");
        var email = $"downgrade-{nonce}@example.test";
        await ids.ProvisionAsync(email, "Downgrade Admin", M18World.Password, CancellationToken.None);
        var org = await organizations.BootstrapAsync("dg-" + nonce[..10], "Downgrade Org", email, CancellationToken.None);
        var user = (await ids.LoginAsync(email, M18World.Password, "downgrade", CancellationToken.None))!.UserId;
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(scratch.Connection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO it_incidents(id,organization_id,title,service_code,severity,impact_code,detected_at_utc,created_by)
            VALUES(@id,@org,'retained','api','SEV-3','x',now(),@user)
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("org", org.Id);
        command.Parameters.AddWithValue("user", user);
        await command.ExecuteNonQueryAsync();
        return id;
    }
}
