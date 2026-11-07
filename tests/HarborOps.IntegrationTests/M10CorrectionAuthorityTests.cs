using System.Security.Cryptography;
using System.Text;
using HarborOps.Api.Audit;
using HarborOps.Api.Shipments;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.Shipments;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

/// <summary>
/// R05, R07, R08 and R14 of the M10 correction authority remediation. Every denial is exercised
/// through a real harborops_api login, so the checks prove the database boundary and not a mock.
/// </summary>
public sealed partial class M10RuntimeIdentityTests
{
    private const string ApplySql = """
        SELECT m10_apply_signed_correction(
            @org::uuid,@shipment::uuid,@target::uuid,@replacement::uuid,
            @generation::bigint,@actor::uuid,@session::uuid,
            @reason::varchar,@correlation::varchar,@digest::varchar,
            @key::uuid,@nonce::uuid,@expires::bigint,@signature::varchar)
        """;

    private sealed record AuthorityCall(Guid Org, Guid Shipment, Guid Target, Guid? Replacement,
        long Generation, Guid Actor, Guid Session, string Reason, string Correlation,
        string Digest, Guid Key, Guid? Nonce, long Expires, string? Signature);

    private static (string, object)[] Bind(AuthorityCall c) => new (string, object)[]
    {
        ("org", c.Org), ("shipment", c.Shipment), ("target", c.Target),
        ("replacement", (object?)c.Replacement ?? DBNull.Value),
        ("generation", c.Generation), ("actor", c.Actor), ("session", c.Session),
        ("reason", c.Reason), ("correlation", c.Correlation), ("digest", c.Digest),
        ("key", c.Key), ("nonce", (object?)c.Nonce ?? DBNull.Value),
        ("expires", c.Expires), ("signature", (object?)c.Signature ?? DBNull.Value)
    };

