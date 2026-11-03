import test from "node:test";
import assert from "node:assert/strict";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { verifyManifest } from "./verify-dcsa-reference.mjs";

const source = name => readFileSync(name, "utf8");
const schema = JSON.parse(source("contracts/tracking/v1/harborops-tracking-v1.schema.json"));
const validEquipment = JSON.parse(source("tests/fixtures/tracking/valid-equipment.json"));
const validTransport = JSON.parse(source("tests/fixtures/tracking/valid-transport.json"));
const rejected = JSON.parse(source("tests/fixtures/tracking/invalid-tenant-spoof.json"));
const policy = source("src/HarborOps.Domain/Tracking/TrackingEventPolicy.cs");
const endpoint = source("src/HarborOps.Api/Integrations/TrackingEndpoints.cs");
const migration = source("src/HarborOps.Infrastructure/Persistence/Migrations/20261103090000_IntegrationSources.cs");

test("M7 profile has one canonical set of wire keys", () => {
  const required = ["sourceEventId", "eventFamily", "eventCode",
    "classifier", "occurredAtUtc", "externalSchemaVersion"];
  assert.deepEqual(schema.required, required);
  assert.equal(schema.additionalProperties, false);
  assert.ok(!Object.hasOwn(schema.properties, "family"));
  assert.ok(!Object.hasOwn(schema.properties, "code"));
  assert.equal(schema.properties.externalSchemaVersion.const, "2.2.0");
  assert.equal(schema.properties.classifier.const, "ACT");
  assert.match(policy, /Profile = "tnt-2[.]2-harborops-v1"/);
  for (const key of required) {
    assert.ok(Object.hasOwn(schema.properties, key));
    assert.ok(policy.includes('"' + key + '"'), "Parser missing canonical key: " + key);
  }
  assert.ok(endpoint.includes("TrackingEventPolicy.Profile"));
});

test("Synthetic fixtures match the restrictive variant contract", () => {
  for (const [fixture, family, codes, requiredRef, forbiddenRef] of [
    [validEquipment, "Equipment", ["GTIN", "GTOT"], "equipmentReference", "transportReference"],
    [validTransport, "Transport", ["ARRI", "DEPA"], "transportReference", "equipmentReference"]
  ]) {
    for (const key of schema.required) assert.equal(typeof fixture[key], "string", key);
    assert.equal(fixture.eventFamily, family);
    assert.ok(codes.includes(fixture.eventCode));
    assert.equal(typeof fixture[requiredRef], "string");
    assert.ok(!Object.hasOwn(fixture, forbiddenRef));
    assert.equal(Object.keys(fixture).filter(k => !Object.hasOwn(schema.properties, k)).length, 0);
  }
  assert.ok(Object.hasOwn(rejected, "organizationId"));
  assert.ok(!Object.hasOwn(schema.properties, "organizationId"));
  assert.ok(schema.oneOf.some(v => v.properties.eventFamily.const === "Equipment"));
  assert.ok(schema.oneOf.some(v => v.properties.eventFamily.const === "Transport"));
});

test("EF Core model and snapshot share the same M7 metadata", () => {
  const db = source("src/HarborOps.Infrastructure/Persistence/HarborOpsDbContext.cs");
  const snapshot = source("src/HarborOps.Infrastructure/Persistence/Migrations/HarborOpsDbContextModelSnapshot.cs");
  const select = text => {
    const start = text.indexOf("        modelBuilder.Entity<IntegrationSource>(entity =>");
    const end = text.indexOf("        modelBuilder.Entity<BootstrapRecord>(entity =>", start);
    assert.ok(start >= 0 && end > start, "M7 mappings malformed or missing");
    return text.slice(start, end);
  };
  const a = select(db);
  const b = select(snapshot);
  assert.equal(a, b, "EF model and snapshot differ");
  assert.ok(db.includes("DbSet<IntegrationSource>"));
  assert.ok(db.includes("DbSet<IntegrationCredential>"));
  assert.ok(a.includes("source_code ~ '^[a-z][a-z0-9_-]{2,79}$'"));
  assert.equal((db.match(/modelBuilder.Entity<IntegrationSource>/g) || []).length, 1);
  assert.equal((snapshot.match(/modelBuilder.Entity<IntegrationCredential>/g) || []).length, 1);
});

test("Database has tenant constraints and lifecycle guards", () => {
  for (const marker of [
    "REFERENCES organizations(id) ON DELETE RESTRICT",
    "UNIQUE (organization_id,source_code)",
    "FOREIGN KEY (organization_id,source_id)",
    "CK_m7_source_name",
    "CREATE FUNCTION m7_guard_source()",
    "CREATE FUNCTION m7_guard_credential()",
    "CREATE TRIGGER m7_source_guard",
    "CREATE TRIGGER m7_credential_guard",
    "Credential overlap limit exceeded",
    "Immutable credential identity",
    "DROP FUNCTION m7_guard_source()",
    "DROP FUNCTION m7_guard_credential()"
  ]) assert.ok(migration.includes(marker), "Missing migration invariant: " + marker);
});

