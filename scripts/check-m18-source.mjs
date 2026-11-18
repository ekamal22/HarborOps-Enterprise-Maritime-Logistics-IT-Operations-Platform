// G18 source, policy and boundary checks (G18-S01 and the static parts of S12, S20 and S24).
// Usage: node scripts/check-m18-source.mjs [--baseline <sha>] [--json <file>]
// Every check records a name and PASS or FAIL. The exit code is non-zero if any check fails.
import { execFileSync } from "node:child_process";
import { readFileSync, writeFileSync, existsSync, readdirSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const argument = name => { const i = process.argv.indexOf(name); return i > 0 ? process.argv[i + 1] : undefined; };
const baseline = argument("--baseline") ?? "72251f5310f24fb98322b7eb403a5e9e66e040ef";
const out = argument("--json");
const git = (...args) => execFileSync("git", args, { cwd: root, encoding: "utf8", maxBuffer: 64 * 1024 * 1024 });
const read = file => readFileSync(resolve(root, file), "utf8");
const results = [];
const check = (name, fn) => {
  try { const detail = fn(); results.push({ name, status: "PASS", detail: detail ?? "" }); }
  catch (error) { results.push({ name, status: "FAIL", detail: String(error.message ?? error) }); }
};
const ensure = (condition, message) => { if (!condition) throw new Error(message); };

check("source-identity", () => {
  const sha = git("rev-parse", "HEAD").trim();
  const tree = git("rev-parse", "HEAD^{tree}").trim();
  ensure(/^[0-9a-f]{40}$/.test(sha) && /^[0-9a-f]{40}$/.test(tree), "unreadable revision");
  ensure(git("merge-base", "--is-ancestor", baseline, "HEAD") === "", "baseline is not an ancestor");
  return `${sha} ${tree}`;
});

check("policy-register-p01-to-p20-approved", () => {
  const text = read("docs/M18_POLICY_PROPOSAL_20261118.md");
  for (let i = 1; i <= 20; i++) {
    const id = "M18-P" + String(i).padStart(2, "0");
    const row = text.split("\n").find(line => line.startsWith("| " + id + " "));
    ensure(row, id + " row missing");
    ensure(/APPROVED 2026-11-18 \|\s*$/.test(row), id + " is not approved with its date");
  }
  ensure(!/\| (PROPOSED|PENDING|UNRESOLVED)/.test(text.split("## Approval-critical")[0]), "an unresolved status remains in the register");
});

check("assertion-inventory-shape", () => {
  const inventory = JSON.parse(read("docs/M18_G18_ASSERTION_INVENTORY_20261118.json"));
  ensure(inventory.scenarios.length === 25, "25 scenarios required");
  const ids = inventory.scenarios.flatMap(s => s.assertions.map(a => a.id));
  ensure(ids.length === 75 && new Set(ids).size === 75, "75 unique assertions required");
  for (let s = 1; s <= 25; s++) for (let a = 1; a <= 3; a++)
    ensure(ids.includes(`G18-S${String(s).padStart(2, "0")}-A0${a}`), "missing assertion " + s + "/" + a);
  ensure(JSON.stringify(inventory.allow_not_applicable) === JSON.stringify(["G18-S24-A01"]), "only G18-S24-A01 may be not applicable");
  ensure(inventory.scope.technical_assertions === 72 && inventory.scope.independent_assertions === 3, "72 technical and 3 independent assertions required");
});

check("accepted-migrations-unchanged-and-m18-is-the-only-new-migration", () => {
  const changes = git("diff", "--name-status", baseline, "HEAD", "--", "src/HarborOps.Infrastructure/Persistence").trim().split("\n").filter(Boolean);
  ensure(changes.length === 1 && /^A\tsrc\/HarborOps\.Infrastructure\/Persistence\/Migrations\/20261118190000_M18IncidentLifecycle\.cs$/.test(changes[0]),
    "unexpected persistence changes: " + changes.join("; "));
  const migrations = readdirSync(resolve(root, "src/HarborOps.Infrastructure/Persistence/Migrations")).filter(f => /^\d{14}_.*\.cs$/.test(f) && !f.endsWith(".Designer.cs")).sort();
  ensure(migrations.at(-1).startsWith("20261118190000_"), "M18 must be the newest migration");
  return migrations.length + " migrations";
});

check("hosted-workflows-and-compose-defaults-unchanged", () => {
  for (const [pathspec, label] of [[".github", "workflow files"], ["compose*.yaml", "compose files"], ["docker", "docker build files"], ["monitoring", "monitoring configuration"]]) {
    const changed = git("diff", "--name-only", baseline, "HEAD", "--", pathspec).trim();
    ensure(changed === "", label + " changed: " + changed);
  }
});

check("historical-evidence-and-other-accepted-records-not-rewritten", () => {
  const changed = git("diff", "--name-status", baseline, "HEAD", "--", "docs").trim().split("\n").filter(Boolean);
  for (const line of changed) {
    const [status, file] = line.split("\t");
    ensure(status !== "D", "documentation deleted: " + file);
    ensure(!/^docs\/(G1[0-9]_|M1[0-7]_.*(EXECUTED|GATE_CLOSURE|EVIDENCE_REVIEW|RESULT)|M16_G16)/.test(file) || status === "A",
      "accepted record modified: " + file);
  }
  return changed.length + " documentation changes";
});

check("incident-feature-is-default-off-in-api-and-web", () => {
  ensure(/GetValue<bool>\("M18:EnableIncidents"\)\) return;/.test(read("src/HarborOps.Api/Incidents/IncidentEndpoints.cs")), "API gate missing");
  ensure(/VITE_M18_ENABLED === "true"/.test(read("web/src/auth/OrganizationWorkspace.tsx")), "web gate missing");
  for (const file of readdirSync(resolve(root, "src/HarborOps.Api")).filter(f => /^appsettings.*\.json$/.test(f)))
    ensure(!/M18/.test(read("src/HarborOps.Api/" + file)), file + " enables or mentions M18");
  for (const file of readdirSync(root).filter(f => /^compose.*\.ya?ml$/.test(f)))
    ensure(!/M18|VITE_M18/.test(read(file)), file + " enables M18");
});

