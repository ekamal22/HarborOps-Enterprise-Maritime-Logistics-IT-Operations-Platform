// G16 controlled fault injection. Runs on an exported copy of the exact source revision, never on
// the checkout. Each mutant applies exact-anchor edits (each anchor must occur exactly once), must
// compile, and must be killed by at least one of the named tests. A surviving or non-compiling
// mutant, or a failing control, fails the run.
// Usage: node scripts/run-g16-mutations.mjs <E: verification root>   (env HARBOROPS_SOURCE_SHA)
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";

const root = process.argv[2], sha = process.env.HARBOROPS_SOURCE_SHA;
assert.ok(root && /^(?:[Ee]:[\\/]|\/e\/)/.test(root), "E: verification root required");
assert.match(sha || "", /^[0-9a-f]{40}$/);
const evidence = path.join(root, "evidence", "mutations");
fs.mkdirSync(evidence, { recursive: true });
const cache = path.join(root, "cache");
const sourceTar = path.join(cache, "g16-mutation-" + sha.slice(0, 12) + ".tar");
const exported = path.join(cache, "g16-mutation-export-" + sha.slice(0, 12));
assert.ok(!fs.existsSync(exported), "Mutation export already exists; use a new verification root");
const run = (cmd, args, options = {}) => {
  const result = spawnSync(cmd, args, { encoding: "utf8", timeout: 1200000, maxBuffer: 64 * 1024 * 1024, ...options });
  if (result.error) throw result.error;
  return result;
};
const expectOk = (cmd, args, options) => {
  const result = run(cmd, args, options);
  assert.equal(result.status, 0, cmd + " failed: " + (result.stderr || result.stdout || "").slice(-600));
  return result.stdout || "";
};
expectOk("git", ["archive", "--format=tar", "--output", sourceTar, sha]);

const MIG = "src/HarborOps.Infrastructure/Persistence/Migrations/20261104180000_AuditEntries.cs";
const POLICY = "src/HarborOps.Domain/Organizations/OrganizationPolicy.cs";
const READER = "src/HarborOps.Infrastructure/Audit/AuditReader.cs";
const ROLES = "src/HarborOps.Api/Audit/AuditRoleProvisioner.cs";
const E = "AuditIntegrityTests.BusinessChangesGenerateMinimalAppendOnlyEvidence";
const ROLE_TEST = "AuditHardeningTests.ProvisionedRuntimeRolesCannotModifyAuditEvidenceOrRunDdl";
const variants = [
  { name: "control", edits: [], killers: [] },
  { name: "no-organization-capture", killers: [E], edits: [[MIG,
    "CREATE TRIGGER m16_organizations_audit AFTER INSERT OR UPDATE OR DELETE ON organizations",
    "CREATE TRIGGER m16_organizations_audit AFTER DELETE ON organizations"]] },
  { name: "no-truncate-guard", killers: [E], edits: [[MIG,
    "FOR EACH STATEMENT EXECUTE FUNCTION m16_audit_guard();",
    "FOR EACH STATEMENT EXECUTE FUNCTION m16_audit_guard();\n            DROP TRIGGER m16_audit_no_truncate ON audit_entries;"]] },
  { name: "no-delete-guard", killers: [E], edits: [[MIG,
    "BEFORE UPDATE OR DELETE ON audit_entries", "BEFORE UPDATE ON audit_entries"]] },
  { name: "swallow-audit-failure", killers: ["AuditIntegrityTests.AuditInsertFailureAbortsTheOriginalBusinessMutation"], edits: [
    [MIG, "EXECUTE format(", "BEGIN\n                EXECUTE format("],
    [MIG, "'operation', operation_code, 'changedFields', changed_fields);",
      "'operation', operation_code, 'changedFields', changed_fields);\n                EXCEPTION WHEN OTHERS THEN NULL; END;"]] },
  { name: "unredacted-metadata", killers: ["AuditHardeningTests.AuditMetadataKeysAndFieldNamesStayWithinTheFixedAllowlist"], edits: [[MIG,
    "'operation', operation_code, 'changedFields', changed_fields);",
    "'operation', operation_code, 'changedFields', changed_fields, 'row', row_data);"]] },
  { name: "stale-human-context", killers: ["AuditHardeningTests.SourceBackedAuditNeverInheritsAStaleHumanOrCliContext"], edits: [
    [MIG, "audit_actor := NULL;", "NULL;"], [MIG, "audit_session := NULL;", "NULL;"], [MIG, "audit_role := NULL;", "NULL;"]] },
  { name: "audit-fk-lock-inversion", killers: ["AuditIntegrityTests.AuditInsertionDoesNotLockTheOrganizationRowBehindRevocations"], edits: [[MIG,
    "organization_id uuid NULL,", "organization_id uuid NULL REFERENCES organizations(id) ON DELETE RESTRICT,"]] },
  { name: "widened-audit-permission", killers: ["AuditPermissionTests.AuditPermissionDoesNotBroadenAdministrativeGrants",
    "AuditPermissionTests.OnlyAuditorHasExplicitAuditReadPermission", "AuditHttpTests.AuditorOnlyTenantScopedReadAndRevocationAreEnforced"], edits: [[POLICY,
    "(permission == OrganizationPermission.ViewAuditEvents && role == OrganizationRole.Auditor)",
    "(permission == OrganizationPermission.ViewAuditEvents && role is (OrganizationRole.Auditor or OrganizationRole.Administrator))"]] },
  { name: "removed-tenant-predicate", killers: ["AuditHttpTests.AuditorOnlyTenantScopedReadAndRevocationAreEnforced",
    "AuditCursorTests.CursorWalkHasNoDuplicatesOrGapsAndRejectsTamperedOrCrossTenantCursors"], edits: [[READER,
    ".Where(e => e.OrganizationId == organizationId)", ".Where(e => e.Id != Guid.Empty)"]] },
  { name: "cursor-ignores-organization", killers: ["AuditCursorTests.CursorWalkHasNoDuplicatesOrGapsAndRejectsTamperedOrCrossTenantCursors"], edits: [[READER,
    "org != organizationId ||", ""]] },
  { name: "api-can-delete-audit", killers: [ROLE_TEST], edits: [[ROLES,
    "GRANT SELECT ON TABLE audit_entries TO harborops_api;", "GRANT SELECT, DELETE ON TABLE audit_entries TO harborops_api;"]] },
  { name: "public-can-create-objects", killers: [ROLE_TEST], edits: [[ROLES,
    "REVOKE CREATE ON SCHEMA public FROM PUBLIC;", "GRANT CREATE ON SCHEMA public TO PUBLIC;"]] },
  { name: "worker-can-read-audit", killers: [ROLE_TEST], edits: [[ROLES,
    "REVOKE ALL ON TABLE audit_entries FROM harborops_worker;", "GRANT SELECT ON TABLE audit_entries TO harborops_worker;"]] }
];
const subset = process.env.HARBOROPS_G16_MUTATION_SUBSET?.split(",");
assert.ok(!subset || process.env.HARBOROPS_G16_MUTATION_SUBSET_DEV === "1", "Subset runs are development only");

