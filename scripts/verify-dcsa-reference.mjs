#!/usr/bin/env node
// Verify the pinned DCSA Track & Trace reference snapshots.
//
//   node scripts/verify-dcsa-reference.mjs
//       Offline. Checks the committed snapshots, the profile fixtures and the manifest rules.
//   node scripts/verify-dcsa-reference.mjs --external-dir <dir>
//       Also checks the withheld artifacts held outside the repository and confirms that every
//       supported event code and classifier exists in the pinned upstream enumerations.
//   node scripts/verify-dcsa-reference.mjs --upstream
//       Optional drift check. Fetches the hosted versions and compares their hashes with the pins.
//       It never writes files and never updates a pin.
//
// Exit codes: 0 verified, 1 verification failure, 3 upstream drift detected, 4 upstream unreachable.
import { createHash } from "node:crypto";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
const HEX64 = /^[0-9a-f]{64}$/;
const DATE = /^\d{4}-\d{2}-\d{2}$/;
const ROOT_DIR = "third_party/dcsa-track-and-trace";

// io.read(relativePath) returns a Buffer or null; io.list(relativeDirectory) returns file names.
export function verifyManifest(manifest, io) {
  const errors = [];
  const fail = message => errors.push(message);
  if (manifest?.manifestVersion !== 1) fail("manifestVersion must be 1");
  if (manifest?.reference?.conformanceClaim !== "none") fail("the reference must not claim conformance");
  if (!/not official/i.test(manifest?.reference?.nature ?? ""))
    fail("the reference must state that the snapshots are not official releases");
  const artifacts = manifest?.artifacts;
  if (!Array.isArray(artifacts) || artifacts.length === 0) fail("artifacts are required");
  const published = new Set();
  for (const a of artifacts ?? []) {
    const label = a?.id ?? "(unnamed artifact)";
    for (const field of ["id", "name", "version", "upstreamUrl", "retrievedDate", "sha256", "redistribution"])
      if (!a?.[field]) fail(label + ": missing " + field);
    if (!/^https:\/\//.test(a?.upstreamUrl ?? "")) fail(label + ": upstreamUrl must be https");
    if (!DATE.test(a?.retrievedDate ?? "")) fail(label + ": retrievedDate must be a date");
    if (!HEX64.test(a?.sha256 ?? "")) fail(label + ": sha256 must be 64 lowercase hex characters");
    if (!Number.isInteger(a?.bytes) || a.bytes <= 0) fail(label + ": bytes must be a positive integer");
    if (a?.redistribution === "published") {
      if (!a.localPath) { fail(label + ": a published artifact needs localPath"); continue; }
      if (a.declaredLicense !== "Apache-2.0") fail(label + ": only a confirmed Apache-2.0 declaration may be published");
      const file = ROOT_DIR + "/" + a.localPath;
      published.add(a.localPath.split("/").pop());
      const bytes = io.read(file);
      if (!bytes) { fail(label + ": snapshot file is missing"); continue; }
      if (bytes.length !== a.bytes) fail(label + ": size differs from the pin");
      if (sha256(bytes) !== a.sha256) fail(label + ": SHA-256 differs from the pin");
    } else if (a?.redistribution === "withheld") {
      if (a.localPath !== null) fail(label + ": a withheld artifact must not have localPath");
      if (!a.withheldReason) fail(label + ": a withheld artifact needs withheldReason");
      if (!a.externalName) fail(label + ": a withheld artifact needs externalName");
      if (a.externalName && io.read(ROOT_DIR + "/snapshots/" + a.externalName))
        fail(label + ": a withheld artifact is present in the repository");
    } else {
      fail(label + ": redistribution must be published or withheld");
    }
  }
  const present = io.list(ROOT_DIR + "/snapshots");
  for (const name of present)
    if (!published.has(name)) fail("unexpected file in snapshots: " + name);
  for (const name of published)
    if (!present.includes(name)) fail("published snapshot not listed on disk: " + name);
  if (!io.read(ROOT_DIR + "/LICENSE-APACHE-2.0.txt")) fail("the Apache 2.0 license text is missing");
  for (const f of manifest?.profileFixtures ?? []) {
    const bytes = io.read(f.path);
    if (!bytes) fail("profile fixture missing: " + f.path);
    else if (sha256(bytes) !== f.sha256) fail("profile fixture hash differs: " + f.path);
  }
  const snapshot = artifacts?.find(a => a.redistribution === "published" && a.localPath);
  if (snapshot) {
    const bytes = io.read(ROOT_DIR + "/" + snapshot.localPath);
    if (bytes) {
      try {
        const info = JSON.parse(bytes.toString("utf8")).info ?? {};
        if (info.version !== manifest.reference.version) fail("snapshot version differs from the reference version");
        if (info.license?.name !== "Apache 2.0") fail("snapshot no longer declares Apache 2.0");
      } catch { fail("snapshot is not valid JSON"); }
    }
  }
  return errors;
}

// Checks the withheld artifacts and the upstream enumerations against the supported subset.
export function verifyExternal(manifest, readExternal) {
  const errors = [];
  const docs = new Map();
  for (const a of manifest.artifacts) {
    const name = a.redistribution === "withheld" ? a.externalName : a.localPath?.split("/").pop();
    const bytes = readExternal(name);
    if (!bytes) { errors.push(a.id + ": external copy is missing (" + name + ")"); continue; }
    if (bytes.length !== a.bytes) errors.push(a.id + ": external size differs from the pin");
    if (sha256(bytes) !== a.sha256) errors.push(a.id + ": external SHA-256 differs from the pin");
    else docs.set(a.id, JSON.parse(bytes.toString("utf8")));
  }
  const events = docs.get("EVENT_DOMAIN-1.0.4")?.components?.schemas;
  if (!events) { errors.push("EVENT_DOMAIN-1.0.4 enumerations are unavailable"); return errors; }
  const enums = manifest.profile.upstreamEnums;
  const expect = (upstream, values, label, source) => {
    if (!Array.isArray(upstream)) { errors.push(label + ": upstream enumeration " + source + " is missing"); return; }
    for (const value of values)
      if (!upstream.includes(value)) errors.push(label + ": " + value + " is not in upstream " + source);
  };
  // The classifier values are declared inline on each event family's base schema.
  const classifierEnum = schemaName => (events[schemaName]?.allOf ?? [])
    .map(part => part?.properties?.eventClassifierCode?.enum).find(Array.isArray);
  for (const family of ["Equipment", "Transport"]) {
    const source = enums[family];
    expect(events[source.codes]?.enum, manifest.profile.supportedCodes[family], family + " codes", source.codes);
    expect(classifierEnum(source.classifier), manifest.profile.classifiers, family + " classifier", source.classifier);
  }
  return errors;
}

// Drift check: reports differences and never updates anything.
export async function checkDrift(manifest, fetchBytes) {
  const results = [];
  for (const a of manifest.artifacts) {
    try {
      const bytes = await fetchBytes(a.upstreamUrl);
      results.push({ id: a.id, status: sha256(bytes) === a.sha256 ? "same" : "changed", observed: sha256(bytes) });
    } catch (error) {
      results.push({ id: a.id, status: "unreachable", error: String(error.message ?? error) });
    }
  }
  return results;
}

function repoIo(root) {
  return {
    read: rel => { const file = join(root, rel); return existsSync(file) ? readFileSync(file) : null; },
    list: rel => { const dir = join(root, rel); return existsSync(dir) ? readdirSync(dir) : []; }
  };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
  const args = process.argv.slice(2);
  const manifest = JSON.parse(readFileSync(join(root, ROOT_DIR, "MANIFEST.json"), "utf8"));
  let failed = false;
  const report = (title, errors) => {
    if (errors.length) { failed = true; console.error(title + ": FAIL"); for (const e of errors) console.error("  " + e); }
    else console.log(title + ": PASS");
  };
  report("offline snapshot and fixture verification", verifyManifest(manifest, repoIo(root)));
  const externalIndex = args.indexOf("--external-dir");
  if (externalIndex >= 0) {
    const dir = args[externalIndex + 1];
    if (!dir || !existsSync(dir)) { console.error("--external-dir needs an existing directory"); process.exit(1); }
    const io = repoIo(dir);
    report("external artifacts and supported-subset check", verifyExternal(manifest, name => io.read(name)));
  }
  if (failed) process.exit(1);
  if (args.includes("--upstream")) {
    const results = await checkDrift(manifest, async url => {
      const response = await fetch(url, { signal: AbortSignal.timeout(30_000) });
      if (!response.ok) throw new Error("HTTP " + response.status);
      return Buffer.from(await response.arrayBuffer());
    });
    for (const r of results) console.log("upstream " + r.id + ": " + r.status + (r.error ? " (" + r.error + ")" : ""));
    if (results.some(r => r.status === "changed")) { console.error("Upstream drift detected; pins were not changed."); process.exit(3); }
    if (results.some(r => r.status === "unreachable")) { console.error("Upstream could not be reached; drift is unknown."); process.exit(4); }
    console.log("upstream drift check: PASS (hosted versions match the pins)");
  }
}
