// Combined M9/M16 controlled fault injection. Runs on an exported copy of the exact source revision, never
// on the checkout. Every edit anchor must occur exactly once, every mutant must compile, and a mutant counts
// as killed only when at least one of its named tests fails while the clean control passes.
// Focus: integration-specific boundaries (tracking permissions, audit permission, runtime role privileges,
// restricted API redrive scope, worker/effect integrity, lock order, rollback retention, audit guards).
// Usage: node scripts/run-combined-mutations.mjs <E: verification root>
//   env HARBOROPS_SOURCE_SHA, HARBOROPS_CG16_PG_IMAGE (private PostgreSQL image tag)
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";

const root = process.argv[2], sha = process.env.HARBOROPS_SOURCE_SHA, dbImage = process.env.HARBOROPS_CG16_PG_IMAGE;
assert.ok(root && /^(?:[Ee]:[\\/]|\/e\/)/.test(root), "E: verification root required");
assert.match(sha || "", /^[0-9a-f]{40}$/);
assert.ok(dbImage, "HARBOROPS_CG16_PG_IMAGE is required");
const evidence = path.join(root, "evidence", "mutations");
fs.mkdirSync(evidence, { recursive: true });
const cache = path.join(root, "cache");
fs.mkdirSync(cache, { recursive: true });
const sourceTar = path.join(cache, "cg16-mutation-" + sha.slice(0, 12) + ".tar");
const exported = path.join(cache, "cg16-mutation-export-" + sha.slice(0, 12));
assert.ok(!fs.existsSync(exported), "Mutation export already exists; use a new verification root");
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
expectOk("git", ["archive", "--format=tar", "--output", sourceTar, sha]);

const MIG16 = "src/HarborOps.Infrastructure/Persistence/Migrations/20261104180000_AuditEntries.cs";
const MIG9 = "src/HarborOps.Infrastructure/Persistence/Migrations/20261104190000_TrackingWorker.cs";
const POLICY = "src/HarborOps.Domain/Organizations/OrganizationPolicy.cs";
const ROLES = "src/HarborOps.Api/Audit/AuditRoleProvisioner.cs";
const ADMIN = "src/HarborOps.Infrastructure/Integrations/TrackingWorkAdminStore.cs";
const WORKER = "src/HarborOps.Infrastructure/Integrations/TrackingWorkStore.cs";

