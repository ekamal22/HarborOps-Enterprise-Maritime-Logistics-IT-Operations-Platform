import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import test from "node:test";
import { checkDrift, verifyExternal, verifyManifest } from "./verify-dcsa-reference.mjs";

const sha = bytes => createHash("sha256").update(bytes).digest("hex");
const ROOT = "third_party/dcsa-track-and-trace";
const snapshotBytes = Buffer.from(JSON.stringify({ info: { version: "2.2.0", license: { name: "Apache 2.0" } } }));
const fixtureBytes = Buffer.from('{"a":1}');
const eventDomain = Buffer.from(JSON.stringify({ components: { schemas: {
  equipmentEventTypeCode: { enum: ["GTIN", "GTOT", "LOAD"] },
  transportEventTypeCode: { enum: ["ARRI", "DEPA"] },
  baseEquipmentEvent: { allOf: [{ properties: { eventType: {} } }, { properties: { eventClassifierCode: { enum: ["PLN", "ACT", "EST"] } } }] },
  baseTransportEvent: { allOf: [{ properties: { eventClassifierCode: { enum: ["ACT", "PLN", "EST"] } } }] }
} } }));

function manifest() {
  return {
    manifestVersion: 1,
    reference: { version: "2.2.0", nature: "Local snapshots, not official releases.", conformanceClaim: "none" },
    profile: {
      supportedCodes: { Equipment: ["GTIN", "GTOT"], Transport: ["ARRI", "DEPA"] }, classifiers: ["ACT"],
      upstreamEnums: {
        Equipment: { codes: "equipmentEventTypeCode", classifier: "baseEquipmentEvent" },
        Transport: { codes: "transportEventTypeCode", classifier: "baseTransportEvent" }
      }
    },
    artifacts: [
      { id: "TNT", name: "t", version: "2.2.0", upstreamUrl: "https://example.test/tnt", retrievedDate: "2026-11-03",
        sha256: sha(snapshotBytes), bytes: snapshotBytes.length, declaredLicense: "Apache-2.0",
        redistribution: "published", localPath: "snapshots/tnt.json" },
      { id: "EVENT_DOMAIN-1.0.4", name: "e", version: "1.0.4", upstreamUrl: "https://example.test/event", retrievedDate: "2026-11-03",
        sha256: sha(eventDomain), bytes: eventDomain.length, declaredLicense: null, redistribution: "withheld",
        localPath: null, externalName: "event.json", withheldReason: "not confirmed" }
    ],
    profileFixtures: [{ path: "tests/fixtures/tracking/f.json", sha256: sha(fixtureBytes) }]
  };
}
function files(overrides = {}) {
  const map = new Map([
    [ROOT + "/snapshots/tnt.json", snapshotBytes],
    [ROOT + "/LICENSE-APACHE-2.0.txt", Buffer.from("license")],
    ["tests/fixtures/tracking/f.json", fixtureBytes],
    ...Object.entries(overrides)
  ]);
  return {
    read: path => map.get(path) ?? null,
    list: dir => [...map.keys()].filter(k => k.startsWith(dir + "/")).map(k => k.slice(dir.length + 1))
  };
}

test("a consistent manifest verifies", () => assert.deepEqual(verifyManifest(manifest(), files()), []));

test("a modified snapshot is rejected", () => {
  const errors = verifyManifest(manifest(), files({ [ROOT + "/snapshots/tnt.json"]: Buffer.from(snapshotBytes.toString() + " ") }));
  assert.ok(errors.some(e => e.includes("SHA-256 differs")));
});

test("a substituted snapshot with a recomputed hash is rejected until the pin is changed", () => {
  const other = Buffer.from(JSON.stringify({ info: { version: "2.2.0", license: { name: "Apache 2.0" } }, extra: 1 }));
  const errors = verifyManifest(manifest(), files({ [ROOT + "/snapshots/tnt.json"]: other }));
  assert.ok(errors.length > 0);
});

