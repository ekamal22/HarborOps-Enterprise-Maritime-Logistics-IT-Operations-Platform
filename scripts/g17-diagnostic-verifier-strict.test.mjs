// Strictness of the independent diagnostic-directory verifier for NEW evidence. Every case pins its exact reason, so a
// rejection caused by anything else (a missing file, a changed hash) cannot be mistaken for the condition under test.
import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import {spawnSync} from "node:child_process";
import {fileURLToPath} from "node:url";
import {verifyDiagnosticDirectory, parseHashList, parseUtcTicks, sha256, HASH_LIST, STATE_FILE} from "./g17-diagnostic-hash-list.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const verifierPath = path.join(here, "g17-diagnostic-hash-list.mjs");
const base = {started_utc: "2026-11-16T10:00:00.0000000Z", stopped_utc: "2026-11-16T11:00:00.0000000Z"};
const v2 = {
  schema_version: 2, project: "ho-verify-m17-fixture-p09", sidecar: "ho-verify-m17-fixture-p09-p09-diag",
  stop_outcomes: {typeperf: "terminated", events: "terminated", logs: "already-exited", sidecar: "removed"}
};

function listText(dir, names) {
  return names.map(name => {
    const bytes = fs.readFileSync(path.join(dir, name));
    return `${sha256(bytes)}  ${bytes.length}  ${name}`;
  }).join("\r\n") + "\r\n";
}

// A directory the verifier accepts, built directly (no producer involved).
function directory(state = {...base}) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "g17-diag-strict-"));
  fs.writeFileSync(path.join(dir, STATE_FILE), typeof state === "string" ? state : "\uFEFF" + JSON.stringify(state, null, 4));
  fs.writeFileSync(path.join(dir, "host-counters.csv"), "a,b\n1,2\n");
  fs.writeFileSync(path.join(dir, HASH_LIST), listText(dir, [STATE_FILE, "host-counters.csv"]));
  return dir;
}
const cleanup = dir => fs.rmSync(dir, {recursive: true, force: true});
const reasons = (dir, options) => verifyDiagnosticDirectory(dir, options).errors;
function withDirectory(state, fn) {
  const dir = directory(state);
  try { return fn(dir); } finally { cleanup(dir); }
}

test("a missing started_utc is rejected on its own", () => {
  const {started_utc, ...withoutStart} = base;
  withDirectory(withoutStart, dir => assert.deepEqual(reasons(dir), [`${STATE_FILE} has no valid started_utc`]));
});

test("every malformed started_utc is rejected on its own", () => {
  const malformed = ["", "yesterday", 20261116, null, true, ["2026-11-16T10:00:00Z"], "2026-11-16", "2026", "2026-11-16T10:00:00",
    "2026-11-16T10:00:00+02:00", "2026-11-16T10:00:00.12345678Z", "2026-13-16T10:00:00Z", "2026-02-30T10:00:00Z",
    "2026-11-16 10:00:00Z", "2026-11-16T25:00:00Z", "2026-11-16T10:60:00Z", " 2026-11-16T10:00:00Z", "2026-11-16T10:00:00Z "];
  for (const value of malformed)
    withDirectory({...base, started_utc: value}, dir =>
      assert.deepEqual(reasons(dir), [`${STATE_FILE} has no valid started_utc`], JSON.stringify(value)));
});

test("a missing or malformed stopped_utc is reported separately from started_utc", () => {
  withDirectory({started_utc: "bad", stopped_utc: "worse"}, dir =>
    assert.deepEqual(reasons(dir).sort(), [`${STATE_FILE} has no valid started_utc`, `${STATE_FILE} has no valid stopped_utc`].sort()));
  withDirectory({...base, stopped_utc: "2026"}, dir =>
    assert.deepEqual(reasons(dir), [`${STATE_FILE} has no valid stopped_utc`], "a bare year is not an instant, although Date.parse accepts it"));
});

test("ordering is exact to the 100 nanosecond tick", () => {
  const cases = [
    ["equal instants", "2026-11-16T10:00:00.5000000Z", "2026-11-16T10:00:00.5000000Z", []],
    ["one tick later", "2026-11-16T10:00:00.5000000Z", "2026-11-16T10:00:00.5000001Z", []],
    ["one tick earlier", "2026-11-16T10:00:00.5000000Z", "2026-11-16T10:00:00.4999999Z", [`${STATE_FILE} stopped_utc precedes started_utc`]],
    ["a shorter fraction that is earlier", "2026-11-16T10:00:00.0000001Z", "2026-11-16T10:00:00Z", [`${STATE_FILE} stopped_utc precedes started_utc`]],
    ["a shorter fraction that is equal", "2026-11-16T10:00:00.5Z", "2026-11-16T10:00:00.5000000Z", []],
    ["a day earlier", "2026-11-16T10:00:00Z", "2026-11-15T10:00:00Z", [`${STATE_FILE} stopped_utc precedes started_utc`]]
  ];
  for (const [label, started, stopped, expected] of cases)
    withDirectory({started_utc: started, stopped_utc: stopped}, dir => assert.deepEqual(reasons(dir), expected, label));
});

