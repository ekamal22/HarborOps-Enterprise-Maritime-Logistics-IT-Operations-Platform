// M10/G16 combined controlled fault injection. Runs on an exported copy of the exact source revision, never
// on the checkout. Every edit anchor must occur exactly once, every mutant must compile, and a mutant counts
// as killed only when at least one of its named tests fails while the clean control passes.
// Scope: M10 privilege, trigger, lock, route, cursor and projector boundaries plus the accepted M9/M16
// integration mutants whose source surfaces M10 changes (role provisioner, worker effect path, retention).
// Usage: node scripts/run-g10-mutations.mjs <E: verification root>
//   env HARBOROPS_SOURCE_SHA, HARBOROPS_G10_PG_IMAGE (private PostgreSQL image tag)
//   The .NET 10 SDK runs in a container because the host has no .NET 10 SDK.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";

const root = process.argv[2], sha = process.env.HARBOROPS_SOURCE_SHA;
const dbImage = process.env.HARBOROPS_G10_PG_IMAGE || process.env.HARBOROPS_CG16_PG_IMAGE;
assert.ok(root && /^(?:[Ee]:[\\/]|\/e\/)/.test(root), "E: verification root required");
assert.match(sha || "", /^[0-9a-f]{40}$/);
assert.ok(dbImage, "HARBOROPS_G10_PG_IMAGE is required");
const evidence = path.join(root, "evidence", "mutations");
fs.mkdirSync(evidence, { recursive: true });
const cache = path.join(root, "cache");
fs.mkdirSync(cache, { recursive: true });
const sourceTar = path.join(cache, "g10-mutation-" + sha.slice(0, 12) + ".tar");
const exported = path.join(cache, "g10-mutation-export-" + sha.slice(0, 12));
const run = (cmd, args, options = {}) => {
  const result = spawnSync(cmd, args, { encoding: "utf8", timeout: 1500000, maxBuffer: 64 * 1024 * 1024, ...options });
  if (result.error) throw result.error;
  return result;
};
const expectOk = (cmd, args, options) => {
  const result = run(cmd, args, options);
  assert.equal(result.status, 0, cmd + " failed: " + (result.stderr || result.stdout || "").slice(-600));
  return result.stdout || "";
};

const MIG10 = "src/HarborOps.Infrastructure/Persistence/Migrations/20261105180000_ShipmentTrackingProjections.cs";
const AUTH10 = "src/HarborOps.Infrastructure/Persistence/Migrations/20261107120000_M10CorrectionAuthority.cs";
const MIG16 = "src/HarborOps.Infrastructure/Persistence/Migrations/20261104180000_AuditEntries.cs";
const MIG9 = "src/HarborOps.Infrastructure/Persistence/Migrations/20261104190000_TrackingWorker.cs";
const ROLES = "src/HarborOps.Api/Audit/AuditRoleProvisioner.cs";
const WORKER = "src/HarborOps.Infrastructure/Integrations/TrackingWorkStore.cs";
const STORE = "src/HarborOps.Infrastructure/Shipments/ShipmentTimelineStore.cs";
const ROUTES = "src/HarborOps.Api/Shipments/ShipmentTimelineEndpoints.cs";
const POLICY = "src/HarborOps.Domain/Tracking/TrackingTimelinePolicy.cs";
const PROJECTOR_SQL = "scripts/configure-m10-projector-db.sql";

