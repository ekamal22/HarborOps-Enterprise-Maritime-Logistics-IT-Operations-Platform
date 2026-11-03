import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

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