test("parseUtcTicks accepts exactly what .NET writes and nothing looser", () => {
  assert.equal(parseUtcTicks("1970-01-01T00:00:00Z"), 0n);
  assert.equal(parseUtcTicks("1970-01-01T00:00:00.0000001Z"), 1n);
  assert.equal(parseUtcTicks("1970-01-01T00:00:01.5Z"), 15000000n);
  assert.equal(parseUtcTicks("2024-02-29T00:00:00Z") > 0n, true, "a real leap day");
  for (const bad of ["2023-02-29T00:00:00Z", "2026-11-16T10:00:00.Z", "2026-11-16T10:00:00,5Z", 5, undefined, {}])
    assert.equal(parseUtcTicks(bad), null, String(bad));
});

test("a symbolic link standing in for a listed file is rejected, not followed", () => {
  const dir = directory();
  const outside = fs.mkdtempSync(path.join(os.tmpdir(), "g17-diag-outside-"));
  try {
    const target = path.join(outside, "host-counters.csv");
    fs.copyFileSync(path.join(dir, "host-counters.csv"), target);
    fs.rmSync(path.join(dir, "host-counters.csv"));
    fs.symlinkSync(target, path.join(dir, "host-counters.csv"), "file");
    assert.deepEqual(reasons(dir), ["host-counters.csv is a symbolic link"],
      "identical bytes reached through a link must still be refused");
  } finally { cleanup(dir); cleanup(outside); }
});

test("an unlisted symbolic link, a directory link and a plain subdirectory are each rejected", () => {
  const dir = directory();
  const outside = fs.mkdtempSync(path.join(os.tmpdir(), "g17-diag-outside-"));
  try {
    fs.symlinkSync(path.join(outside), path.join(dir, "linked-directory"), "junction");
    assert.deepEqual(reasons(dir), ["linked-directory is a symbolic link"]);
    fs.rmSync(path.join(dir, "linked-directory"), {recursive: true, force: true});
    fs.mkdirSync(path.join(dir, "nested"));
    assert.deepEqual(reasons(dir), ["nested is not a regular file and is not covered by the hash list"]);
  } finally { cleanup(dir); cleanup(outside); }
});

test("a hash list that is itself a directory or a link is refused outright", () => {
  const dir = directory();
  try {
    fs.rmSync(path.join(dir, HASH_LIST));
    fs.mkdirSync(path.join(dir, HASH_LIST));
    assert.deepEqual(reasons(dir), [`${HASH_LIST} is not a regular file`]);
  } finally { cleanup(dir); }
});

test("entries that differ only by case, or that name a stream, a drive or a wildcard, are rejected", () => {
  const dir = directory();
  try {
    const original = fs.readFileSync(path.join(dir, HASH_LIST), "utf8");
    const line = original.split(/\r\n/)[1];
    const mutate = (text, expectedFragment, label) => {
      fs.writeFileSync(path.join(dir, HASH_LIST), text);
      const found = reasons(dir);
      assert.ok(found.some(error => error.includes(expectedFragment)), `${label}: ${JSON.stringify(found)}`);
    };
    mutate(original + line.replace("host-counters.csv", "HOST-COUNTERS.CSV") + "\r\n", "entries differ only by case", "case fold");
    for (const bad of ["diag-state.json:evil", "C:evil", "star*", "q?", "a<b", "a>b", "a|b", "a\"b", "tab\tname", "ctl\u0001name"])
      mutate(original + line.replace("host-counters.csv", bad) + "\r\n", "is not '<sha256>  <size>  <name>'", bad);
  } finally { cleanup(dir); }
});

test("an impossible size is rejected without being compared", () => {
  assert.deepEqual(parseHashList(`${"a".repeat(64)}  99999999999999999999  x`).errors, ["line 1 has an impossible size"]);
});