const U = {
  reducer: "TrackingTimelinePolicyTests",
  tracking: "RedriveAndRolesFailClosed",
  auditOnly: "OnlyAuditorHasExplicitAuditReadPermission",
  auditNoAdmin: "AuditPermissionDoesNotBroadenAdministrativeGrants"
};
const I = {
  // Combined M9/M16 regressions
  lifecycle: "ManagerRedriveAndWorkerLifecycleRunUnderTheRealRestrictedRoles",
  grants: "RuntimeRolesCarryExactlyTheCombinedGrantContract",
  roleMatrix: "ProvisionedRuntimeRolesCannotModifyAuditEvidenceOrRunDdl",
  swallow: "AuditInsertFailureAbortsTheOriginalBusinessMutation",
  sqlForge: "DirectSqlCannotForgeDuplicateOrTerminalWorkEvidence",
  commit: "AmbiguousFinalizeCommitIsReconstructedFromDurableState",
  effect: "ValidTimeScopedContainerAssignmentProducesExactlyOneImmutableEffect",
  lockProcess: "TwoIndependentWorkersContendOnOneReceiptAndOnlyOneClaimsIt",
  lockIntake: "IntakeAndWorkerClaimSerializeOnTheOrganizationLockWithoutDeadlock",
  m9down: "RetainedAttemptHistoryOnRedrivenPendingWorkBlocksTheM9Downgrade",
  m16down: "AcceptedM8UpgradeAddsImmutableAuditWithoutChangingPriorRecords",
  // M10 verification
  g10main: "ResolvedEventReadRebuildAndVoidCorrectionPreserveM9History",
  g10race: "ConcurrentDirectCorrectionWritersCannotCommitAnInvalidChain",
  workerId: "RealWorkerIdentityFeedsTheM10IndexAndAuditCaptureThroughDefinerTriggers",
  apiId: "RealApiIdentityHasOnlyTheScopedM10WritesAndTheDefinerTriggerRuns",
  proofForgery: "StolenDatabaseSessionIdsCannotForgeAnM10Correction",
  auditRollback: "FailedRequiredM16AuditRollsBackCorrectionAndReplay",
  bindings: "EveryProofBindingAndInvalidProofIsDeniedWithoutEffectsOnTheRestrictedLogin",
  authorityState: "AuthorityIsDeniedForAnalystDowngradedRevokedAndExpiredSessions",
  authorityRace: "RevocationAndDowngradeRacingASignedCorrectionKeepTheLockOrderAndNeverCommitAfterRevocation",
  keyLifecycle: "MissingMismatchedAndRotatedVerifierKeysFailClosedAndRecoverAfterProvisioning",
  projectorId: "RealProjectorIdentityCannotMutateM8M9M16EvidenceOrOtherScopes",
  projectorStore: "RealProjectorIdentityDiscoversAndRebuildsADirtyScopeThroughTheStore",
  projectorService: "RealProjectorServiceSkipsAPoisonedShipmentThenRecoversUnderTheRestrictedRole",
  projectorFresh: "FreshInstallAppliesThirteenMigrationsWithTheNullSafeProjectorScopeFunction",
  projectorUpgrade: "GenuineTwelveMigrationDatabaseKeepsTheOriginalFunctionUntilMigrationThirteenThenIsNullSafeAndIdempotent",
  projectorPerms: "OrdinaryRuntimeIdentitiesCannotExecuteReplaceOrBroadenTheProjectorScopeAndSigningFunctions",
  routes: "M10HttpRoutesRunUnderTheRestrictedApiRoleWithManagerAndTenantGuards",
  locks: "WorkerFinalizationBlocksBehindProjectorAndCorrectionLocksWithoutDeadlock",
  live: "M10RoutesHonorLiveRoleDowngradeRateLimitAndSessionRevocation",
  scale: "LargeHistoryUsesTheKeysetIndexPagesCompletelyAndRebuildsWithinBounds",
  upgrade: "PopulatedHistoryIsBackfilledByM10WithoutAlteringAcceptedEvidenceAndDowngradeIsProtected"
};
const V = (name, edits, unit, integration) => ({ name, edits, unit, integration });
// Migration 12 is immutable and migration 13 replaces the projector scope function in place, so the
// effective definition on every database comes from migration 13. Its Up block is addressed through a
// header that is unique to it (the Down block restores the original body under a different label).
const NEWLINE = String.fromCharCode(10);
const RECONCILE_HEAD = [
  "DO $reconcile$",
  "            DECLARE schema_name text := current_schema();",
  "            BEGIN",
  "                EXECUTE format($sql$",
  "                    CREATE OR REPLACE FUNCTION %1$I.m10_lock_projector_scope(p_org uuid,p_shipment uuid)",
  "                    RETURNS boolean",
  "                    LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $fn$",
  "                    DECLARE organization_state text;",
  "                    DECLARE shipment_state text;",
  "                    BEGIN"].join(NEWLINE);
