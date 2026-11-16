import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import {spawnSync} from "node:child_process";
import {fileURLToPath} from "node:url";
import {verifyDiagnosticDirectory, parseHashList, sha256, HASH_LIST, STATE_FILE} from "./g17-diagnostic-hash-list.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "..");
const protocol = path.join(repo, "docs", "m17-s25-p09-controlled-protocol-20261115");
const currentProducer = path.join(protocol, "diagnostics.ps1");
const legacyProducer = path.join(here, "fixtures", "g17-diagnostics-legacy-order.ps1");
const fixtureProject = "ho-verify-m17-fixture-p09";

function shellFor() {
  for (const exe of ["powershell.exe", "pwsh"]) {
    const probe = spawnSync(exe, ["-NoProfile", "-Command", "$PSVersionTable.PSVersion.Major"], {encoding: "utf8"});
    if (probe.status === 0) return exe;
  }
  assert.fail("A PowerShell host is required to execute the real diagnostic producer; none was found");
}

// A disposable directory in the exact shape that -Action Start leaves behind. The pids are unused values, the
// sidecar name is unique to this fixture, so Stop cannot touch any real process or container.
function startedFixture() {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "g17-diag-"));
  const state = {started_utc: "2026-11-16T10:00:00.0000000Z", project: fixtureProject, sidecar: `${fixtureProject}-p09-diag`,
    pids: {typeperf: 2147483001, events: 2147483002, logs: 2147483003}, timezone_offset_minutes: 180};
  fs.writeFileSync(path.join(dir, STATE_FILE), "\uFEFF" + JSON.stringify(state, null, 4) + "\n");
  fs.writeFileSync(path.join(dir, "host-counters.csv"), "\"time\",\"cpu\"\r\n\"10:00:01\",\"3.2\"\r\n");
  fs.writeFileSync(path.join(dir, "docker-events.jsonl"), "{\"status\":\"create\"}\n{\"status\":\"start\"}\n");
  fs.writeFileSync(path.join(dir, "vm-pressure.log"), "T 1\ncpu some avg10=0.00\n");
  fs.writeFileSync(path.join(dir, "typeperf.out"), "Exiting, please wait...\n");
  for (const empty of ["typeperf.err", "docker-events.err", "vm-pressure.err"]) fs.writeFileSync(path.join(dir, empty), "");
  return dir;
}

function stop(producer, dir) {
  const exe = shellFor();
  const args = exe === "powershell.exe" ? ["-NoProfile", "-ExecutionPolicy", "Bypass"] : ["-NoProfile"];
  const run = spawnSync(exe, [...args, "-File", producer, "-Action", "Stop", "-OutDir", dir], {encoding: "utf8"});
  assert.equal(run.status, 0, `producer failed: ${run.stdout}\n${run.stderr}`);
  assert.match(run.stdout, /diagnostics stopped/);
}

function listFor(dir, names) {
  return names.map(name => {
    const bytes = fs.readFileSync(path.join(dir, name));
    return `${sha256(bytes)}  ${bytes.length}  ${name}`;
  }).join("\r\n") + "\r\n";
}

function stoppedFixture() {
  const dir = startedFixture();
  stop(currentProducer, dir);
  return dir;
}

function withFixture(fn) {
  const dir = stoppedFixture();
  try { fn(dir); } finally { fs.rmSync(dir, {recursive: true, force: true}); }
}

const rewriteList = (dir, edit) => {
  const file = path.join(dir, HASH_LIST);
  fs.writeFileSync(file, edit(fs.readFileSync(file, "utf8")));
};

test("the corrected producer finalises the state before hashing it, and every listed byte matches", () => {
  withFixture(dir => {
    const result = verifyDiagnosticDirectory(dir);
    assert.deepEqual(result.errors, []);
    assert.equal(result.ok, true);
    const state = JSON.parse(fs.readFileSync(path.join(dir, STATE_FILE), "utf8").replace(/^\uFEFF/, ""));
    assert.match(state.stopped_utc, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+Z$/);
    assert.ok(Date.parse(state.stopped_utc) > Date.parse(state.started_utc));
    const entry = result.entries.find(e => e.name === STATE_FILE);
    const bytes = fs.readFileSync(path.join(dir, STATE_FILE));
    assert.equal(entry.size, bytes.length, "listed size is the size of the final state file");
    assert.equal(entry.sha256, sha256(bytes), "listed checksum is the checksum of the final state file");
    const names = result.entries.map(e => e.name).sort();
    assert.deepEqual(names, fs.readdirSync(dir).filter(n => n !== HASH_LIST).sort());
    assert.ok(!names.includes(HASH_LIST), "the hash list is never self-referential");
  });
});