const summary = { sourceSha: sha, checkedAtUtc: new Date().toISOString(), mutants: [] };
const report = path.join(root, "evidence", "g16-mutations.json");
const save = () => fs.writeFileSync(report, JSON.stringify(summary, null, 2));
const name = "ho-g16-mutant-pg";
assert.equal(expectOk("docker", ["ps", "-aq", "--filter", "name=^/" + name + "$"]).trim(), "", "Existing mutant database container found");
const FILTER = "FullyQualifiedName~AuditIntegrityTests|FullyQualifiedName~AuditHardeningTests|FullyQualifiedName~AuditHttpTests|FullyQualifiedName~AuditCursorTests|FullyQualifiedName~AuditMigrationTests|FullyQualifiedName~MigrationSnapshotTests";
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
      fs.writeFileSync(target, text.replace(anchor, () => replacement));
    }
    run("docker", ["rm", "-f", name]);
    expectOk("docker", ["run", "-d", "--name", name, "--label", "harborops.g16mutation=disposable", "--ulimit", "core=0",
      "--tmpfs", "/var/lib/postgresql/data:uid=70,gid=70,mode=0700", "--cap-drop", "ALL", "--security-opt", "no-new-privileges",
      "-e", "POSTGRES_DB=harborops_integration", "-e", "POSTGRES_USER=harborops", "-e", "POSTGRES_HOST_AUTH_METHOD=trust",
      "harborops-postgres:16.15-rootless"]);
    let ready = false;
    for (let k = 0; k < 60 && !ready; k++) {
      ready = run("docker", ["exec", name, "pg_isready", "-U", "harborops", "-d", "harborops_integration"]).status === 0;
      if (!ready) await new Promise(r => setTimeout(r, 500));
    }
    assert.ok(ready, "Mutation database not ready");
    const shell = "dotnet restore HarborOps.sln -v q && dotnet build HarborOps.sln -c Release --no-restore -v q -warnaserror && " +
      "{ dotnet test tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj -c Release --no-build --filter 'FullyQualifiedName~AuditPermission'; " +
      "dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj -c Release --no-build --filter '" + FILTER + "'; }";
    // The runner exports NUGET_PACKAGES for host tools; the SDK package cache used by the other
    // stages is <root>/cache/nuget/packages. A development override is available.
    const cacheDir = process.env.G16_NUGET_PACKAGES ?? path.join(cache, "nuget", "packages");
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
    const compiled = !/error (CS|MSB|xUnit)\d+/.test(output) && /Passed!|Failed!/.test(output);
    const entry = { variant: variant.name, compiled, exitCode: result.status, failedTests: [...new Set(failed)], expectedKillers: variant.killers };
    if (variant.name === "control") entry.status = compiled && result.status === 0 && failed.length === 0 ? "control-clean" : "CONTROL-FAILED";
    else if (!compiled) entry.status = "INVALID-MUTANT (did not compile)";
    else entry.status = variant.killers.some(k => failed.some(f => f.endsWith(k))) ? "killed" : (failed.length ? "KILLED-BY-UNEXPECTED-TEST" : "SURVIVED");
    if (!["control-clean", "killed"].includes(entry.status)) failures++;
    summary.mutants.push(entry); save();
    console.log(`${entry.status}: ${variant.name} failed=[${entry.failedTests.join(", ")}]`);
  }
} finally { run("docker", ["rm", "-f", name]); }
summary.survivorsOrInvalid = failures; save();
console.log(`G16 mutation summary: ${summary.mutants.length} variants, ${failures} not killed as expected`);
process.exit(failures ? 1 : 0);