const GUARD_LINE = NEWLINE + "                        IF session_user <> 'harborops_projector' THEN RETURN false; END IF;";
const variants = [
  V("control", [], Object.values(U), Object.values(I)),

  // ----- M10 reducer (unit) -----
  V("reducer-superseded-accepted", [[POLICY,
    'if (e.Superseded || e.EventCode is not ("GTIN" or "GTOT")) return;',
    'if (e.EventCode is not ("GTIN" or "GTOT")) return;']], [U.reducer], []),
  V("reducer-mixed-aggregated-as-in", [[POLICY,
    'else result = "Mixed";', 'else result = "EquipmentIn";']], [U.reducer], []),
  V("reducer-same-container-conflict-ignored", [[POLICY,
    'else if (active.Any(x => x.ObservedState == "Conflict")) result = "Conflict";',
    'else if (false && active.Any(x => x.ObservedState == "Conflict")) result = "Conflict";']], [U.reducer], []),

  // ----- M10 privileges -----
  V("m10-api-timeline-insert-granted", [[ROLES,
    "GRANT UPDATE ON shipment_tracking_projections TO harborops_api;",
    "GRANT UPDATE ON shipment_tracking_projections TO harborops_api;\n                        GRANT INSERT ON shipment_tracking_timeline_entries TO harborops_api;"]],
    [], [I.grants, I.apiId]),
  V("m10-worker-input-version-update-granted", [[ROLES,
    "GRANT SELECT ON tracking_work_attempt_events,\n                            tracking_processing_effects TO harborops_worker;",
    "GRANT SELECT ON tracking_work_attempt_events,\n                            tracking_processing_effects TO harborops_worker;\n                        IF to_regclass('public.shipment_tracking_input_versions') IS NOT NULL THEN\n                            GRANT UPDATE ON shipment_tracking_input_versions TO harborops_worker;\n                        END IF;"]],
    [], [I.grants, I.workerId]),
  V("m10-projector-heads-grant-removed", [[PROJECTOR_SQL,
    "shipment_tracking_input_versions,shipment_tracking_correction_heads,",
    "shipment_tracking_input_versions,"]],
    [], [I.projectorStore, I.projectorService]),
  V("m10-projector-session-guard-removed", [[MIG10,
    "IF session_user <> 'harborops_projector' THEN RETURN false; END IF;", ""],
    [AUTH10, RECONCILE_HEAD + GUARD_LINE, RECONCILE_HEAD]],
    [], [I.projectorId, I.projectorUpgrade]),
  V("m10-projector-lock-null-for-unknown-scope", [[AUTH10,
    "RETURN COALESCE(organization_state='Active' AND shipment_state='Active',false);",
    "RETURN organization_state='Active' AND shipment_state='Active';"]],
    [], [I.projectorId, I.projectorFresh, I.projectorUpgrade]),
  V("m10-projector-scope-reconcile-not-security-definer", [[AUTH10, RECONCILE_HEAD,
    RECONCILE_HEAD.replace("LANGUAGE plpgsql SECURITY DEFINER SET", "LANGUAGE plpgsql SET")]],
    [], [I.projectorId, I.projectorFresh, I.projectorPerms]),
  V("m10-projector-scope-reconcile-search-path-widened", [[AUTH10, RECONCILE_HEAD,
    RECONCILE_HEAD.replace("SET search_path=pg_catalog AS", "SET search_path=public,pg_catalog AS")]],
    [], [I.projectorFresh, I.projectorPerms]),
  V("m10-down-leaves-corrected-projector-function", [[AUTH10,
    "RETURN organization_state='Active' AND shipment_state='Active';",
    "RETURN COALESCE(organization_state='Active' AND shipment_state='Active',false);"]],
    [], [I.projectorUpgrade]),
  V("m10-twelfth-migration-edited-in-place", [[MIG10,
    "RETURN organization_state='Active' AND shipment_state='Active';",
    "RETURN COALESCE(organization_state='Active' AND shipment_state='Active',false);"]],
    [], [I.projectorUpgrade]),

  // ----- M10 migration triggers and retention -----
  V("m10-input-trigger-not-security-definer", [[MIG10,
    "CREATE FUNCTION m10_track_input_change() RETURNS trigger\n            LANGUAGE plpgsql SECURITY DEFINER",
    "CREATE FUNCTION m10_track_input_change() RETURNS trigger\n            LANGUAGE plpgsql"]],
    [], [I.workerId, I.lifecycle]),
  V("m10-signed-correction-function-not-security-definer", [[AUTH10,
    "RETURNS void LANGUAGE plpgsql SECURITY DEFINER",
    "RETURNS void LANGUAGE plpgsql"]],
    [], [I.apiId, I.routes]),
  V("m10-timeline-immutability-removed", [[MIG10,
    "CREATE TRIGGER tr_m10_immutable_timeline\n                BEFORE UPDATE OR DELETE ON shipment_tracking_timeline_entries\n                FOR EACH ROW EXECUTE FUNCTION m8_guard_append_only();",
    "-- immutability trigger removed by mutation"]],
    [], [I.upgrade]),
  V("m10-timeline-provenance-check-removed", [[MIG10,
    "IF matched<>1 THEN\n                    RAISE EXCEPTION 'M10 timeline entry must match immutable M9 evidence'",
    "IF false THEN\n                    RAISE EXCEPTION 'M10 timeline entry must match immutable M9 evidence'"]],
    [], [I.g10main]),
  V("m10-downgrade-retention-weakened", [[MIG10,
    "RAISE EXCEPTION 'M10 retained history prevents downgrade'\n                        USING ERRCODE='23514';",
    "RAISE NOTICE 'weakened';"]],
    [], [I.upgrade]),
  V("m10-keyset-index-dropped", [[MIG10,
    "CREATE INDEX ix_m10_timeline_keyset\n                ON shipment_tracking_timeline_entries\n                (organization_id,shipment_id,occurred_at_utc,received_at_utc,source_id,\n                 source_event_id COLLATE \"C\",receipt_id,event_id);",
    "-- keyset index removed by mutation"]],
    [], [I.scale]),

  // ----- M10 HTTP boundaries -----
  V("m10-cursor-digest-check-removed", [[ROUTES,
    "cursor is not null && cursor.Digest != view.State.InputDigest)",
    "false)"]],
    [], [I.scale, I.g10main]),
  V("m10-idempotency-collision-accepted", [[STORE,
    "return sameRequest\n                            ? (TrackingMutationResult.Replayed, savedRevision)\n                            : (TrackingMutationResult.Conflict, 0);",
    "return (TrackingMutationResult.Replayed, savedRevision);"]],
    [], [I.routes]),
  V("m10-rebuild-rate-limit-removed", [[ROUTES,
    "_ => Fail(ctx, 409, \"rebuild_conflict\")\n            };\n        }).RequireRateLimiting(\"m10-mutations\");",
    "_ => Fail(ctx, 409, \"rebuild_conflict\")\n            };\n        });"]],
    [], [I.live]),
  // Manager authority is enforced at the route, store and signed database operation.
  // Remove all three checks to make this a meaningful security mutation.
  V("m10-manager-only-removed-both-layers", [
    [ROUTES, "OrganizationPermission.ManageShipmentData,\n                ct, managerOnly: true);\n            if (denied is not null) return denied;\n            var input = await ReadCorrection(ctx);",
      "OrganizationPermission.ManageShipmentData,\n                ct, managerOnly: false);\n            if (denied is not null) return denied;\n            var input = await ReadCorrection(ctx);"],
    [STORE, "if (session != Guid.Empty &&\n                    !await CurrentManagerAsync(Connection, tx, org, actor, session, ct))",
      "if (false &&\n                    !await CurrentManagerAsync(Connection, tx, org, actor, session, ct))"],
    [AUTH10, "AND m.status='Active' AND m.role='Manager'",
      "AND m.status='Active'"]],
    [], [I.live, I.authorityState]),

  // ----- Correction authority and atomic audit -----
  V("m10-api-direct-correction-grant-restored", [[ROLES,
    "REVOKE INSERT ON shipment_tracking_corrections FROM harborops_api;",
    "GRANT INSERT ON shipment_tracking_corrections TO harborops_api;"]],
    [], [I.grants, I.apiId]),
  V("m10-proof-signature-ignored", [[AUTH10,
    "OR p_signature IS DISTINCT FROM expected_signature THEN",
    "OR false THEN"]],
    [], [I.proofForgery, I.bindings]),
  V("m10-required-audit-rejected", [[AUTH10,
    "VALUES (p_org,'shipment.tracking_correction_created',1,'Succeeded',",
    "VALUES (p_org,'shipment.tracking_correction_created',1,'Failed',"]],
    [], [I.apiId, I.auditRollback]),
  V("m10-proof-expired-accepted", [[AUTH10,
    "OR p_expires_epoch < now_epoch", "OR false"]],
    [], [I.bindings]),
  V("m10-proof-far-future-accepted", [[AUTH10,
    "OR p_expires_epoch > now_epoch + 60 THEN", "OR false THEN"]],
    [], [I.bindings]),
  V("m10-session-revocation-ignored", [[AUTH10,
    "AND s.revoked_at_utc IS NULL\n                      AND s.idle_expires_at_utc > clock_timestamp()",
    "AND s.idle_expires_at_utc > clock_timestamp()"]],
    [], [I.authorityState, I.authorityRace]),
  V("m10-session-expiry-ignored", [[AUTH10,
    "AND s.idle_expires_at_utc > clock_timestamp()\n                      AND s.absolute_expires_at_utc > clock_timestamp()",
    "AND true"]],
    [], [I.authorityState]),
  V("m10-security-version-ignored", [[AUTH10,
    "AND u.status='Active' AND u.security_version=s.security_version",
    "AND u.status='Active'"]],
    [], [I.authorityState]),
  V("m10-authority-row-locks-removed", [[AUTH10,
    "FOR SHARE OF s,u,m\n                ) INTO active_manager;",
    ") INTO active_manager;"]],
    [], [I.authorityRace]),
  V("m10-replay-guard-trigger-removed", [[AUTH10,
    "CREATE TRIGGER tr_m10_guard_correction_replay\n                BEFORE INSERT ON shipment_tracking_operation_replays\n                FOR EACH ROW EXECUTE FUNCTION m10_guard_correction_replay();",
    "-- replay guard trigger removed by mutation"]],
    [], [I.proofForgery, I.upgrade]),
  V("m10-verifier-key-readable-by-api", [[ROLES,
    "REVOKE ALL ON m10_correction_proof_keys,\n                            m10_correction_proof_nonces FROM harborops_api;",
    "GRANT SELECT ON m10_correction_proof_keys,\n                            m10_correction_proof_nonces TO harborops_api;"]],
    [], [I.keyLifecycle, I.apiId, I.grants]),
  V("m10-audit-failure-swallowed-in-correction", [[AUTH10,
    "EXCEPTION WHEN OTHERS THEN\n                    RAISE EXCEPTION 'Required correction audit is unavailable'\n                        USING ERRCODE='PZ001';",
    "EXCEPTION WHEN OTHERS THEN\n                    NULL;"]],
    [], [I.auditRollback]),

  // ----- Accepted M9/M16 integration surfaces changed by M10 -----
  V("api-can-insert-audit", [[ROLES,
    "GRANT SELECT ON TABLE audit_entries TO harborops_api;",
    "GRANT SELECT, INSERT ON TABLE audit_entries TO harborops_api;"]],
    [], [I.roleMatrix, I.grants]),
  V("worker-attempt-insert-granted", [[ROLES,
    "GRANT INSERT ON tracking_processing_effects TO harborops_worker;",
    "GRANT INSERT ON tracking_processing_effects, tracking_work_attempt_events TO harborops_worker;"]],
    [], [I.lifecycle, I.grants, I.roleMatrix]),
  V("worker-credential-read-restored", [[ROLES,
    "                    tracking_inbox_work_items,\n                    organizations, containers, shipments,",
    "                    tracking_inbox_work_items, integration_credentials,\n                    organizations, containers, shipments,"]],
    [], [I.grants]),
  V("api-redrive-scope-guards-disabled", [
    [ROLES, "IF session_user = 'harborops_api' AND NOT COALESCE(", "IF session_user = 'harborops_api_off' AND NOT COALESCE("],
    [ROLES, "IF session_user = 'harborops_api' THEN\n                                -- A new intake is Pending at generation zero.", "IF session_user = 'harborops_api_off' THEN\n                                -- A new intake is Pending at generation zero."]],
    [], [I.lifecycle]),
  V("audit-failure-swallowed", [
    [MIG16, "EXECUTE format(", "BEGIN\n                EXECUTE format("],
    [MIG16, "'operation', operation_code, 'changedFields', changed_fields);",
      "'operation', operation_code, 'changedFields', changed_fields);\n                EXCEPTION WHEN OTHERS THEN NULL; END;"]],
    [], [I.swallow]),
  V("m9-effect-uniqueness-removed", [[MIG9,
    "CREATE TABLE tracking_processing_effects (\n                receipt_id uuid PRIMARY KEY,",
    "CREATE TABLE tracking_processing_effects (\n                receipt_id uuid NOT NULL,"]],
    [], [I.sqlForge]),
  V("m9-claim-organization-lock-removed", [[WORKER,
    "        if (!await LockActiveOrganizationAsync(connection, tx, organizationId, ct))\n            return null;\n\n        Guid receipt = default, source = default;",
    "        Guid receipt = default, source = default;"]],
    [], [I.lockProcess, I.lockIntake]),
  V("m9-effect-commit-acknowledged-without-commit", [[WORKER,
    "        await transaction.CommitAsync(ct);\n        return resolved.HasValue ? TrackingWorkDisposition.Processed :",
    "        // Disposable mutation: acknowledge without COMMIT.\n        return resolved.HasValue ? TrackingWorkDisposition.Processed :"]],
    [], [I.effect, I.commit]),
  V("m9-downgrade-retention-weakened", [[MIG9,
    "RAISE EXCEPTION 'M9 work evidence prevents downgrade'\n                        USING ERRCODE='23514';",
    "RAISE NOTICE 'weakened';"]],
    [], [I.m9down]),
  V("m16-downgrade-retention-weakened", [[MIG16,
    "RAISE EXCEPTION 'Cannot downgrade M16 with retained audit evidence'\n                        USING ERRCODE='23514';",
    "RAISE NOTICE 'weakened';"]],
    [], [I.m16down])
];