test("stopping twice leaves a consistent record and never lists the hash list itself", () => {
  withFixture(dir => {
    stop(currentProducer, dir);
    const result = verifyDiagnosticDirectory(dir);
    assert.deepEqual(result.errors, []);
    assert.ok(result.entries.every(e => e.name !== HASH_LIST));
  });
});

test("negative control: the previous producer order is detected as exactly one state-file mismatch", () => {
  const dir = startedFixture();
  try {
    stop(legacyProducer, dir);
    const result = verifyDiagnosticDirectory(dir);
    assert.equal(result.ok, false);
    assert.deepEqual(result.errors.sort(), [
      `sha256 mismatch for ${STATE_FILE}`,
      `size mismatch for ${STATE_FILE}: listed ${result.entries.find(e => e.name === STATE_FILE).size}, actual ${fs.statSync(path.join(dir, STATE_FILE)).size}`
    ].sort());
  } finally { fs.rmSync(dir, {recursive: true, force: true}); }
});

test("the current producer source writes the final state before it creates the hash list", () => {
  const source = fs.readFileSync(currentProducer, "utf8");
  const stopSection = source.slice(source.indexOf("$state = Get-Content"));
  const stateWrite = stopSection.indexOf("Set-Content -LiteralPath $stateFile");
  const listWrite = stopSection.indexOf("diag-hashes.txt') -Encoding ascii");
  assert.ok(stateWrite > 0 && listWrite > 0, "both writes must be present in the Stop path");
  assert.ok(stateWrite < listWrite, "diag-state.json must be finalised before diag-hashes.txt is written");
  assert.match(stopSection, /Name -ne 'diag-hashes\.txt'/, "the hash list must exclude itself");
  assert.ok(stopSection.indexOf("stopped_utc") < stopSection.indexOf("Get-FileHash"), "stopped_utc is added before hashing");
});

test("every kind of tampering or malformed list is rejected on its own", () => {
  const cases = {
    "a raw capture changed with the same length": dir => {
      const file = path.join(dir, "docker-events.jsonl");
      const bytes = fs.readFileSync(file); bytes[3] ^= 1; fs.writeFileSync(file, bytes);
    },
    "a raw capture truncated": dir => {
      const file = path.join(dir, "host-counters.csv");
      fs.writeFileSync(file, fs.readFileSync(file).subarray(0, 10));
    },
    "a raw capture extended": dir => fs.appendFileSync(path.join(dir, "vm-pressure.log"), "extra\n"),
    "a listed capture deleted": dir => fs.rmSync(path.join(dir, "typeperf.out")),
    "an unlisted file added": dir => fs.writeFileSync(path.join(dir, "unlisted.txt"), "x"),
    "the state rewritten after hashing (the legacy defect)": dir => {
      const file = path.join(dir, STATE_FILE);
      fs.writeFileSync(file, fs.readFileSync(file, "utf8").replace(/"stopped_utc":\s*"[^"]+"/, "\"stopped_utc\":  \"2026-11-16T23:59:59.9999999Z\""));
    },
    "the list contains itself": dir => rewriteList(dir, t => t + `${"0".repeat(64)}  0  ${HASH_LIST}\r\n`),
    "a duplicate entry": dir => rewriteList(dir, t => t + t.split(/\r?\n/)[0] + "\r\n"),
    "an uppercase checksum": dir => rewriteList(dir, t => t.replace(/[0-9a-f]{64}/, m => m.toUpperCase().replace(/[0-9]/g, "A"))),
    "a short checksum": dir => rewriteList(dir, t => t.replace(/[0-9a-f]{64}/, m => m.slice(1))),
    "a single-space separator": dir => rewriteList(dir, t => t.replace(/ {2}/, " ")),
    "a path traversal name": dir => rewriteList(dir, t => t + `${"1".repeat(64)}  0  ..\\outside.txt\r\n`),
    "a negative size": dir => rewriteList(dir, t => t.replace(/ {2}\d+ {2}/, "  -1  ")),
    "the state entry removed": dir => rewriteList(dir, t => t.split(/\r?\n/).filter(l => !l.endsWith(STATE_FILE)).join("\r\n") + "\r\n"),
    "an empty list": dir => fs.writeFileSync(path.join(dir, HASH_LIST), ""),
    "no list": dir => fs.rmSync(path.join(dir, HASH_LIST))
  };
  for (const [label, tamper] of Object.entries(cases)) {
    withFixture(dir => {
      tamper(dir);
      const result = verifyDiagnosticDirectory(dir);
      assert.equal(result.ok, false, label);
      assert.ok(result.errors.length > 0, label);
    });
  }
});