test("a withheld artifact committed to the repository is rejected", () => {
  const errors = verifyManifest(manifest(), files({ [ROOT + "/snapshots/event.json"]: eventDomain }));
  assert.ok(errors.some(e => e.includes("withheld artifact is present")));
  assert.ok(errors.some(e => e.includes("unexpected file in snapshots")));
});

test("an unlisted file in the snapshots directory is rejected", () => {
  const errors = verifyManifest(manifest(), files({ [ROOT + "/snapshots/extra.json"]: Buffer.from("{}") }));
  assert.ok(errors.some(e => e.includes("unexpected file in snapshots: extra.json")));
});

test("publishing needs a confirmed license declaration", () => {
  const m = manifest();
  m.artifacts[0].declaredLicense = null;
  assert.ok(verifyManifest(m, files()).some(e => e.includes("confirmed Apache-2.0")));
});

test("a conformance claim or a missing provenance statement is rejected", () => {
  const m = manifest();
  m.reference.conformanceClaim = "full";
  m.reference.nature = "official";
  const errors = verifyManifest(m, files());
  assert.ok(errors.some(e => e.includes("must not claim conformance")));
  assert.ok(errors.some(e => e.includes("not official releases")));
});

test("a changed profile fixture is rejected", () => {
  const errors = verifyManifest(manifest(), files({ "tests/fixtures/tracking/f.json": Buffer.from('{"a":2}') }));
  assert.ok(errors.some(e => e.includes("profile fixture hash differs")));
});

test("external check confirms the supported subset exists upstream", () => {
  const read = name => ({ "event.json": eventDomain, "tnt.json": snapshotBytes })[name] ?? null;
  assert.deepEqual(verifyExternal(manifest(), read), []);
});

test("external check rejects a code that upstream does not define", () => {
  const m = manifest();
  m.profile.supportedCodes.Transport = ["ARRI", "XXXX"];
  const read = name => ({ "event.json": eventDomain, "tnt.json": snapshotBytes })[name] ?? null;
  assert.ok(verifyExternal(m, read).some(e => e.includes("XXXX is not in upstream")));
});

test("external check rejects a classifier that upstream does not define for the family", () => {
  const m = manifest();
  m.profile.classifiers = ["ACT", "REQ"];
  const read = name => ({ "event.json": eventDomain, "tnt.json": snapshotBytes })[name] ?? null;
  assert.ok(verifyExternal(m, read).some(e => e.includes("REQ is not in upstream baseEquipmentEvent")));
});

test("external check rejects a modified withheld copy", () => {
  const read = name => ({ "event.json": Buffer.from(eventDomain.toString() + " "), "tnt.json": snapshotBytes })[name] ?? null;
  assert.ok(verifyExternal(manifest(), read).some(e => e.includes("external SHA-256 differs")));
});

test("drift check reports same, changed and unreachable without touching the pins", async () => {
  const m = manifest();
  const before = JSON.stringify(m);
  const results = await checkDrift(m, async url => {
    if (url.endsWith("/tnt")) return snapshotBytes;
    if (url.endsWith("/event")) return Buffer.from("changed upstream");
    throw new Error("offline");
  });
  assert.deepEqual(results.map(r => r.status), ["same", "changed"]);
  const unreachable = await checkDrift(m, async () => { throw new Error("offline"); });
  assert.ok(unreachable.every(r => r.status === "unreachable"));
  assert.equal(JSON.stringify(m), before);
});

test("the committed repository snapshots verify offline", async () => {
  const { existsSync, readdirSync } = await import("node:fs");
  const repo = {
    read: rel => (existsSync(rel) ? readFileSync(rel) : null),
    list: rel => (existsSync(rel) ? readdirSync(rel) : [])
  };
  const real = JSON.parse(readFileSync(ROOT + "/MANIFEST.json", "utf8"));
  assert.deepEqual(verifyManifest(real, repo), []);
  assert.equal(real.reference.conformanceClaim, "none");
});
