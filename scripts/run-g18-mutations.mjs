// G18 controlled fault (mutation) campaign.
// Usage: node scripts/run-g18-mutations.mjs --sha <40 hex> --out <E: directory> [--only id,id] [--port 55430]
// Every mutant is applied to a disposable detached worktree of the frozen source, never to the working
// tree. A compile failure is classified invalid and is never counted as a kill. A mutant is killed only
// when a designated behavioural test fails on assertion text, survived when everything passes.
import { execFileSync, spawnSync } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync, appendFileSync, existsSync } from "node:fs";
import { resolve, join } from "node:path";

const arg = (name, fallback) => { const i = process.argv.indexOf(name); return i > 0 ? process.argv[i + 1] : fallback; };
const sha = arg("--sha");
const out = arg("--out");
const only = arg("--only")?.split(",");
const port = arg("--port", "55430");
if (!/^[0-9a-f]{40}$/.test(sha ?? "") || !/^[Ee]:[\\/]/.test(out ?? "")) throw new Error("--sha (40 hex) and an E: --out directory are required");
const repo = resolve(import.meta.dirname, "..");
mkdirSync(out, { recursive: true });
const resultsFile = join(out, "mutation-results.jsonl");
const run = (cmd, args, options = {}) => spawnSync(cmd, args, { encoding: "utf8", maxBuffer: 256 * 1024 * 1024, ...options });

const SECURITY = "FullyQualifiedName~M18SecurityTests", DATABASE = "FullyQualifiedName~M18DatabaseTests",
  LIFECYCLE = "FullyQualifiedName~M18LifecycleTests", RESILIENCE = "FullyQualifiedName~M18ResilienceTests",
  EVIDENCE = "FullyQualifiedName~M18EvidenceTests", CONTRACT = "FullyQualifiedName~M18ContractTests";
const STORE = "src/HarborOps.Infrastructure/Incidents/IncidentStore.cs";
const MIGRATION = "src/HarborOps.Infrastructure/Persistence/Migrations/20261118190000_M18IncidentLifecycle.cs";
const POLICY = "src/HarborOps.Domain/Incidents/IncidentPolicy.cs";