test("a state that was never finalised fails even when its listed hash is internally correct", () => {
  const dir = startedFixture();
  try {
    const names = fs.readdirSync(dir).sort();
    fs.writeFileSync(path.join(dir, HASH_LIST), listFor(dir, names));
    const result = verifyDiagnosticDirectory(dir);
    assert.deepEqual(result.errors, [`${STATE_FILE} has no valid stopped_utc`]);
    assert.equal(verifyDiagnosticDirectory(dir, {requireFinalState: false}).ok, true,
      "the final-state requirement is the only rejected condition");
  } finally { fs.rmSync(dir, {recursive: true, force: true}); }
});

test("hash list parsing accepts only the producer's exact line format", () => {
  const good = `${"a".repeat(64)}  12  diag-state.json`;
  assert.deepEqual(parseHashList(good + "\r\n").errors, []);
  assert.deepEqual(parseHashList(good).entries, [{sha256: "a".repeat(64), size: 12, name: "diag-state.json"}]);
  for (const bad of [good + " ", `${"a".repeat(64)}  012  x`, `${"g".repeat(64)}  1  x`, `${"a".repeat(64)}  1  a/b`, `${"a".repeat(64)}  1  ..`, ""]) {
    assert.notDeepEqual(parseHashList(bad).errors, [], JSON.stringify(bad));
  }
});

test("historical r1c, r2 and r3 records keep exactly their one documented state-file mismatch", () => {
  // Characterisation only. The original evidence is read, never changed and never made to appear valid.
  for (const run of ["r1c", "r2", "r3"]) {
    const directory = path.join(protocol, "results", "runs", run);
    const result = verifyDiagnosticDirectory(directory, {onlyListed: true});
    const entry = result.entries.find(e => e.name === STATE_FILE);
    const actual = fs.statSync(path.join(directory, STATE_FILE)).size;
    assert.deepEqual(result.errors.sort(), [
      `sha256 mismatch for ${STATE_FILE}`,
      `size mismatch for ${STATE_FILE}: listed ${entry.size}, actual ${actual}`
    ].sort(), run);
    assert.ok(actual > entry.size, `${run}: the final state is longer than the state that was hashed (stopped_utc added afterwards)`);
  }
});

// Directly built, internally consistent directory: lets every rejection be pinned to its own reason.
const finalState = {started_utc: "2026-11-16T10:00:00.0000000Z", stopped_utc: "2026-11-16T11:00:00.0000000Z", project: fixtureProject};
function consistent(mutateState = s => s) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "g17-diag-c-"));
  const state = mutateState(structuredClone(finalState));
  fs.writeFileSync(path.join(dir, STATE_FILE), typeof state === "string" ? state : "\uFEFF" + JSON.stringify(state, null, 4));
  fs.writeFileSync(path.join(dir, "host-counters.csv"), "a,b\n1,2\n");
  fs.writeFileSync(path.join(dir, HASH_LIST), listFor(dir, ["diag-state.json", "host-counters.csv"]));
  return dir;
}
const reasons = (dir, options) => verifyDiagnosticDirectory(dir, options).errors;

test("the directly built consistent directory verifies, including a subdirectory that the producer never lists", () => {
  const dir = consistent();
  try {
    assert.deepEqual(reasons(dir), []);
    fs.mkdirSync(path.join(dir, "nested"));
    assert.deepEqual(reasons(dir), [], "Get-ChildItem -File never lists directories, so neither does the verifier");
  } finally { fs.rmSync(dir, {recursive: true, force: true}); }
});

