using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

/// <summary>
/// Migration 12 is an immutable historical migration. The NULL-safe projector scope function is
/// delivered by migration 13 with CREATE OR REPLACE, so a database that really executed the
/// original twelve migrations receives it on upgrade. These tests build that historical state by
/// applying only the original twelve migrations to an empty schema, never by reverting a
/// thirteen-migration database, and verify the function through the real harborops_projector login.
/// </summary>
public sealed class M10ProjectorScopeUpgradeTests
{
    private const string M10 = "20261105180000_ShipmentTrackingProjections";
    private const string Original = "RETURN organization_state='Active' AND shipment_state='Active';";
    private const string Corrected =
        "RETURN COALESCE(organization_state='Active' AND shipment_state='Active',false);";
    private const string Signature = "m10_lock_projector_scope(uuid,uuid)";

    private static HarborOpsDbContext Context(string connection) => new(
        new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(connection).Options);

    private static async Task<object?> Scalar(string connection, string sql,
        params (string, object)[] values)
    {
        await using var direct = new NpgsqlConnection(connection);
        await direct.OpenAsync();
        await using var command = new NpgsqlCommand(sql, direct);
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        return await command.ExecuteScalarAsync();
    }

    private static string ProjectorFor(M10RuntimeIdentityTests.Roles roles, string schema) =>
        new NpgsqlConnectionStringBuilder(roles.Projector) { SearchPath = schema + ",public" }
            .ConnectionString;