// Development aid: verify every anchor occurs exactly once in the source revision, then stop.
if (process.env.HARBOROPS_G10_ANCHOR_CHECK === "1") {
  let bad = 0;
  for (const v of variants) for (const [file, anchor] of v.edits) {
    const text = expectOk("git", ["show", sha + ":" + file]);
    const count = text.split(anchor).length - 1;
    if (count !== 1) { bad++; console.error(`ANCHOR ${count}x: ${v.name}: ${anchor.slice(0, 70)}`); }
  }
  console.log(bad ? "anchor check FAILED" : "all anchors unique in " + sha.slice(0, 12));
  process.exit(bad ? 1 : 0);
}
assert.ok(!fs.existsSync(exported), "Mutation export already exists; use a new verification root");
expectOk("git", ["archive", "--format=tar", "--output", sourceTar, sha]);
const subset = process.env.HARBOROPS_G10_MUTATION_SUBSET?.split(",");
assert.ok(!subset || process.env.HARBOROPS_G10_MUTATION_SUBSET_DEV === "1", "Subset runs are development only");

const summary = { sourceSha: sha, checkedAtUtc: new Date().toISOString(), mutants: [] };
const report = path.join(root, "evidence", "g10-mutations.json");
const save = () => fs.writeFileSync(report, JSON.stringify(summary, null, 2));
const name = "ho-g10-mutant-pg";
assert.equal(expectOk("docker", ["ps", "-aq", "--filter", "name=^/" + name + "$"]).trim(), "", "Existing mutant database container found");
const filterFor = tests => tests.map(t => "FullyQualifiedName~" + t).join("|");
let failures = 0;
try {
  for (const variant of variants.filter(v => !subset || v.name === "control" || subset.includes(v.name))) {
    const work = path.join(exported, variant.name);
    fs.mkdirSync(work, { recursive: true });
    // GNU tar reads "E:\..." as host:path, so extract with forward-slash paths relative to the cache.
    const rel = p => path.relative(cache, p).split(path.sep).join("/");
    const extract = run("tar", ["-xf", rel(sourceTar), "-C", rel(work)], { cwd: cache });
    assert.equal(extract.status, 0, "tar failed: " + (extract.stderr || extract.stdout || "").slice(-400));
    for (const [file, anchor, replacement] of variant.edits) {
      const target = path.join(work, file);
      const text = fs.readFileSync(target, "utf8");
      assert.equal(text.split(anchor).length - 1, 1, `${variant.name}: anchor must occur exactly once in ${file}: ${anchor}`);
      assert.notEqual(anchor, replacement, `${variant.name}: no-op edit`);
      fs.writeFileSync(target, text.replace(anchor, () => replacement));
    }
    run("docker", ["rm", "-f", name]);
    expectOk("docker", ["run", "-d", "--name", name, "--label", "harborops.g10mutation=disposable", "--ulimit", "core=0",
      "--tmpfs", "/var/lib/postgresql/data:uid=70,gid=70,mode=0700", "--cap-drop", "ALL", "--security-opt", "no-new-privileges",
      "-e", "POSTGRES_DB=harborops_integration", "-e", "POSTGRES_USER=harborops", "-e", "POSTGRES_HOST_AUTH_METHOD=trust", dbImage]);
    let ready = false;
    for (let k = 0; k < 60 && !ready; k++) {
      ready = run("docker", ["exec", name, "pg_isready", "-U", "harborops", "-d", "harborops_integration"]).status === 0;
      if (!ready) await new Promise(r => setTimeout(r, 500));
    }
    assert.ok(ready, "Mutation database not ready");
    const steps = ["dotnet restore HarborOps.sln -v q", "dotnet build HarborOps.sln -c Release --no-restore -v q -warnaserror"];
    if (variant.unit.length)
      steps.push("dotnet test tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj -c Release --no-build --filter '" + filterFor(variant.unit) + "' || true");
    if (variant.integration.length)
      steps.push("dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj -c Release --no-build --filter '" + filterFor(variant.integration) + "' || true");
    const shell = steps.slice(0, 2).join(" && ") + " && { " + steps.slice(2).join("; ") + "; }";
    const cacheDir = path.join(cache, "nuget", "packages");
    const result = run("docker", ["run", "--rm", "--ulimit", "core=0", "--network", "container:" + name,
      "-v", work + ":/src", "-v", path.dirname(cacheDir) + ":/nuget-parent", "-w", "/src",
      "-e", "DOTNET_NOLOGO=1", "-e", "DOTNET_CLI_TELEMETRY_OPTOUT=1", "-e", "NUGET_PACKAGES=/nuget-parent/" + path.basename(cacheDir),
      "-e", "HOME=/tmp/home", "-e", "DOTNET_CLI_HOME=/tmp/home",
      "-e", "HARBOROPS_TEST_DB=Host=127.0.0.1;Port=5432;Database=harborops_integration;Username=harborops",
      "mcr.microsoft.com/dotnet/sdk:10.0", "bash", "-c", shell], { env: { ...process.env, MSYS_NO_PATHCONV: "1" } });
    const output = (result.stdout || "") + (result.stderr || "");
    fs.writeFileSync(path.join(evidence, variant.name + ".log"), output);
    run("docker", ["rm", "-f", name]);
    const failed = [...output.matchAll(/^\s+Failed (\S+)/gm)].map(m => m[1].replace("HarborOps.IntegrationTests.", "").replace("HarborOps.UnitTests.", ""));
    const expected = [...variant.unit, ...variant.integration];
    const compiled = !/error (CS|MSB|xUnit)\d+/.test(output) && /Passed!|Failed!/.test(output);
    const entry = { variant: variant.name, compiled, exitCode: result.status, failedTests: [...new Set(failed)], expectedKillers: variant.name === "control" ? [] : expected };
    if (variant.name === "control") {
      const executed = [...output.matchAll(/(Passed|Failed)!\s+- Failed:\s+(\d+), Passed:\s+(\d+)/g)].map(m => ({ failed: +m[2], passed: +m[3] }));
      entry.executed = executed;
      entry.status = compiled && executed.length === 2 && executed.every(e => e.failed === 0 && e.passed > 0) ? "control-clean" : "CONTROL-FAILED";
    } else if (!compiled) entry.status = "INVALID-MUTANT (did not compile)";
    else entry.status = expected.some(k => failed.some(f => f.includes(k))) ? "killed" : (failed.length ? "KILLED-BY-UNEXPECTED-TEST" : "SURVIVED");
    if (!["control-clean", "killed"].includes(entry.status)) failures++;
    summary.mutants.push(entry); save();
    console.log(`${entry.status}: ${variant.name} failed=[${entry.failedTests.join(", ")}]`);
    if (variant.name === "control" && entry.status !== "control-clean") break;
  }
} finally { run("docker", ["rm", "-f", name]); }
summary.survivorsOrInvalid = failures; save();
console.log(`G10 mutation summary: ${summary.mutants.length} variants, ${failures} not killed as expected`);
process.exit(failures ? 1 : 0);