test("Authenticated intake cannot acknowledge without durable M8 receipt", () => {
  assert.match(endpoint, /AuthenticateAsync/);
  assert.match(endpoint, /preauth:/);
  assert.match(endpoint, /intake_unavailable/);
  assert.ok(endpoint.includes("503"));
  assert.ok(!endpoint.includes("Results.Accepted("));
  assert.ok(!endpoint.includes("Results.Created("));
  assert.match(endpoint, /Bind\(source.Id, source.OrganizationId\)/);
});


test("Source authentication is refreshed under lock and rate limiter stays bounded", () => {
  const source = readFileSync("src/HarborOps.Infrastructure/Integrations/IntegrationSourceStore.cs", "utf8");
  const limiter = readFileSync("src/HarborOps.Api/Integrations/IntegrationRequestLimiter.cs", "utf8");
  for(const marker of [
    "FOR UPDATE", "ReloadAsync(ct)", "FOR SHARE OF c, s, o",
    "CryptographicOperations.FixedTimeEquals", "source.Version++",
    "MaximumOverlap", "BeginTransactionAsync"
  ]) assert.ok(source.includes(marker), "Lifecycle invariant missing: " + marker);
  assert.ok(!source.includes("FOR UPDATE\").SingleAsync("),
    "Do not compose SingleAsync over a locking query");
  assert.ok(limiter.includes("MaximumPartitions = 4096"),
    "Remote partition allocation needs an explicit capacity limit");
  assert.ok(limiter.includes("windows.Count >= MaximumPartitions"),
    "Partition cap must reject unbounded entries");
});

test("G7 technical runner uses isolated resources and controlled mutation checks", () => {
  const runner = readFileSync("scripts/verify-m7-local.sh", "utf8");
  const mutation = readFileSync("scripts/run-g7-mutations.mjs", "utf8");
  const network = readFileSync("scripts/check-m7-compose-network.mjs", "utf8");
  for (const marker of [
    "check-m3-docker-storage.mjs", "check-m7-compose-network.mjs",
    "stage security-mutations", "stage dotnet-regressions", "stage browsers",
    "stage source-outage-recovery", "stage image-scans", "stage evidence-summary", "ho-verify-g7",
    "trap cleanup EXIT"
  ]) assert.ok(runner.includes(marker), "G7 stage missing: "+marker);
  const summary = readFileSync("scripts/summarize-g7-evidence.mjs", "utf8");
  assert.ok(summary.includes("D-002 acceptance pending"), "D-002 block must stay recorded");
  for (let i = 1; i <= 25; i++)
    assert.ok(summary.includes("G7-S" + String(i).padStart(2, "0")), "Summary scenario missing: " + i);
  assert.ok(network.includes("172.30.253.0/24"));
  for(const name of ["digest","suspension","revocation","size","credential-guard"])
    assert.ok(mutation.includes('"'+name+'"'), "Security mutant missing: "+name);
});

test("G7 documents all required cases without claiming acceptance", () => {
  const plan = readFileSync("docs/M7_VERIFICATION_PLAN.md", "utf8");
  const status = readFileSync("docs/M7_IMPLEMENTATION_STATUS.md", "utf8");
  for(let i=1;i<=25;i++)
    assert.ok(plan.includes("G7-S"+String(i).padStart(2,"0")),
      "G7 gate case missing: "+i);
  const g7Line = status.split("\n").find(line => line.startsWith("**G7:**")) ?? "";
  assert.ok(g7Line.includes("acceptance pending"), "G7 must not be recorded as accepted");
});

test("D-002 reference snapshots are pinned, withheld artifacts stay out and the profile matches the policy", () => {
  const manifest = JSON.parse(source("third_party/dcsa-track-and-trace/MANIFEST.json"));
  const io = {
    read: rel => (existsSync(rel) ? readFileSync(rel) : null),
    list: rel => (existsSync(rel) ? readdirSync(rel) : [])
  };
  assert.deepEqual(verifyManifest(manifest, io), []);
  assert.equal(manifest.reference.conformanceClaim, "none");
  const policy = source("src/HarborOps.Domain/Tracking/TrackingEventPolicy.cs");
  assert.ok(policy.includes('Profile = "' + manifest.profile.id + '"'), "profile id must match the policy");
  for (const code of [...manifest.profile.supportedCodes.Equipment, ...manifest.profile.supportedCodes.Transport])
    assert.ok(policy.includes('"' + code + '"'), "supported code missing from the policy: " + code);
  assert.deepEqual(manifest.profile.classifiers, ["ACT"]);
  assert.ok(policy.includes('classifier != "ACT"'), "classifier restriction must match the manifest");
  const published = manifest.artifacts.filter(a => a.redistribution === "published");
  assert.deepEqual(published.map(a => a.id), ["DCSA_TNT-2.2.0"]);
  const readme = source("third_party/dcsa-track-and-trace/README.md");
  for (const phrase of ["not official immutable DCSA releases", "not DCSA conformant", "translation layer"])
    assert.ok(readme.includes(phrase), "provenance statement missing: " + phrase);
});