test("each rejection names exactly its own reason", () => {
  const cases = [
    ["a self-listing entry", dir => rewriteList(dir, t => t + `${"0".repeat(64)}  0  ${HASH_LIST}\r\n`), [`${HASH_LIST} lists itself`]],
    ["a duplicate entry", dir => rewriteList(dir, t => t + t.split(/\r?\n/)[0] + "\r\n"), [`duplicate entry ${STATE_FILE}`]],
    ["a removed state entry", dir => rewriteList(dir, t => t.split(/\r?\n/).filter(l => l && !l.endsWith(STATE_FILE)).join("\r\n") + "\r\n"),
      [`file is not covered by the hash list: ${STATE_FILE}`, `${STATE_FILE} is not listed`]],
    ["a short checksum", dir => rewriteList(dir, t => t.replace(/[0-9a-f]{64}/, m => m.slice(1))),
      ["line 1 is not '<sha256>  <size>  <name>'", `file is not covered by the hash list: ${STATE_FILE}`, `${STATE_FILE} is not listed`]],
    ["a leading-zero size", dir => rewriteList(dir, t => t.replace(/ {2}(\d+) {2}/, "  0$1  ")),
      ["line 1 is not '<sha256>  <size>  <name>'", `file is not covered by the hash list: ${STATE_FILE}`, `${STATE_FILE} is not listed`]],
    ["an unlisted file", dir => fs.writeFileSync(path.join(dir, "extra.txt"), "x"), ["file is not covered by the hash list: extra.txt"]],
    ["a deleted listed file", dir => fs.rmSync(path.join(dir, "host-counters.csv")), ["listed file is missing: host-counters.csv"]],
    ["a changed capture of the same length", dir => fs.writeFileSync(path.join(dir, "host-counters.csv"), "a,b\n1,3\n"), ["sha256 mismatch for host-counters.csv"]],
    ["a truncated capture", dir => fs.writeFileSync(path.join(dir, "host-counters.csv"), "a,b\n"),
      ["size mismatch for host-counters.csv: listed 8, actual 4", "sha256 mismatch for host-counters.csv"]],
    ["a missing hash list", dir => fs.rmSync(path.join(dir, HASH_LIST)), [`${HASH_LIST} is missing`]],
    ["an empty hash list", dir => fs.writeFileSync(path.join(dir, HASH_LIST), ""),
      ["empty hash list", `file is not covered by the hash list: ${STATE_FILE}`, `file is not covered by the hash list: host-counters.csv`, `${STATE_FILE} is not listed`]]
  ];
  for (const [label, tamper, expected] of cases) {
    const dir = consistent();
    try {
      tamper(dir);
      assert.deepEqual(reasons(dir).sort(), [...expected].sort(), label);
    } finally { fs.rmSync(dir, {recursive: true, force: true}); }
  }
});

test("a state record must carry a real, ordered stopped_utc and be valid JSON", () => {
  const cases = [
    ["no stopped_utc", s => { delete s.stopped_utc; return s; }, [`${STATE_FILE} has no valid stopped_utc`]],
    ["numeric stopped_utc", s => { s.stopped_utc = 2026; return s; }, [`${STATE_FILE} has no valid stopped_utc`]],
    ["unparseable stopped_utc", s => { s.stopped_utc = "never"; return s; }, [`${STATE_FILE} has no valid stopped_utc`]],
    ["stopped before started", s => { s.stopped_utc = "2026-11-16T09:00:00.0000000Z"; return s; }, [`${STATE_FILE} stopped_utc precedes started_utc`]],
    ["not JSON", () => "{ not json", [`${STATE_FILE} is not valid JSON`]]
  ];
  for (const [label, mutate, expected] of cases) {
    const dir = consistent(mutate);
    try { assert.deepEqual(reasons(dir), expected, label); }
    finally { fs.rmSync(dir, {recursive: true, force: true}); }
  }
});

test("a listed file that is absent is tolerated only in the explicit historical mode", () => {
  const dir = consistent();
  try {
    fs.rmSync(path.join(dir, "host-counters.csv"));
    assert.deepEqual(reasons(dir, {onlyListed: true}), []);
    assert.deepEqual(reasons(dir), ["listed file is missing: host-counters.csv"]);
  } finally { fs.rmSync(dir, {recursive: true, force: true}); }
});