// Test method names. unit tests run in HarborOps.UnitTests, integration tests in HarborOps.IntegrationTests.
const U = {
  tracking: "RedriveAndRolesFailClosed",
  auditOnly: "OnlyAuditorHasExplicitAuditReadPermission",
  auditNoAdmin: "AuditPermissionDoesNotBroadenAdministrativeGrants"
};
const I = {
  lifecycle: "ManagerRedriveAndWorkerLifecycleRunUnderTheRealRestrictedRoles",
  grants: "RuntimeRolesCarryExactlyTheCombinedGrantContract",
  roleMatrix: "ProvisionedRuntimeRolesCannotModifyAuditEvidenceOrRunDdl",
  foreign: "ForeignOrganizationIdentifiersNeverCrossTheTenantBoundary",
  revoked: "ManagerQueueAccessAndRedriveStayDeniedAfterRevocation",
  swallow: "AuditInsertFailureAbortsTheOriginalBusinessMutation",
  stale: "SourceBackedAuditNeverInheritsAStaleHumanOrCliContext",
  fk: "AuditInsertionDoesNotLockTheOrganizationRowBehindRevocations",
  evidence: "BusinessChangesGenerateMinimalAppendOnlyEvidence",
  sqlForge: "DirectSqlCannotForgeDuplicateOrTerminalWorkEvidence",
  commit: "AmbiguousFinalizeCommitIsReconstructedFromDurableState",
  effect: "ValidTimeScopedContainerAssignmentProducesExactlyOneImmutableEffect",
  lockProcess: "TwoIndependentWorkersContendOnOneReceiptAndOnlyOneClaimsIt",
  lockIntake: "IntakeAndWorkerClaimSerializeOnTheOrganizationLockWithoutDeadlock",
  m9down: "RetainedAttemptHistoryOnRedrivenPendingWorkBlocksTheM9Downgrade",
  m16down: "AcceptedM8UpgradeAddsImmutableAuditWithoutChangingPriorRecords"
};
const V = (name, edits, unit, integration) => ({ name, edits, unit, integration });
const variants = [
  V("control", [], [U.tracking, U.auditOnly, U.auditNoAdmin], Object.values(I)),
  V("m9-work-read-auditor-removed", [[POLICY,
    "role is (OrganizationRole.Analyst or OrganizationRole.Manager or OrganizationRole.Auditor)) ||\n        (permission == OrganizationPermission.TrackingWorkRedrive",
    "role is (OrganizationRole.Analyst or OrganizationRole.Manager)) ||\n        (permission == OrganizationPermission.TrackingWorkRedrive"]],
    [U.tracking], []),
  V("m9-redrive-widened-to-administrator", [[POLICY,
    "(permission == OrganizationPermission.TrackingWorkRedrive &&\n            role == OrganizationRole.Manager) ||",
    "(permission == OrganizationPermission.TrackingWorkRedrive &&\n            role is (OrganizationRole.Manager or OrganizationRole.Administrator)) ||"]],
    [U.tracking], []),
  V("m16-audit-widened-to-manager", [[POLICY,
    "(permission == OrganizationPermission.ViewAuditEvents && role == OrganizationRole.Auditor)",
    "(permission == OrganizationPermission.ViewAuditEvents && role is (OrganizationRole.Auditor or OrganizationRole.Manager))"]],
    [U.auditOnly, U.auditNoAdmin], []),
  // The list query filters by tenant twice (source query and outer predicate); removing both is the
  // non-equivalent mutant. Removing only one is an equivalent mutant by defense in depth.
  V("tracking-work-tenant-predicates-removed", [
    [ADMIN, "        var query = scoped.AsNoTracking().Where(x =>\n            x.OrganizationId == scope.OrganizationId &&",
      "        var query = scoped.AsNoTracking().Where(x =>\n            x.ReceiptId != Guid.Empty &&"],
    [ADMIN, "            : db.TrackingInboxWorkItems.Where(x => x.OrganizationId == scope.OrganizationId);",
      "            : db.TrackingInboxWorkItems;"]],
    [], [I.lifecycle]),
  V("api-can-insert-audit", [[ROLES,
    "GRANT SELECT ON TABLE audit_entries TO harborops_api;",
    "GRANT SELECT, INSERT ON TABLE audit_entries TO harborops_api;"]],
    [], [I.roleMatrix, I.grants]),
  V("api-attempt-insert-revoked", [[ROLES,
    "GRANT INSERT ON tracking_work_attempt_events TO harborops_api;",
    "REVOKE INSERT ON tracking_work_attempt_events FROM harborops_api;"]],
    [], [I.lifecycle, I.grants, I.roleMatrix]),
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
    [ROLES, "IF session_user = 'harborops_api' THEN\n                                valid", "IF session_user = 'harborops_api_off' THEN\n                                valid"]],
    [], [I.lifecycle]),
  V("audit-failure-swallowed", [
    [MIG16, "EXECUTE format(", "BEGIN\n                EXECUTE format("],
    [MIG16, "'operation', operation_code, 'changedFields', changed_fields);",
      "'operation', operation_code, 'changedFields', changed_fields);\n                EXCEPTION WHEN OTHERS THEN NULL; END;"]],
    [], [I.swallow]),
  V("audit-truncate-guard-removed", [[MIG16,
    "FOR EACH STATEMENT EXECUTE FUNCTION m16_audit_guard();",
    "FOR EACH STATEMENT EXECUTE FUNCTION m16_audit_guard();\n            DROP TRIGGER m16_audit_no_truncate ON audit_entries;"]],
    [], [I.evidence, I.roleMatrix]),
  V("audit-stale-human-context", [
    [MIG16, "audit_actor := NULL;", "NULL;"], [MIG16, "audit_session := NULL;", "NULL;"], [MIG16, "audit_role := NULL;", "NULL;"]],
    [], [I.stale]),
  V("audit-fk-lock-order-inversion", [[MIG16,
    "organization_id uuid NULL,", "organization_id uuid NULL REFERENCES organizations(id) ON DELETE RESTRICT,"]],
    [], [I.fk]),
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
if (process.env.HARBOROPS_CG16_ANCHOR_CHECK === "1") {
  let bad = 0;
  for (const v of variants) for (const [file, anchor] of v.edits) {
    const text = expectOk("git", ["show", sha + ":" + file]);
    const count = text.split(anchor).length - 1;
    if (count !== 1) { bad++; console.error(`ANCHOR ${count}x: ${v.name}: ${anchor.slice(0, 70)}`); }
  }
  console.log(bad ? "anchor check FAILED" : "all anchors unique in " + sha.slice(0, 12));
  process.exit(bad ? 1 : 0);
}
const subset = process.env.HARBOROPS_CG16_MUTATION_SUBSET?.split(",");
assert.ok(!subset || process.env.HARBOROPS_CG16_MUTATION_SUBSET_DEV === "1", "Subset runs are development only");

const summary = { sourceSha: sha, checkedAtUtc: new Date().toISOString(), mutants: [] };
const report = path.join(root, "evidence", "combined-mutations.json");
const save = () => fs.writeFileSync(report, JSON.stringify(summary, null, 2));
const name = "ho-cg16-mutant-pg";
assert.equal(expectOk("docker", ["ps", "-aq", "--filter", "name=^/" + name + "$"]).trim(), "", "Existing mutant database container found");
const filterFor = tests => tests.map(t => "FullyQualifiedName~" + t).join("|");
let failures = 0;
try {
  for (const variant of variants.filter(v => !subset || subset.includes(v.name))) {
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
    expectOk("docker", ["run", "-d", "--name", name, "--label", "harborops.cg16mutation=disposable", "--ulimit", "core=0",
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
    else entry.status = expected.some(k => failed.some(f => f.endsWith(k))) ? "killed" : (failed.length ? "KILLED-BY-UNEXPECTED-TEST" : "SURVIVED");
    if (!["control-clean", "killed"].includes(entry.status)) failures++;
    summary.mutants.push(entry); save();
    console.log(`${entry.status}: ${variant.name} failed=[${entry.failedTests.join(", ")}]`);
    if (variant.name === "control" && entry.status !== "control-clean") break;
  }
} finally { run("docker", ["rm", "-f", name]); }
summary.survivorsOrInvalid = failures; save();
console.log(`Combined mutation summary: ${summary.mutants.length} variants, ${failures} not killed as expected`);
process.exit(failures ? 1 : 0);