    // The wire format is spelled out independently of M10CorrectionProofSigner so that a
    // drift between the signer and the SQL verifier cannot hide behind shared code.
    private static string SignIndependently(string keyBase64, Guid nonce, long expires,
        Guid key, Guid session, string digest)
    {
        var message = $"m10-correction-v1|{nonce:N}|{expires}|{key:N}|{session:N}|{digest}";
        return Convert.ToHexString(HMACSHA256.HashData(Convert.FromBase64String(keyBase64),
            Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
    }

    private static AuthorityCall Genuine(Seeded s, Guid target, Guid actor, Guid session,
        long expectedGeneration, string reason, Guid? replacement = null,
        string? keyBase64 = null)
    {
        var correction = new TrackingCorrection(target, replacement, expectedGeneration, reason);
        var key = Guid.NewGuid();
        var proof = M10CorrectionProofSigner.Sign(keyBase64 ?? ReferenceDataHttpTests.CorrectionProofKey,
            s.Fixture.A, s.Target.Shipment, actor, session, key, correction);
        Assert.NotNull(proof);
        var digest = ShipmentTimelineStore.RequestDigest(s.Fixture.A, s.Target.Shipment, actor, correction);
        Assert.Equal(SignIndependently(keyBase64 ?? ReferenceDataHttpTests.CorrectionProofKey,
            proof!.Nonce, proof.ExpiresEpoch, key, session, digest), proof.Signature);
        return new AuthorityCall(s.Fixture.A, s.Target.Shipment, target, replacement,
            expectedGeneration + 1, actor, session, reason, "authority-probe", digest, key,
            proof.Nonce, proof.ExpiresEpoch, proof.Signature);
    }

    private static async Task<Guid> LatestSession(string admin, Guid user, Guid org)
    {
        await using var db = Context(admin);
        return await db.AuthSessions.AsNoTracking()
            .Where(x => x.UserId == user && x.SelectedOrganizationId == org && x.RevokedAtUtc == null)
            .OrderByDescending(x => x.IssuedAtUtc).Select(x => x.Id).FirstAsync();
    }

    private static async Task<Guid> UserId(string admin, string email)
    {
        await using var db = Context(admin);
        return (await db.AuthUsers.AsNoTracking().SingleAsync(x => x.NormalizedEmail ==
            HarborOps.Infrastructure.Identity.IdentityStore.NormalizeEmail(email))).Id;
    }

    private static async Task AssertNoCorrectionEffects(Seeded s, string label)
    {
        foreach (var (name, sql) in new[]
        {
            ("corrections", "SELECT COUNT(*) FROM shipment_tracking_corrections WHERE organization_id=@o"),
            ("nonces", "SELECT COUNT(*) FROM m10_correction_proof_nonces WHERE organization_id=@o"),
            ("replays", "SELECT COUNT(*) FROM shipment_tracking_operation_replays WHERE organization_id=@o AND operation_kind='Correction'"),
            ("audit", "SELECT COUNT(*) FROM audit_entries WHERE organization_id=@o AND event_type='shipment.tracking_correction_created'")
        })
            Assert.True(0 == await Count(s.Roles.Admin, sql, ("o", s.Fixture.A)),
                $"{label}: unexpected persisted {name}");
    }

    private static async Task AssertForbiddenCall(Seeded s, string label, AuthorityCall call)
    {
        var failure = await Denied(s.Roles.Api, ApplySql, Bind(call));
        Assert.True(failure.SqlState == PostgresErrorCodes.InsufficientPrivilege,
            $"{label}: expected 42501 but got {failure.SqlState} {failure.MessageText}");
    }

    [Fact]
    public async Task EveryProofBindingAndInvalidProofIsDeniedWithoutEffectsOnTheRestrictedLogin()
    {
        var s = await SeedAsync("g10-bind");
        using var _ = s.Admin;
        using var __ = s.Fixture.Admin;
        using var ___ = s.Fixture.Manager;
        using var ____ = s.Fixture.Analyst;
        var target = await AcceptAsync(s, "bind-1");
        var other = await AcceptAsync(s, "bind-2", "2026-11-03T11:00:00Z", "GTOT");
        for (var n = 0; n < 2; n++)
            Assert.Equal(TrackingWorkDisposition.Processed, await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));
        var session = await LatestSession(s.Roles.Admin, s.Actor, s.Fixture.A);
        var analystUser = await UserId(s.Roles.Admin, s.Fixture.AnalystEmail);
        var genuine = Genuine(s, target, s.Actor, session, 0, "Binding probe reason");

        // R07: altering any single bound element invalidates authority.
        await AssertForbiddenCall(s, "tenant", genuine with { Org = s.Fixture.B });
        await AssertForbiddenCall(s, "shipment", genuine with { Shipment = Guid.NewGuid() });
        await AssertForbiddenCall(s, "actor", genuine with { Actor = analystUser });
        await AssertForbiddenCall(s, "session", genuine with { Session = Guid.NewGuid() });
        await AssertForbiddenCall(s, "target", genuine with { Target = other });
        await AssertForbiddenCall(s, "replacement", genuine with { Replacement = other });
        await AssertForbiddenCall(s, "reason", genuine with { Reason = "Substituted reason text" });
        await AssertForbiddenCall(s, "generation", genuine with { Generation = genuine.Generation + 1 });
        await AssertForbiddenCall(s, "idempotency key", genuine with { Key = Guid.NewGuid() });
        await AssertForbiddenCall(s, "nonce", genuine with { Nonce = Guid.NewGuid() });
        await AssertForbiddenCall(s, "expiry", genuine with { Expires = genuine.Expires + 1 });
        await AssertForbiddenCall(s, "digest", genuine with { Digest = new string('f', 64) });
        // R05: missing, malformed, truncated, expired and far-future proofs.
        await AssertForbiddenCall(s, "no nonce", genuine with { Nonce = null });
        await AssertForbiddenCall(s, "no signature", genuine with { Signature = null });
        await AssertForbiddenCall(s, "empty signature", genuine with { Signature = "" });
        await AssertForbiddenCall(s, "malformed signature", genuine with { Signature = "not-a-signature" });
        await AssertForbiddenCall(s, "truncated signature",
            genuine with { Signature = genuine.Signature![..32] });
        await AssertForbiddenCall(s, "uppercase signature",
            genuine with { Signature = genuine.Signature!.ToUpperInvariant() });
        var past = DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeSeconds();
        await AssertForbiddenCall(s, "expired but correctly signed", genuine with
        {
            Expires = past,
            Signature = SignIndependently(ReferenceDataHttpTests.CorrectionProofKey,
                genuine.Nonce!.Value, past, genuine.Key, genuine.Session, genuine.Digest)
        });
        var distant = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        await AssertForbiddenCall(s, "far-future expiry correctly signed", genuine with
        {
            Expires = distant,
            Signature = SignIndependently(ReferenceDataHttpTests.CorrectionProofKey,
                genuine.Nonce!.Value, distant, genuine.Key, genuine.Session, genuine.Digest)
        });
        var foreignKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await AssertForbiddenCall(s, "signed with a different key",
            Genuine(s, target, s.Actor, session, 0, "Binding probe reason", null, foreignKey));
        // Neither the worker nor the projector identity can execute the operation at all.
        foreach (var role in new[] { s.Roles.Worker, s.Roles.Projector })
            await AssertPermissionDenied(role, "non-api identity", ApplySql, Bind(genuine));
        await AssertNoCorrectionEffects(s, "denied proofs");

        // The genuine request is accepted exactly once through the shipped store and
        // its nonce, signature and key never reach audit metadata or the correction row.
        await using (var db = Context(s.Roles.Api))
        {
            var store = new ShipmentTimelineStore(db);
            var proof = new TrackingCorrectionProof(genuine.Nonce!.Value, genuine.Expires, genuine.Signature!);
            var applied = await store.CorrectForSessionAsync(s.Fixture.A, s.Target.Shipment,
                new TrackingCorrection(target, null, 0, genuine.Reason), s.Actor, session,
                genuine.Correlation, genuine.Key, proof, CancellationToken.None);
            Assert.Equal(TrackingMutationResult.Applied, applied.Result);
        }
        Assert.Equal(1, await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM shipment_tracking_corrections WHERE organization_id=@o", ("o", s.Fixture.A)));
        Assert.Equal(1, await Count(s.Roles.Admin, """
            SELECT COUNT(*) FROM audit_entries WHERE organization_id=@o
              AND event_type='shipment.tracking_correction_created' AND outcome='Succeeded'
              AND actor_session_id=@session AND actor_role='Manager'
            """, ("o", s.Fixture.A), ("session", session)));
        Assert.Equal(0, await Count(s.Roles.Admin, """
            SELECT COUNT(*) FROM audit_entries
            WHERE metadata::text LIKE '%' || @signature || '%'
               OR metadata::text LIKE '%' || @reason || '%'
               OR metadata::text LIKE '%' || @nonce || '%'
            """, ("signature", genuine.Signature!), ("reason", genuine.Reason),
            ("nonce", genuine.Nonce!.Value.ToString())));

        // R05 replay: the consumed nonce cannot be reused, even by a verbatim call.
        var replay = await Denied(s.Roles.Api, ApplySql, Bind(genuine));
        Assert.Contains(replay.SqlState, new[] { PostgresErrorCodes.UniqueViolation,
            PostgresErrorCodes.InsufficientPrivilege, PostgresErrorCodes.CheckViolation });
        Assert.Equal(1, await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM shipment_tracking_corrections WHERE organization_id=@o", ("o", s.Fixture.A)));
        Assert.Equal(1, await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM m10_correction_proof_nonces WHERE organization_id=@o", ("o", s.Fixture.A)));
        Assert.Equal(1, await Count(s.Roles.Admin, """
            SELECT COUNT(*) FROM audit_entries WHERE organization_id=@o
              AND event_type='shipment.tracking_correction_created'
            """, ("o", s.Fixture.A)));
    }

    [Fact]
    public async Task AuthorityIsDeniedForAnalystDowngradedRevokedAndExpiredSessions()
    {
        var s = await SeedAsync("g10-who");
        using var _ = s.Admin;
        using var admin = s.Fixture.Admin;
        using var ___ = s.Fixture.Manager;
        using var analyst = s.Fixture.Analyst;
        var target = await AcceptAsync(s, "who-1");
        Assert.Equal(TrackingWorkDisposition.Processed, await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));
        var analystUser = await UserId(s.Roles.Admin, s.Fixture.AnalystEmail);
        await ReferenceDataHttpTests.Login(analyst, s.Fixture.AnalystEmail);
        await ReferenceDataHttpTests.Select(analyst, s.Fixture.A);
        var analystSession = await LatestSession(s.Roles.Admin, analystUser, s.Fixture.A);
        await AssertForbiddenCall(s, "analyst with a properly signed request",
            Genuine(s, target, analystUser, analystSession, 0, "Analyst attempt reason"));

        var managerSession = await LatestSession(s.Roles.Admin, s.Actor, s.Fixture.A);
        var call = Genuine(s, target, s.Actor, managerSession, 0, "Manager state probe");

        // Expired idle window.
        await ExecuteAdmin(s.Roles.Admin,
            """
            UPDATE auth_sessions SET issued_at_utc=issued_at_utc - interval '30 days',
                last_seen_at_utc=last_seen_at_utc - interval '30 days',
                idle_expires_at_utc=idle_expires_at_utc - interval '30 days',
                absolute_expires_at_utc=absolute_expires_at_utc - interval '30 days' WHERE id=@id
            """,
            ("id", managerSession));
        await AssertForbiddenCall(s, "expired session", call);
        await ExecuteAdmin(s.Roles.Admin,
            """
            UPDATE auth_sessions SET issued_at_utc=issued_at_utc + interval '30 days',
                last_seen_at_utc=last_seen_at_utc + interval '30 days',
                idle_expires_at_utc=idle_expires_at_utc + interval '30 days',
                absolute_expires_at_utc=absolute_expires_at_utc + interval '30 days' WHERE id=@id
            """,
            ("id", managerSession));
        // Revoked session.
        await ExecuteAdmin(s.Roles.Admin,
            "UPDATE auth_sessions SET revoked_at_utc = clock_timestamp() WHERE id=@id", ("id", managerSession));
        await AssertForbiddenCall(s, "revoked session", call);
        await ExecuteAdmin(s.Roles.Admin,
            "UPDATE auth_sessions SET revoked_at_utc = NULL WHERE id=@id", ("id", managerSession));
        // Security-version drift (password change, forced sign-out).
        await ExecuteAdmin(s.Roles.Admin,
            "UPDATE auth_sessions SET security_version = security_version + 1 WHERE id=@id", ("id", managerSession));
        await AssertForbiddenCall(s, "stale security version", call);
        await ExecuteAdmin(s.Roles.Admin,
            "UPDATE auth_sessions SET security_version = security_version - 1 WHERE id=@id", ("id", managerSession));
        // Downgraded and later reinstated Manager.
        async Task SetRole(string role)
        {
            await using var db = Context(s.Roles.Admin);
            var membership = await db.OrganizationMemberships.AsNoTracking().SingleAsync(m =>
                m.OrganizationId == s.Fixture.A && m.UserId == s.Actor);
            using var put = await ReferenceDataHttpTests.Send(admin, HttpMethod.Put,
                $"/api/v1/organizations/{s.Fixture.A}/memberships/{s.Actor}",
                new { role, status = "Active", expectedVersion = membership.Version });
            Assert.Equal(System.Net.HttpStatusCode.OK, put.StatusCode);
        }
        await SetRole("Analyst");
        await AssertForbiddenCall(s, "downgraded Manager", call);
        await SetRole("Manager");
        await AssertNoCorrectionEffects(s, "state matrix");
    }

    private static async Task ExecuteAdmin(string admin, string sql, params (string, object)[] values)
    {
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task RevocationAndDowngradeRacingASignedCorrectionKeepTheLockOrderAndNeverCommitAfterRevocation()
    {
        var s = await SeedAsync("g10-race");
        using var _ = s.Admin;
        using var __ = s.Fixture.Admin;
        using var ___ = s.Fixture.Manager;
        using var ____ = s.Fixture.Analyst;
        var target = await AcceptAsync(s, "race-1");
        Assert.Equal(TrackingWorkDisposition.Processed, await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));
        var session = await LatestSession(s.Roles.Admin, s.Actor, s.Fixture.A);

        async Task<(TrackingMutationResult Result, long Revision)> StoreCorrection(
            string reason, TimeSpan timeout)
        {
            var correction = new TrackingCorrection(target, null, 0, reason);
            var key = Guid.NewGuid();
            var proof = M10CorrectionProofSigner.Sign(ReferenceDataHttpTests.CorrectionProofKey,
                s.Fixture.A, s.Target.Shipment, s.Actor, session, key, correction)!;
            await using var db = Context(s.Roles.Api);
            return await new ShipmentTimelineStore(db).CorrectForSessionAsync(s.Fixture.A,
                s.Target.Shipment, correction, s.Actor, session, "race", key, proof,
                CancellationToken.None).WaitAsync(timeout);
        }

        // Case 1: a revocation is in flight (uncommitted) when the correction arrives. The
        // correction waits on the session row, and once the revocation commits it is denied.
        await using (var revoker = new NpgsqlConnection(s.Roles.Admin))
        {
            await revoker.OpenAsync();
            await using var transaction = await revoker.BeginTransactionAsync();
            await using var revoke = new NpgsqlCommand(
                "UPDATE auth_sessions SET revoked_at_utc=clock_timestamp() WHERE id=@id", revoker, transaction);
            revoke.Parameters.AddWithValue("id", session);
            Assert.Equal(1, await revoke.ExecuteNonQueryAsync());
            var pending = Task.Run(() => StoreCorrection("Correction racing a revocation", TimeSpan.FromSeconds(60)));
            Assert.True(await WaitForLockWait(s.Roles.Admin, "harborops_api", TimeSpan.FromSeconds(30)),
                "The correction did not wait behind the in-flight revocation");
            Assert.False(pending.IsCompleted);
            await transaction.CommitAsync();
            var outcome = await pending;
            Assert.Equal(TrackingMutationResult.Forbidden, outcome.Result);
        }
        await AssertNoCorrectionEffects(s, "revocation race");
        await ExecuteAdmin(s.Roles.Admin,
            "UPDATE auth_sessions SET revoked_at_utc=NULL WHERE id=@id", ("id", session));

        // Case 2: a role downgrade is in flight when the correction arrives.
        var membershipId = await Scalar(s.Roles.Admin, """
            SELECT id FROM organization_memberships WHERE organization_id=@o AND user_id=@u
            """, ("o", s.Fixture.A), ("u", s.Actor));
        await using (var downgrader = new NpgsqlConnection(s.Roles.Admin))
        {
            await downgrader.OpenAsync();
            await using var transaction = await downgrader.BeginTransactionAsync();
            await using var downgrade = new NpgsqlCommand(
                "UPDATE organization_memberships SET role='Analyst' WHERE id=@id", downgrader, transaction);
            downgrade.Parameters.AddWithValue("id", membershipId!);
            Assert.Equal(1, await downgrade.ExecuteNonQueryAsync());
            var pending = Task.Run(() => StoreCorrection("Correction racing a downgrade", TimeSpan.FromSeconds(60)));
            Assert.True(await WaitForLockWait(s.Roles.Admin, "harborops_api", TimeSpan.FromSeconds(30)),
                "The correction did not wait behind the in-flight downgrade");
            await transaction.CommitAsync();
            Assert.Equal(TrackingMutationResult.Forbidden, (await pending).Result);
        }
        await AssertNoCorrectionEffects(s, "downgrade race");
        await ExecuteAdmin(s.Roles.Admin,
            "UPDATE organization_memberships SET role='Manager' WHERE id=@id", ("id", membershipId!));

        // Case 3: the correction commits first and a later revocation queues behind its locks.
        // The correction was authorized at its commit instant, the revocation then takes effect.
        var heldCorrection = new TrackingCorrection(target, null, 0, "Correction ahead of a revocation");
        var heldKey = Guid.NewGuid();
        var heldProof = M10CorrectionProofSigner.Sign(ReferenceDataHttpTests.CorrectionProofKey,
            s.Fixture.A, s.Target.Shipment, s.Actor, session, heldKey, heldCorrection)!;
        await using (var api = new NpgsqlConnection(s.Roles.Api))
        {
            await api.OpenAsync();
            await using var transaction = await api.BeginTransactionAsync();
            var call = new AuthorityCall(s.Fixture.A, s.Target.Shipment, target, null, 1, s.Actor, session,
                heldCorrection.Reason, "race",
                ShipmentTimelineStore.RequestDigest(s.Fixture.A, s.Target.Shipment, s.Actor, heldCorrection),
                heldKey, heldProof.Nonce, heldProof.ExpiresEpoch, heldProof.Signature);
            await using var apply = new NpgsqlCommand(ApplySql, api, transaction);
            foreach (var (name, value) in Bind(call)) apply.Parameters.AddWithValue(name, value);
            await apply.ExecuteNonQueryAsync();
            var revocation = Task.Run(() => ExecuteAdmin(s.Roles.Admin,
                "UPDATE auth_sessions SET revoked_at_utc=clock_timestamp() WHERE id=@id", ("id", session)));
            Assert.True(await WaitForLockWait(s.Roles.Admin, "harborops", TimeSpan.FromSeconds(30)),
                "The revocation did not queue behind the in-flight signed correction");
            Assert.False(revocation.IsCompleted);
            await transaction.CommitAsync();
            await revocation.WaitAsync(TimeSpan.FromSeconds(60));
        }
        Assert.Equal(1, await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM shipment_tracking_corrections WHERE organization_id=@o", ("o", s.Fixture.A)));
        Assert.Equal(1, await Count(s.Roles.Admin, """
            SELECT COUNT(*) FROM audit_entries WHERE organization_id=@o
              AND event_type='shipment.tracking_correction_created'
            """, ("o", s.Fixture.A)));
        Assert.True(await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM auth_sessions WHERE id=@id AND revoked_at_utc IS NOT NULL", ("id", session)) == 1);
        // After the revocation committed, a further correction is refused.
        var after = await StoreCorrection("Correction after revocation", TimeSpan.FromSeconds(60));
        Assert.True(after.Result is TrackingMutationResult.Forbidden or TrackingMutationResult.Conflict,
            $"post-revocation result {after.Result}");
        Assert.Equal(1, await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM shipment_tracking_corrections WHERE organization_id=@o", ("o", s.Fixture.A)));
    }

    [Fact]
    public async Task MissingMismatchedAndRotatedVerifierKeysFailClosedAndRecoverAfterProvisioning()
    {
        var s = await SeedAsync("g10-key");
        using var _ = s.Admin;
        using var __ = s.Fixture.Admin;
        using var ___ = s.Fixture.Manager;
        using var ____ = s.Fixture.Analyst;
        var target = await AcceptAsync(s, "key-1");
        Assert.Equal(TrackingWorkDisposition.Processed, await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));
        var session = await LatestSession(s.Roles.Admin, s.Actor, s.Fixture.A);
        var call = Genuine(s, target, s.Actor, session, 0, "Key lifecycle probe");

        // Mismatch: the verifier holds a different key than the HTTP runtime signs with.
        var otherKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await Provision(s, otherKey);
        await AssertForbiddenCall(s, "verifier key mismatch", call);
        // Missing: no verifier row exists at all, which is a dependency failure and never a pass.
        await ExecuteAdmin(s.Roles.Admin, "DELETE FROM m10_correction_proof_keys");
        var missing = await Denied(s.Roles.Api, ApplySql, Bind(call));
        Assert.Equal("PZ002", missing.SqlState);
        await using (var db = Context(s.Roles.Api))
        {
            var proof = new TrackingCorrectionProof(call.Nonce!.Value, call.Expires, call.Signature!);
            var result = await new ShipmentTimelineStore(db).CorrectForSessionAsync(s.Fixture.A,
                s.Target.Shipment, new TrackingCorrection(target, null, 0, call.Reason), s.Actor, session,
                "key", call.Key, proof, CancellationToken.None);
            Assert.Equal(TrackingMutationResult.DependencyUnavailable, result.Result);
        }
        await AssertNoCorrectionEffects(s, "key failures");

        // Invalid provisioning input is rejected and never replaces a working key.
        await using (var db = Context(s.Roles.Admin))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                AuditRoleProvisioner.ProvisionCorrectionKeyAsync(db, "not base64!", CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                AuditRoleProvisioner.ProvisionCorrectionKeyAsync(db,
                    Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)), CancellationToken.None));
        }
        Assert.Equal(0, await Count(s.Roles.Admin, "SELECT COUNT(*) FROM m10_correction_proof_keys"));

        // Rotation and restart: the old proof stays invalid, a newly signed one succeeds.
        await Provision(s, ReferenceDataHttpTests.CorrectionProofKey);
        await Provision(s, ReferenceDataHttpTests.CorrectionProofKey); // idempotent restart
        Assert.Equal(1, await Count(s.Roles.Admin, "SELECT COUNT(*) FROM m10_correction_proof_keys"));
        var rotated = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await Provision(s, rotated);
        await AssertForbiddenCall(s, "proof signed before rotation", call);
        var renewed = Genuine(s, target, s.Actor, session, 0, "Key lifecycle probe", null, rotated);
        await using (var db = Context(s.Roles.Api))
        {
            var proof = new TrackingCorrectionProof(renewed.Nonce!.Value, renewed.Expires, renewed.Signature!);
            var result = await new ShipmentTimelineStore(db).CorrectForSessionAsync(s.Fixture.A,
                s.Target.Shipment, new TrackingCorrection(target, null, 0, renewed.Reason), s.Actor, session,
                "key", renewed.Key, proof, CancellationToken.None);
            Assert.Equal(TrackingMutationResult.Applied, result.Result);
        }
        Assert.Equal(1, await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM shipment_tracking_corrections WHERE organization_id=@o", ("o", s.Fixture.A)));

        // The verifier material never appears in audit rows, correction rows or nonce rows.
        var hex = Convert.ToHexString(Convert.FromBase64String(rotated)).ToLowerInvariant();
        foreach (var sql in new[]
        {
            "SELECT COUNT(*) FROM audit_entries WHERE metadata::text LIKE '%'||@k||'%' OR metadata::text LIKE '%'||@h||'%'",
            "SELECT COUNT(*) FROM shipment_tracking_corrections WHERE reason LIKE '%'||@k||'%' OR correlation_id LIKE '%'||@k||'%' OR reason LIKE '%'||@h||'%'",
            "SELECT COUNT(*) FROM m10_correction_proof_nonces WHERE request_digest LIKE '%'||@h||'%'"
        })
            Assert.Equal(0, await Count(s.Roles.Admin, sql, ("k", rotated), ("h", hex)));

        // Runtime identities cannot read, write or replace the verifier at any point.
        foreach (var role in new[] { s.Roles.Api, s.Roles.Worker, s.Roles.Projector })
        {
            await AssertPermissionDenied(role, "read key", "SELECT secret FROM m10_correction_proof_keys");
            await AssertPermissionDenied(role, "write key",
                "UPDATE m10_correction_proof_keys SET secret=decode(repeat('00',32),'hex')");
            await AssertPermissionDenied(role, "insert key",
                "INSERT INTO m10_correction_proof_keys(key_id,secret) VALUES (1,decode(repeat('00',32),'hex'))");
            await AssertPermissionDenied(role, "read nonces", "SELECT 1 FROM m10_correction_proof_nonces");
            await AssertPermissionDenied(role, "forge nonce", """
                INSERT INTO m10_correction_proof_nonces(nonce,organization_id,shipment_id,actor_user_id,
                    idempotency_key,request_digest,expires_at_utc)
                VALUES (gen_random_uuid(),gen_random_uuid(),gen_random_uuid(),gen_random_uuid(),
                        gen_random_uuid(),repeat('a',64),clock_timestamp())
                """);
        }
    }

    [Fact]
    public async Task AuditOutageReturnsASanitized503WithNoEffectsAndTheSameKeyRecoversAfterwards()
    {
        var s = await SeedAsync("g10-outage");
        using var _ = s.Admin;
        using var __ = s.Fixture.Admin;
        using var ___ = s.Fixture.Manager;
        using var ____ = s.Fixture.Analyst;
        var target = await AcceptAsync(s, "outage-1");
        Assert.Equal(TrackingWorkDisposition.Processed, await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));
        using var apiFactory = new ReferenceDataHttpTests.ApiFactory(s.Roles.Api);
        using var manager = apiFactory.CreateClient(new WebApplicationFactoryClientOptions
            { HandleCookies = true });
        await ReferenceDataHttpTests.Login(manager, s.Fixture.ManagerEmail);
        await ReferenceDataHttpTests.Select(manager, s.Fixture.A);
        var path = $"/api/v1/organizations/{s.Fixture.A}/shipments/{s.Target.Shipment}/tracking-corrections";
        var body = new { targetReceiptId = target, replacementReceiptId = (Guid?)null,
            expectedGeneration = 0, reason = "Correction during an audit outage" };
        var key = Guid.NewGuid();
        var revisionBefore = await Count(s.Roles.Admin, """
            SELECT input_revision FROM shipment_tracking_input_versions
            WHERE organization_id=@o AND shipment_id=@s
            """, ("o", s.Fixture.A), ("s", s.Target.Shipment));

        await ExecuteDdl(s.Roles.Admin, """
            CREATE FUNCTION g10_audit_outage() RETURNS trigger LANGUAGE plpgsql AS $f$
            BEGIN
                IF NEW.event_type='shipment.tracking_correction_created' THEN
                    RAISE EXCEPTION 'injected audit persistence outage';
                END IF;
                RETURN NEW;
            END $f$;
            CREATE TRIGGER g10_audit_outage BEFORE INSERT ON audit_entries
                FOR EACH ROW EXECUTE FUNCTION g10_audit_outage();
            """);
        try
        {
            using var failed = await SendM10(manager, path, body, key);
            Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, failed.StatusCode);
            var text = await failed.Content.ReadAsStringAsync();
            Assert.DoesNotContain("injected", text);
            Assert.DoesNotContain("audit persistence", text);
            await AssertNoCorrectionEffects(s, "audit outage");
            Assert.Equal(revisionBefore, await Count(s.Roles.Admin, """
                SELECT input_revision FROM shipment_tracking_input_versions
                WHERE organization_id=@o AND shipment_id=@s
                """, ("o", s.Fixture.A), ("s", s.Target.Shipment)));
        }
        finally
        {
            await ExecuteDdl(s.Roles.Admin, """
                DROP TRIGGER g10_audit_outage ON audit_entries;
                DROP FUNCTION g10_audit_outage();
                """);
        }

        // The failed attempt left no replay acknowledgment, so the same key now applies exactly once.
        using var recovered = await SendM10(manager, path, body, key);
        Assert.Equal(System.Net.HttpStatusCode.OK, recovered.StatusCode);
        using var replay = await SendM10(manager, path, body, key);
        Assert.Equal(System.Net.HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(1, await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM shipment_tracking_corrections WHERE organization_id=@o", ("o", s.Fixture.A)));
        Assert.Equal(1, await Count(s.Roles.Admin, """
            SELECT COUNT(*) FROM audit_entries WHERE organization_id=@o
              AND event_type='shipment.tracking_correction_created'
            """, ("o", s.Fixture.A)));
        Assert.Equal(1, await Count(s.Roles.Admin, """
            SELECT COUNT(*) FROM shipment_tracking_operation_replays
            WHERE organization_id=@o AND operation_kind='Correction'
            """, ("o", s.Fixture.A)));
    }

    private static async Task ExecuteDdl(string admin, string sql)
    {
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task Provision(Seeded s, string keyBase64)
    {
        await using var db = Context(s.Roles.Admin);
        await AuditRoleProvisioner.ProvisionCorrectionKeyAsync(db, keyBase64, CancellationToken.None);
    }
}