    private static string RoleFor(string connection, string schema) =>
        new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema + ",public" }
            .ConnectionString;

    private sealed record Deployed(string Body, string Attributes);

    private static async Task<Deployed> Function(string connection, string schema)
    {
        var oid = $"to_regprocedure('\"{schema}\".{Signature}')";
        var body = (string)(await Scalar(connection, $"SELECT pg_get_functiondef({oid})"))!;
        // Identity, owner, SECURITY DEFINER, pinned search_path, ACL and signature. A replacement
        // that dropped and recreated the function would change the oid and lose the grants.
        var attributes = (string)(await Scalar(connection, $"""
            SELECT concat_ws('|', p.oid::text, pg_get_userbyid(p.proowner), p.prosecdef::text,
                COALESCE(array_to_string(p.proconfig,','),''), COALESCE(p.proacl::text,''),
                pg_get_function_result(p.oid), p.provolatile::text, p.proisstrict::text,
                p.prolang::text, (SELECT COUNT(*) FROM aclexplode(p.proacl) a WHERE a.grantee=0)::text)
            FROM pg_proc p WHERE p.oid={oid}
            """))!;
        return new Deployed(body, attributes);
    }

    private static async Task GrantProjector(string scoped, string schema, string database)
    {
        // The committed grant script, with the database and schema retargeted. Its unqualified
        // object names resolve through the scoped search_path.
        var script = (await File.ReadAllTextAsync(M10RuntimeIdentityTests.RepositoryFile(
                "scripts/configure-m10-projector-db.sql")))
            .Replace("ON DATABASE harborops TO", $"ON DATABASE \"{database}\" TO",
                StringComparison.Ordinal)
            .Replace("GRANT USAGE ON SCHEMA public TO", $"GRANT USAGE ON SCHEMA \"{schema}\" TO",
                StringComparison.Ordinal);
        Assert.Contains($"SCHEMA \"{schema}\"", script);
        await M10UpgradeTests.Run(scoped, script);
    }

    private static Task<object?> ProjectionEffects(string connection) => Scalar(connection, """
        SELECT concat_ws('/',
          (SELECT md5(COALESCE(string_agg(x::text,'|' ORDER BY x::text),'')) FROM shipment_tracking_projections x),
          (SELECT md5(COALESCE(string_agg(x::text,'|' ORDER BY x::text),'')) FROM shipment_projection_applications x),
          (SELECT md5(COALESCE(string_agg(x::text,'|' ORDER BY x::text),'')) FROM shipment_tracking_timeline_entries x),
          (SELECT md5(COALESCE(string_agg(x::text,'|' ORDER BY x::text),'')) FROM organizations x),
          (SELECT md5(COALESCE(string_agg(x::text,'|' ORDER BY x::text),'')) FROM shipments x))
        """);

    private static Task<object?> Scope(string projector, Guid org, Guid shipment) =>
        Scalar(projector, "SELECT m10_lock_projector_scope(@o,@s)", ("o", org), ("s", shipment));

    [Fact]
    public async Task FreshInstallAppliesThirteenMigrationsWithTheNullSafeProjectorScopeFunction()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        var database = new NpgsqlConnectionStringBuilder(roles.Admin).Database!;
        var schema = "m10_p13_" + Guid.NewGuid().ToString("N");
        var scoped = M10UpgradeTests.Scoped(schema);
        await using var admin = new NpgsqlConnection(TestDatabase.ConnectionString());
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = Context(scoped);
            await db.Database.MigrateAsync();
            Assert.Equal(15, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());

            var fresh = await Function(scoped, schema);
            Assert.Contains(Corrected, fresh.Body);
            Assert.DoesNotContain(Original, fresh.Body);
            var parts = fresh.Attributes.Split('|');
            Assert.Equal("true", parts[2]);                        // SECURITY DEFINER
            Assert.Equal("search_path=pg_catalog", parts[3]);      // pinned search_path
            Assert.Equal("0", parts[^1]);                          // no PUBLIC execute entry
            Assert.Equal(await Scalar(scoped, "SELECT current_user"), parts[1]);

            await GrantProjector(scoped, schema, database);
            var projector = ProjectorFor(roles, schema);
            Assert.Equal("projector", await Scalar(projector,
                "SELECT 'projector' WHERE session_user='harborops_projector'") as string);
            // Absent scope is an explicit false, never NULL.
            Assert.Equal(false, await Scope(projector, Guid.NewGuid(), Guid.NewGuid()));
            // Any session other than the projector is told false by the definer function.
            Assert.Equal(false, await Scope(scoped, Guid.NewGuid(), Guid.NewGuid()));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task GenuineTwelveMigrationDatabaseKeepsTheOriginalFunctionUntilMigrationThirteenThenIsNullSafeAndIdempotent()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        var database = new NpgsqlConnectionStringBuilder(roles.Admin).Database!;
        var schema = "m10_p12_" + Guid.NewGuid().ToString("N");
        var scoped = M10UpgradeTests.Scoped(schema);
        var projector = ProjectorFor(roles, schema);
        await using var admin = new NpgsqlConnection(TestDatabase.ConnectionString());
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            // Apply exactly the original twelve migrations to an empty schema. Migration 13 has
            // never run, so the function below is the one migration 12 itself created.
            await using var db = Context(scoped);
            await db.Database.MigrateAsync(M10);
            Assert.Equal(12, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(false, await Scalar(scoped,
                $"SELECT to_regclass('\"{schema}\".m10_correction_proof_keys') IS NOT NULL"));
            var historical = await Function(scoped, schema);
            Assert.Contains(Original, historical.Body);
            Assert.DoesNotContain("COALESCE", historical.Body);

            // Representative populated M8/M9/M10 history on the twelve-migration schema.
            using var factory = new ReferenceDataHttpTests.ApiFactory(scoped);
            var fixture = await ReferenceDataHttpTests.Setup(factory, migrateToHead: false);
            using var a = fixture.Admin;
            using var m = fixture.Manager;
            using var n = fixture.Analyst;
            var target = await G9Support.SeedResolvable(factory, fixture);
            var archivedTarget = await G9Support.SeedResolvable(factory, fixture);
            var source = await G9Support.NewSource(factory, fixture.A, "m10p12");
            var prefix = Guid.NewGuid().ToString("N")[..8];
            var receipts = new List<Guid>();
            foreach (var (key, at, code) in new[]
            {
                ("p12-1", "2026-11-03T10:00:00Z", "GTIN"),
                ("p12-2", "2026-11-03T11:00:00Z", "GTOT")
            })
                receipts.Add(await G9Support.Accept(factory, source,
                    G9Support.EquipmentPayload(key + "-" + prefix, target.Reference, at, code)));
            Guid actor;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var work = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
                var worker = new TrackingWorkStore(work, new TrackingWorkerSettings());
                for (var i = 0; i < 2; i++)
                {
                    var claim = await worker.ClaimOneAsync(fixture.A, CancellationToken.None);
                    Assert.NotNull(claim);
                    Assert.Equal(TrackingWorkDisposition.Processed,
                        await worker.FinalizeAsync(claim!, CancellationToken.None));
                }
                actor = (await work.AuthUsers.AsNoTracking().SingleAsync(u => u.NormalizedEmail ==
                    HarborOps.Infrastructure.Identity.IdentityStore.NormalizeEmail(
                        fixture.ManagerEmail))).Id;
            }
            // Historical correction evidence written the way the twelve-migration source did.
            var digest = new string('7', 64);
            await using (var connection = new Npgsql.NpgsqlConnection(scoped))
            {
                await connection.OpenAsync();
                await using var insert = new NpgsqlCommand("""
                    INSERT INTO shipment_tracking_corrections
                        (id,organization_id,shipment_id,target_receipt_id,replacement_receipt_id,
                         generation,actor_user_id,reason,correlation_id,correction_digest)
                    VALUES (gen_random_uuid(),@o,@s,@t,NULL,1,@u,'Correction written before authority','legacy',@d);
                    INSERT INTO shipment_tracking_operation_replays
                        (organization_id,shipment_id,actor_user_id,idempotency_key,operation_kind,
                         request_digest,response_revision,response_generation)
                    VALUES (@o,@s,@u,gen_random_uuid(),'Correction',@d,
                        (SELECT input_revision FROM shipment_tracking_input_versions
                         WHERE organization_id=@o AND shipment_id=@s),1);
                    """, connection);
                insert.Parameters.AddWithValue("o", fixture.A);
                insert.Parameters.AddWithValue("s", target.Shipment);
                insert.Parameters.AddWithValue("t", receipts[0]);
                insert.Parameters.AddWithValue("u", actor);
                insert.Parameters.AddWithValue("d", digest);
                await insert.ExecuteNonQueryAsync();
            }
            Assert.Equal(1L, await Scalar(scoped, "SELECT COUNT(*) FROM shipment_tracking_corrections"));
            Assert.Equal(2L, await Scalar(scoped, "SELECT COUNT(*) FROM shipment_tracking_timeline_entries"));
            // Archive the second shipment through the accepted route: close its allocation first.
            var assignmentId = (Guid)(await Scalar(scoped,
                "SELECT id FROM shipment_container_assignments WHERE shipment_id=@s AND ends_at_utc IS NULL",
                ("s", archivedTarget.Shipment)))!;
            using (var close = await ReferenceDataHttpTests.Send(fixture.Analyst, HttpMethod.Patch,
                $"/api/v1/organizations/{fixture.A}/shipments/{archivedTarget.Shipment}/container-assignments/{assignmentId}",
                new { expectedVersion = 1L, endsAtUtc = "2026-11-10T12:00:00Z" }))
                Assert.Equal(System.Net.HttpStatusCode.OK, close.StatusCode);
            var shipmentVersion = (long)(await Scalar(scoped,
                "SELECT version FROM shipments WHERE id=@s", ("s", archivedTarget.Shipment)))!;
            using (var archive = await ReferenceDataHttpTests.Send(fixture.Manager, HttpMethod.Delete,
                $"/api/v1/organizations/{fixture.A}/shipments/{archivedTarget.Shipment}",
                new { expectedVersion = shipmentVersion }))
                Assert.Equal(System.Net.HttpStatusCode.NoContent, archive.StatusCode);
            Assert.Equal("Archived", await Scalar(scoped,
                "SELECT record_status FROM shipments WHERE id=@s", ("s", archivedTarget.Shipment)));

            // The operator-run projector grants were applied to this twelve-migration database.
            await GrantProjector(scoped, schema, database);
            var preAttributes = (await Function(scoped, schema)).Attributes;
            Assert.Contains("harborops_projector=X/", preAttributes);

            // Behavior before the upgrade: the original function returns NULL for an absent shipment.
            Assert.Equal(true, await Scope(projector, fixture.A, target.Shipment));
            Assert.Equal(false, await Scope(projector, fixture.A, archivedTarget.Shipment));
            Assert.Same(DBNull.Value, await Scope(projector, fixture.A, Guid.NewGuid()));

            const string HistoryRows = """
                SELECT md5(string_agg("MigrationId"||'/'||"ProductVersion",'|' ORDER BY "MigrationId"))
                FROM "__EFMigrationsHistory" WHERE "MigrationId" < '20261107120000'
                """;
            var historyBefore = await Scalar(scoped, HistoryRows);
            var evidenceBefore = await M10UpgradeTests.Fingerprint(scoped);
            var m10Before = await M10UpgradeTests.M10Fingerprint(scoped);
            var effectsBefore = await ProjectionEffects(scoped);
            var auditBefore = await Scalar(scoped, "SELECT COUNT(*) FROM audit_entries");

            // Upgrade the genuinely historical database.
            await db.Database.MigrateAsync();
            Assert.Equal(15, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(historyBefore, await Scalar(scoped, HistoryRows));
            Assert.Equal(evidenceBefore, await M10UpgradeTests.Fingerprint(scoped));
            Assert.Equal(m10Before, await M10UpgradeTests.M10Fingerprint(scoped));
            Assert.Equal(effectsBefore, await ProjectionEffects(scoped));
            Assert.Equal(auditBefore, await Scalar(scoped, "SELECT COUNT(*) FROM audit_entries"));

            var upgraded = await Function(scoped, schema);
            Assert.Contains(Corrected, upgraded.Body);
            Assert.DoesNotContain(Original, upgraded.Body);
            // Same function object, owner, SECURITY DEFINER, search_path, ACL and signature.
            Assert.Equal(preAttributes, upgraded.Attributes);

            // Restricted projector behavior after the upgrade.
            Assert.Equal(true, await Scope(projector, fixture.A, target.Shipment));
            Assert.Equal(false, await Scope(projector, fixture.A, archivedTarget.Shipment));
            Assert.Equal(false, await Scope(projector, fixture.A, Guid.NewGuid()));
            Assert.Equal(false, await Scope(projector, Guid.NewGuid(), Guid.NewGuid()));
            // Another tenant's organization with this tenant's shipment is not lockable.
            Assert.Equal(false, await Scope(projector, fixture.B, target.Shipment));
            // Calling the function created no projection, application or evidence rows.
            Assert.Equal(effectsBefore, await ProjectionEffects(scoped));
            Assert.Equal(evidenceBefore, await M10UpgradeTests.Fingerprint(scoped));
            // The status changes below are legitimate audited business writes, so they come last.
            await M10UpgradeTests.Run(scoped,
                $"UPDATE organizations SET status='Suspended' WHERE id='{fixture.A}'");
            Assert.Equal(false, await Scope(projector, fixture.A, target.Shipment));
            await M10UpgradeTests.Run(scoped,
                $"UPDATE organizations SET status='Active' WHERE id='{fixture.A}'");
            Assert.Equal(true, await Scope(projector, fixture.A, target.Shipment));
            // The definer function refuses every other session.
            Assert.Equal(false, await Scope(scoped, fixture.A, target.Shipment));

            // Idempotency: running the migrator again changes nothing.
            await db.Database.MigrateAsync();
            Assert.Equal(15, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(upgraded, await Function(scoped, schema));

            // Reversible without retained authority evidence: the downgrade restores the exact
            // original definition on the same function object and the upgrade re-applies.
            await db.Database.MigrateAsync(M10);
            Assert.Equal(12, (await db.Database.GetAppliedMigrationsAsync()).Count());
            var restored = await Function(scoped, schema);
            Assert.Equal(historical.Body.Replace("\r", ""), restored.Body.Replace("\r", ""));
            Assert.Equal(preAttributes, restored.Attributes);
            Assert.Same(DBNull.Value, await Scope(projector, fixture.A, Guid.NewGuid()));
            Assert.Equal(m10Before, await M10UpgradeTests.M10Fingerprint(scoped));
            await db.Database.MigrateAsync();
            Assert.Equal(upgraded, await Function(scoped, schema));

            // Retained correction-authority evidence still blocks the downgrade and leaves the
            // corrected function in place.
            await M10UpgradeTests.Run(scoped, """
                INSERT INTO m10_correction_proof_nonces
                    (nonce,organization_id,shipment_id,actor_user_id,idempotency_key,
                     request_digest,expires_at_utc)
                VALUES (gen_random_uuid(),gen_random_uuid(),gen_random_uuid(),gen_random_uuid(),
                        gen_random_uuid(),repeat('a',64),clock_timestamp())
                """);
            var blocked = await Assert.ThrowsAsync<PostgresException>(() =>
                db.Database.MigrateAsync(M10));
            Assert.Equal(PostgresErrorCodes.CheckViolation, blocked.SqlState);
            Assert.Contains("Retained correction authority evidence", blocked.MessageText);
            Assert.Equal(13, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(upgraded, await Function(scoped, schema));
            Assert.Equal(false, await Scope(projector, fixture.A, Guid.NewGuid()));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task OrdinaryRuntimeIdentitiesCannotExecuteReplaceOrBroadenTheProjectorScopeAndSigningFunctions()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        var scope = $"public.{Signature}";
        var signed = "public.m10_apply_signed_correction(uuid,uuid,uuid,uuid,bigint,uuid,uuid,varchar,varchar,varchar,uuid,uuid,bigint,varchar)";

        async Task<bool> Can(string role, string function) => (bool)(await Scalar(roles.Admin,
            "SELECT has_function_privilege(@r, @f, 'EXECUTE')", ("r", role), ("f", function)))!;
        Assert.True(await Can("harborops_projector", scope));
        Assert.False(await Can("harborops_api", scope));
        Assert.False(await Can("harborops_worker", scope));
        Assert.True(await Can("harborops_api", signed));
        Assert.False(await Can("harborops_worker", signed));
        Assert.False(await Can("harborops_projector", signed));

        var deployed = await Function(roles.Admin, "public");
        var parts = deployed.Attributes.Split('|');
        Assert.Equal("true", parts[2]);
        Assert.Equal("search_path=pg_catalog", parts[3]);
        Assert.Equal("0", parts[^1]);
        Assert.DoesNotContain("harborops_api", parts[1]);
        Assert.DoesNotContain("harborops_worker", parts[1]);
        Assert.DoesNotContain("harborops_projector", parts[1]);
        Assert.Contains(Corrected, deployed.Body);

        var shipment = Guid.NewGuid();
        var organization = Guid.NewGuid();
        foreach (var (label, connection) in new[] { ("api", roles.Api), ("worker", roles.Worker) })
        {
            await using var direct = new NpgsqlConnection(connection);
            await direct.OpenAsync();
            await using var call = new NpgsqlCommand(
                "SELECT m10_lock_projector_scope(@o,@s)", direct);
            call.Parameters.AddWithValue("o", organization);
            call.Parameters.AddWithValue("s", shipment);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => call.ExecuteScalarAsync());
            Assert.True(failure.SqlState == PostgresErrorCodes.InsufficientPrivilege,
                $"{label} must not execute the projector scope function: {failure.SqlState}");
        }
        // No runtime identity can redefine either function or the replaced object's ownership.
        foreach (var (label, connection) in new[]
                 { ("api", roles.Api), ("worker", roles.Worker), ("projector", roles.Projector) })
        {
            await using var direct = new NpgsqlConnection(connection);
            await direct.OpenAsync();
            await using var replace = new NpgsqlCommand("""
                CREATE OR REPLACE FUNCTION public.m10_lock_projector_scope(p_org uuid,p_shipment uuid)
                RETURNS boolean LANGUAGE sql SECURITY DEFINER AS $$ SELECT true $$
                """, direct);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => replace.ExecuteNonQueryAsync());
            Assert.True(failure.SqlState == PostgresErrorCodes.InsufficientPrivilege,
                $"{label} must not replace the function: {failure.SqlState}");
            // A non-owner GRANT is refused or is a no-op warning; the final comparison proves
            // that the ACL did not change either way.
            await using var grant = new NpgsqlCommand(
                "GRANT EXECUTE ON FUNCTION public.m10_lock_projector_scope(uuid,uuid) TO PUBLIC", direct);
            try { await grant.ExecuteNonQueryAsync(); }
            catch (PostgresException refused)
            {
                Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
            }
        }
        Assert.Equal(deployed, await Function(roles.Admin, "public"));
    }
}