check("incident-route-inventory-matches-the-contract", () => {
  const source = read("src/HarborOps.Api/Incidents/IncidentEndpoints.cs");
  const routes = [...source.matchAll(/root\.Map(Get|Post|Put|Patch|Delete)\("([^"]*)"/g)].map(m => m[1].toUpperCase() + " " + m[2]).sort();
  const expected = [
    "GET ", "GET /{incidentId}", "GET /m17-prefill", "GET /read-grants", "GET /{incidentId}/history", "GET /{incidentId}/summary",
    "POST ", "POST /{incidentId}/transitions", "POST /{incidentId}/reopen", "POST /{incidentId}/notes", "POST /{incidentId}/actions",
    "POST /{incidentId}/evidence", "POST /{incidentId}/m17-snapshot", "POST /{incidentId}/recovery",
    "PATCH /{incidentId}/impact", "PATCH /{incidentId}/severity", "PATCH /{incidentId}/assignment", "PATCH /{incidentId}/actions/{actionId}",
    "PUT /read-grants/{recipientId}", "DELETE /read-grants/{recipientId}"
  ].sort();
  ensure(JSON.stringify(routes) === JSON.stringify(expected), "route set differs:\n" + routes.join("\n"));
  return routes.length + " routes";
});

check("every-incident-write-requires-csrf-origin-and-operation-id", () => {
  const source = read("src/HarborOps.Api/Incidents/IncidentEndpoints.cs");
  const writes = source.split(/root\.Map(?:Post|Put|Patch|Delete)\(/).slice(1);
  ensure(writes.length === 14, "expected 14 write routes, found " + writes.length);
  for (const body of writes) {
    const block = body.split(/root\.Map(?:Get|Post|Put|Patch|Delete)\(/)[0];
    ensure(/MutationAllowed\(ctx,anti\)/.test(block), "a write route lacks CSRF and Origin validation");
  }
  const operationRoutes = writes.filter(block => /OperationId\(ctx,out var operation\)/.test(block)).length;
  ensure(operationRoutes === 12, "12 incident commands require an operation id, found " + operationRoutes);
});

check("every-store-write-uses-the-scoped-transaction-and-an-operation-key", () => {
  const files = ["IncidentStore.cs", "IncidentStore.More.cs", "IncidentStore.Recovery.cs", "IncidentStore.Classification.cs", "IncidentStore.Paging.cs"];
  const source = files.map(f => read("src/HarborOps.Infrastructure/Incidents/" + f)).join("\n");
  const writes = (source.match(/Reserve\(conn,tx,actor,org,operationKey,/g) ?? []).length;
  ensure(writes === 11, "11 keyed commands expected, found " + writes);
  ensure(!/db\.(Add|Update|Remove|SaveChanges)/.test(source), "an incident write bypasses the scoped transaction");
  ensure(/FOR SHARE OF s,u,m,o/.test(source), "session and membership revalidation lock is missing");
  ensure(/AuditActorContext\.SetHumanAsync\(db, actor\.UserId, actor\.SessionId, correlation\.Value/.test(source), "actor and correlation context missing");
});

check("migration-installs-forced-rls-immutability-guard-and-audit-for-all-tables", () => {
  const sql = read("src/HarborOps.Infrastructure/Persistence/Migrations/20261118190000_M18IncidentLifecycle.cs");
  for (const table of ["it_incidents", "it_incident_events", "it_incident_notes", "it_incident_actions", "it_incident_action_events", "it_incident_evidence", "it_incident_operation_ids", "it_incident_read_grants"])
    ensure(sql.includes("'" + table + "'"), table + " is not in the row-level security list");
  ensure(/FORCE ROW LEVEL SECURITY/.test(sql) && /m18_incident_guard/.test(sql) && /m18_audit_capture/.test(sql) && /Retained M18 incident evidence prevents downgrade/.test(sql), "guard, audit or downgrade protection missing");
  ensure(!/(m16_audit_capture\(\))\s*;/.test(sql.split("m18_audit_capture() RETURNS")[1] ?? ""), "generic capture still attached");
  ensure(!/GRANT[^;]*(TO|TO\s+)\s*(harborops_api|harborops_worker)/.test(sql), "the migration must not grant runtime access itself");
});

check("runtime-grants-are-centralized-and-never-include-delete-truncate-or-history-update", () => {
  const source = read("src/HarborOps.Api/Audit/AuditRoleProvisioner.cs");
  const block = source.split("DO $m18_role_grants$")[1].split("END $m18_role_grants$")[0];
  ensure(!/\b(DELETE|TRUNCATE|REFERENCES|TRIGGER|ALL PRIVILEGES)\b/.test(block.replace(/REVOKE ALL/g, "")), "a forbidden privilege is granted");
  ensure(!/harborops_worker/.test(block), "the worker role must receive nothing");
  ensure(!/GRANT UPDATE ON it_incident_(events|notes|action_events|evidence)\b/.test(block), "history update granted");
});

check("no-automation-attribution-or-em-dashes-in-added-repository-text", () => {
  const diff = git("diff", baseline, "HEAD", "--", ".", ":(exclude)web/package-lock.json");
  const added = diff.split("\n").filter(line => line.startsWith("+") && !line.startsWith("+++"));
  const emDash = added.filter(line => line.includes("—"));
  ensure(emDash.length === 0, emDash.length + " added lines contain an em dash: " + emDash[0]);
  const marker = /co-authored-by|generated with|claude code|ai[- ]generated|chatgpt|copilot|anthropic/i;
  const hits = added.filter(line => marker.test(line));
  ensure(hits.length === 0, "attribution marker in added text: " + hits[0]);
  const log = git("log", "--format=%B", baseline + "..HEAD");
  ensure(!marker.test(log), "attribution marker in commit messages");
});

check("no-hosted-verification-is-configured-by-this-change", () => {
  const files = git("diff", "--name-only", baseline, "HEAD").trim().split("\n");
  ensure(!files.some(f => f.startsWith(".github/")), "a workflow file changed");
  const added = git("diff", baseline, "HEAD", "--", "scripts", "tests").split("\n").filter(l => l.startsWith("+"));
  ensure(!added.some(l => /gh (workflow|run) |workflow_dispatch/.test(l)), "a script invokes hosted workflows");
});

check("test-inventory-is-present-for-every-mapped-witness", () => {
  const mapPath = resolve(root, "tests/g18/assertion-map.json");
  ensure(existsSync(mapPath), "assertion map missing");
  const map = JSON.parse(readFileSync(mapPath, "utf8"));
  const sources = ["tests/HarborOps.IntegrationTests", "tests/HarborOps.UnitTests", "tests/browser/g18", "web/tests", "scripts"]
    .flatMap(dir => readdirSync(resolve(root, dir), { recursive: true }).map(f => resolve(root, dir, String(f))).filter(f => /\.(cs|mjs)$/.test(f)))
    .map(f => readFileSync(f, "utf8")).join("\n");
  const missing = [];
  for (const [id, entry] of Object.entries(map.assertions))
    for (const witness of entry.witnesses ?? []) {
      const name = witness.replace(/^(test|browser|node|check|stage|mutation|journey|q3):/, "").split("(")[0].split(" ")[0];
      if (/^(test|browser):/.test(witness) && !sources.includes(name.split(".").at(-1))) missing.push(id + " -> " + witness);
    }
  ensure(missing.length === 0, "mapped witnesses without a test: " + missing.slice(0, 5).join("; "));
  ensure(Object.keys(map.assertions).length === 75, "the map must name all 75 assertions");
});

const failed = results.filter(r => r.status === "FAIL");
for (const r of results) console.log(`${r.status} ${r.name}${r.detail ? " :: " + String(r.detail).split("\n")[0].slice(0, 200) : ""}`);
if (out) writeFileSync(out, JSON.stringify({ baseline, results }, null, 2));
process.exit(failed.length ? 1 : 0);