test("schema version 2 states must confirm every child exit, the sidecar removal and their own ownership", () => {
  const full = {...base, ...v2};
  withDirectory(full, dir => assert.deepEqual(reasons(dir), [], "the complete v2 record verifies"));
  const cases = [
    ["no outcomes", s => { delete s.stop_outcomes; }, [`${STATE_FILE} records no stop outcomes`]],
    ["outcomes not an object", s => { s.stop_outcomes = "ok"; }, [`${STATE_FILE} records no stop outcomes`]],
    ["a child that was not confirmed", s => { s.stop_outcomes.events = "running"; }, [`${STATE_FILE} does not confirm that events exited`]],
    ["a missing child outcome", s => { delete s.stop_outcomes.typeperf; }, [`${STATE_FILE} does not confirm that typeperf exited`]],
    ["a sidecar that was not removed", s => { s.stop_outcomes.sidecar = "left-running"; }, [`${STATE_FILE} does not confirm the sidecar removal`]],
    ["a foreign project", s => { s.project = "someone-else"; }, [`${STATE_FILE} names no owned project`]],
    ["a sidecar of another project", s => { s.sidecar = "ho-verify-m17-other-p09-diag"; }, [`${STATE_FILE} sidecar does not belong to its project`]]
  ];
  for (const [label, edit, expected] of cases) {
    const state = structuredClone(full);
    edit(state);
    withDirectory(state, dir => assert.deepEqual(reasons(dir), expected, label));
  }
});

test("historical characterization is explicit, labelled, and can never report a trustworthy directory", () => {
  const dir = directory();
  try {
    assert.equal(verifyDiagnosticDirectory(dir).mode, "strict");
    assert.equal(verifyDiagnosticDirectory(dir).ok, true);
    const historical = verifyDiagnosticDirectory(dir, {onlyListed: true});
    assert.equal(historical.mode, "historical-characterization");
    assert.deepEqual(historical.errors, []);
    assert.equal(historical.ok, false, "a characterization of a clean directory still must not vouch for it");
    fs.writeFileSync(path.join(dir, "unlisted.txt"), "x");
    assert.deepEqual(reasons(dir, {onlyListed: true}), [], "the historical mode ignores unlisted files by design");
    assert.deepEqual(reasons(dir), ["file is not covered by the hash list: unlisted.txt"], "the default does not");
  } finally { cleanup(dir); }
});

test("the command line is strict by default and exits non-zero for the historical mode", () => {
  const dir = directory();
  try {
    const run = args => spawnSync(process.execPath, [verifierPath, dir, ...args], {encoding: "utf8"});
    const strict = run([]);
    assert.equal(strict.status, 0, strict.stdout + strict.stderr);
    assert.equal(JSON.parse(strict.stdout).mode, "strict");
    fs.writeFileSync(path.join(dir, "unlisted.txt"), "x");
    assert.equal(run([]).status, 1, "an unlisted file fails the default command");
    assert.equal(run(["--only-listed"]).status, 1, "the old flag no longer weakens the check");
    const historical = run(["--historical-characterization"]);
    assert.equal(historical.status, 1, "the historical mode never reports success");
    assert.equal(JSON.parse(historical.stdout).mode, "historical-characterization");
    assert.equal(spawnSync(process.execPath, [verifierPath], {encoding: "utf8"}).status, 2);
  } finally { cleanup(dir); }
});

test("a state file that is not a JSON object is reported, never allowed to crash the verifier", () => {
  for (const content of ["null", "[]", "42", "\"text\"", "true"]) {
    withDirectory(content, dir => assert.deepEqual(reasons(dir), [`${STATE_FILE} is not a JSON object`], content));
  }
});

test("a byte order mark on the state file is tolerated, because the producer writes one", () => {
  const dir = directory();
  try {
    const bytes = fs.readFileSync(path.join(dir, STATE_FILE));
    assert.deepEqual([...bytes.subarray(0, 3)], [0xef, 0xbb, 0xbf]);
    assert.deepEqual(reasons(dir), []);
  } finally { cleanup(dir); }
});

test("names containing a colon anywhere are rejected, whatever the position", () => {
  for (const bad of ["a:b", "name:stream", "ends:", "x:y:z", ":leading", ":", "::"]) {
    assert.ok(parseHashList(`${"a".repeat(64)}  1  ${bad}`).errors.length > 0, bad);
  }
  assert.deepEqual(parseHashList(`${"a".repeat(64)}  1  ok-name.txt`).errors, []);
});

test("calendar dates that do not exist are rejected, including ones that roll over", () => {
  for (const bad of ["2026-02-29T10:00:00Z", "2026-04-31T10:00:00Z", "2026-00-10T10:00:00Z", "2026-01-00T10:00:00Z", "2026-12-32T10:00:00Z", "2026-11-16T24:00:00Z"])
    assert.equal(parseUtcTicks(bad), null, bad);
  assert.notEqual(parseUtcTicks("2028-02-29T23:59:59.9999999Z"), null);
});