// kind: expected classification when everything works as designed
const mutants = [
  { id: "remove-app-tenant-predicate", expected: "equivalent",
    note: "Defense in depth: forced row-level security still hides foreign rows, so removing only the application predicate is not observable.",
    patches: [[STORE, "FROM it_incidents WHERE organization_id=@org AND id=@id
            {(lockRow ? \"FOR UPDATE\" : \"\")}", "FROM it_incidents WHERE id=@id
            {(lockRow ? \"FOR UPDATE\" : \"\")}"]], filter: SECURITY },
  { id: "remove-tenant-predicate", expected: "killed",
    note: "Removes both the application predicate and the row-level policy.",
    patches: [[STORE, "FROM it_incidents WHERE organization_id=@org AND id=@id
            {(lockRow ? \"FOR UPDATE\" : \"\")}", "FROM it_incidents WHERE id=@id
            {(lockRow ? \"FOR UPDATE\" : \"\")}"],
      [MIGRATION, "USING (m18_scoped_session(organization_id)) WITH CHECK (m18_scoped_session(organization_id))", "USING (true) WITH CHECK (true)"]],
    filter: SECURITY },
  { id: "widen-role-grant", expected: "killed",
    patches: [["src/HarborOps.Domain/Organizations/OrganizationPolicy.cs",
      "role is (OrganizationRole.Manager or OrganizationRole.Support or OrganizationRole.Administrator))",
      "role is (OrganizationRole.Analyst or OrganizationRole.Manager or OrganizationRole.Support or OrganizationRole.Administrator))"]],
    filter: SECURITY },
  { id: "ignore-revocation-after-lock", expected: "killed",
    patches: [[STORE, "AND m.status='Active' AND o.status='Active'\n            FOR SHARE OF s,u,m,o", "AND o.status='Active'\n            FOR SHARE OF s,u,m,o"]],
    filter: RESILIENCE },
  { id: "bypass-transition-guard", expected: "killed",
    patches: [[POLICY, "if (!valid) return false;", "if (!valid) { }"]], filter: LIFECYCLE },
  { id: "drop-database-transition-guard", expected: "killed",
    patches: [[MIGRATION, "CREATE TRIGGER m18_incidents_guard BEFORE UPDATE ON it_incidents\n                FOR EACH ROW EXECUTE FUNCTION m18_incident_guard();", ""]],
    filter: DATABASE },
  { id: "lose-version-compare", expected: "killed",
    patches: [[STORE.replace("IncidentStore.cs", "IncidentStore.cs"), "var current=await Read(conn,tx,org,id,true,token);\n                if(current.Version!=input.ExpectedVersion)throw Conflict();\n                await using(var insert=Command(conn,tx,\"\"\"\n                    INSERT INTO it_incident_notes",
      "var current=await Read(conn,tx,org,id,true,token);\n                await using(var insert=Command(conn,tx,\"\"\"\n                    INSERT INTO it_incident_notes"]],
    filter: RESILIENCE },
  { id: "duplicate-event-on-replay", expected: "killed",
    patches: [[STORE, "if (await insert.ExecuteScalarAsync(ct) is not null) return null;", "await insert.ExecuteScalarAsync(ct); return null;"]],
    filter: RESILIENCE },
  { id: "drop-immutable-history-guard", expected: "killed",
    patches: [[MIGRATION, "CREATE TRIGGER m18_events_immutable\n                BEFORE UPDATE OR DELETE OR TRUNCATE ON it_incident_events\n                FOR EACH STATEMENT EXECUTE FUNCTION m18_history_immutable();", ""]],
    filter: DATABASE },
  { id: "remove-required-audit", expected: "killed",
    patches: [[MIGRATION, "CREATE TRIGGER m18_notes_audit AFTER INSERT ON it_incident_notes\n                FOR EACH ROW EXECUTE FUNCTION m18_audit_capture();", ""]],
    filter: DATABASE },
  { id: "leak-note-into-audit", expected: "killed",
    patches: [[MIGRATION, "jsonb_build_object('operation', lower(TG_OP), 'changedFields', changed);", "jsonb_build_object('operation', lower(TG_OP), 'changedFields', changed, 'row', row_json);"]],
    filter: DATABASE },
  { id: "accept-arbitrary-evidence-url", expected: "killed",
    patches: [[POLICY, "c is '-' or '_'))", "c is '-' or '_' or ':' or '/' or '.' or '@'))"]], filter: CONTRACT },
  { id: "treat-missing-telemetry-as-healthy", expected: "killed",
    patches: [["src/HarborOps.Api/Observability/M17Telemetry.cs", "if (unix <= 0) return (\"unknown\", now);", "if (unix <= 0) return (\"healthy\", now);"],
      ["src/HarborOps.Api/Observability/M17Telemetry.cs", "observed < now.AddSeconds(-45))\n            return (\"unknown\", now);", "observed < now.AddSeconds(-45))\n            return (\"healthy\", now);"]],
    filter: EVIDENCE },
  { id: "skip-recovery-proof", expected: "killed",
    patches: [[STORE, "if(Convert.ToInt64(await fresh.ExecuteScalarAsync(token))!=2)throw Conflict();", "await fresh.ExecuteScalarAsync(token);"]],
    filter: EVIDENCE },
  { id: "skip-independent-closure", expected: "killed",
    patches: [[STORE, "independentlyConfirmed= !IncidentPolicy.IndependentClosureRequired(severity) ||\n                        (resolvedBy.HasValue && resolvedBy.Value!=actor.UserId);", "independentlyConfirmed= true;"]],
    filter: LIFECYCLE },
];

function pg(action, name) {
  if (action === "start") {
    run("docker", ["rm", "-f", name]);
    const r = run("docker", ["run", "-d", "--name", name, "--label", "harborops.g18=disposable", "--label", "harborops.g18.attempt=mutation",
      "--ulimit", "core=0", "--tmpfs", "/var/lib/postgresql/data:uid=70,gid=70,mode=0700", "--cap-drop", "ALL", "--security-opt", "no-new-privileges",
      "-p", `127.0.0.1:${port}:5432`, "-e", "POSTGRES_DB=harborops_integration", "-e", "POSTGRES_USER=harborops", "-e", "POSTGRES_HOST_AUTH_METHOD=trust",
      "harborops-g13g15-postgres:16.15-rootless", "-c", "fsync=off", "-c", "max_connections=300"]);
    if (r.status !== 0) throw new Error("database start failed: " + r.stderr);
    for (let i = 0; i < 60; i++) {
      if (run("docker", ["exec", name, "pg_isready", "-U", "harborops", "-d", "harborops_integration"]).status === 0) return;
      run("node", ["-e", "setTimeout(()=>{},1000)"]);
    }
    throw new Error("database not ready");
  }
  const label = run("docker", ["inspect", "-f", "{{index .Config.Labels \"harborops.g18\"}}", name]).stdout.trim();
  if (label !== "disposable") throw new Error("refusing to remove a container that is not an owned disposable one: " + name);
  run("docker", ["rm", "-f", name]);
}

const baselineBuild = { done: false };
for (const mutant of mutants) {
  if (only && !only.includes(mutant.id)) continue;
  const started = Date.now();
  const worktree = join(out, "wt-" + mutant.id);
  const record = { id: mutant.id, expected: mutant.expected, note: mutant.note ?? "", source_sha: sha, filter: mutant.filter };
  try {
    if (!existsSync(worktree)) execFileSync("git", ["worktree", "add", "--detach", worktree, sha], { cwd: repo, stdio: "pipe" });
    for (const [file, from, to] of mutant.patches) {
      const path = join(worktree, file);
      const text = readFileSync(path, "utf8").replace(/\r\n/g, "\n");
      const count = text.split(from).length - 1;
      if (count !== 1) throw Object.assign(new Error(`patch target found ${count} times in ${file}`), { invalid: true });
      writeFileSync(path, text.replace(from, to));
    }
    const build = run("dotnet", ["build", "tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj", "-c", "Release", "-nologo"], { cwd: worktree });
    writeFileSync(join(out, `build-${mutant.id}.log`), build.stdout + build.stderr);
    record.build_exit = build.status;
    if (build.status !== 0) { record.status = "invalid"; record.reason = "the mutant does not compile, so it is not a behavioural kill"; }
    else {
      const name = "ho-verify-g18-mut-" + port;
      pg("start", name);
      const env = { ...process.env, HARBOROPS_TEST_DB: `Host=127.0.0.1;Port=${port};Database=harborops_integration;Username=harborops`,
        HARBOROPS_G12_EXCLUSIVE_POSTGRES: "1" };
      const test = run("dotnet", ["test", "tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj", "-c", "Release", "--no-build", "--filter", mutant.filter,
        "--logger", `trx;LogFileName=mut-${mutant.id}.trx`, "--results-directory", out], { cwd: worktree, env });
      pg("stop", name);
      const log = test.stdout + test.stderr;
      writeFileSync(join(out, `test-${mutant.id}.log`), log);
      record.test_exit = test.status;
      const failed = [...log.matchAll(/^\s+Failed (\S+)/gm)].map(m => m[1]);
      record.failed_tests = [...new Set(failed)];
      const environmental = /Requires HARBOROPS_G12_EXCLUSIVE|connection refused|Failed to connect|timeout expired/i.test(log) && !/Assert\.|Expected:|Actual:/.test(log);
      if (test.status === 0) record.status = "survived";
      else if (environmental) { record.status = "inconclusive"; record.reason = "failure looks environmental, not an assertion"; }
      else record.status = "killed";
    }
  } catch (error) {
    record.status = error.invalid ? "invalid" : "error";
    record.reason = String(error.message ?? error).slice(0, 400);
  }
  record.duration_s = Math.round((Date.now() - started) / 1000);
  record.matches_expectation = (record.status === mutant.expected);
  appendFileSync(resultsFile, JSON.stringify(record) + "\n");
  console.log(`${record.status.toUpperCase()} ${mutant.id} (${record.duration_s}s) failed=${(record.failed_tests ?? []).length}`);
}
