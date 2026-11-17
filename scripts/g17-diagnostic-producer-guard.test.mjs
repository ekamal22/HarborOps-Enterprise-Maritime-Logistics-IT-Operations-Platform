// Ownership, shutdown and stability guarantees of docs/m17-s25-p09-controlled-protocol-20261115/diagnostics.ps1.
// The real PowerShell producer is executed. Unrelated processes are stand-ins started by each test, and docker is a
// copy of node.exe scripted through NODE_OPTIONS, so nothing real is ever signalled, inspected or removed.
import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import {spawn} from "node:child_process";
import {verifyDiagnosticDirectory, STATE_FILE, HASH_LIST} from "./g17-diagnostic-hash-list.mjs";
import {createToolbox, alive, waitUntil, identityOf, runStop, runProducer, validInspect, producer, removeTree} from "./g17-diagnostic-test-support.mjs";

const project = "ho-verify-m17-fixture-guard";
const sidecar = `${project}-p09-diag`;
const sidecarId = "a".repeat(64);
const unusedPids = {typeperf: 2147483001, events: 2147483002, logs: 2147483003};

function fixtureDirectory() {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "g17-diag-guard-"));
  fs.writeFileSync(path.join(dir, "host-counters.csv"), "\"time\",\"cpu\"\r\n\"10:00:01\",\"3.2\"\r\n");
  fs.writeFileSync(path.join(dir, "docker-events.jsonl"), "{\"status\":\"start\"}\n");
  fs.writeFileSync(path.join(dir, "vm-pressure.log"), "T 1\n");
  return dir;
}

function writeState(dir, overrides = {}) {
  const state = {
    started_utc: "2026-11-16T10:00:00.0000000Z", project, sidecar, sidecar_id: sidecarId,
    pids: {...unusedPids}, timezone_offset_minutes: 180, ...overrides
  };
  fs.writeFileSync(path.join(dir, STATE_FILE), "\uFEFF" + JSON.stringify(state, null, 4) + "\n");
  return state;
}

async function ready(child) {
  assert.ok(await waitUntil(() => identityOf(child.pid) !== null, 10000), "the stand-in process must be visible to the process table");
  return identityOf(child.pid);
}

// A full, correct state for three live stand-in children and the sidecar.
async function liveRun(toolbox, dir) {
  const markers = {
    typeperf: toolbox.impostor("typeperf.exe", path.join(dir, "host-counters.csv")),
    events: toolbox.impostor("docker.exe", "events", "--filter", `label=com.docker.compose.project=${project}`),
    logs: toolbox.impostor("docker.exe", "logs", "-f", sidecar)
  };
  const children = {};
  for (const [kind, child] of Object.entries(markers)) {
    const identity = await ready(child);
    children[kind] = {pid: child.pid, name: identity.name, creation_utc: identity.creation_utc};
  }
  writeState(dir, {schema_version: 2, pids: {typeperf: markers.typeperf.pid, events: markers.events.pid, logs: markers.logs.pid}, children});
  return markers;
}

const sidecarMode = (overrides = {}, inspectOverrides = {}) =>
  ({ids: [sidecarId], inspect: validInspect(sidecar, sidecarId, inspectOverrides), ...overrides});
const text = run => `${run.stdout}\n${run.stderr}`;

test("an owned run stops only its own children and sidecar, waits for their exit, and verifies strictly", async () => {
  const toolbox = createToolbox();
  const dir = fixtureDirectory();
  try {
    const children = await liveRun(toolbox, dir);
    const run = runStop(dir, toolbox.env(sidecarMode()));
    assert.equal(run.status, 0, text(run));
    for (const [kind, child] of Object.entries(children))
      assert.ok(await waitUntil(() => !alive(child.pid), 4000), `${kind} child must have exited`);
    const state = JSON.parse(fs.readFileSync(path.join(dir, STATE_FILE), "utf8").replace(/^\uFEFF/, ""));
    assert.deepEqual(state.stop_outcomes, {typeperf: "terminated", events: "terminated", logs: "terminated", sidecar: "removed"});
    assert.deepEqual(verifyDiagnosticDirectory(dir).errors, []);
    assert.deepEqual(toolbox.removals(), [["rm", "-f", sidecarId]], "exactly one removal, addressed by the recorded id");
  } finally { toolbox.cleanup(); removeTree(dir); }
});

test("an unrelated process that now holds a recorded pid is never signalled", async () => {
  const cases = [
    ["a different executable", t => t.impostor("node.exe"), "not this run's"],
    ["the right executable with another command line", t => t.impostor("typeperf.exe", "some-other-file.csv"), "does not carry this run's"]
  ];
  for (const [label, create, message] of cases) {
    const toolbox = createToolbox();
    const dir = fixtureDirectory();
    try {
      const stranger = create(toolbox);
      const identity = await ready(stranger);
      writeState(dir, {schema_version: 2, pids: {...unusedPids, typeperf: stranger.pid},
        children: {typeperf: {pid: stranger.pid, name: "typeperf.exe", creation_utc: identity.creation_utc}}});
      const run = runStop(dir, toolbox.env(sidecarMode()));
      assert.notEqual(run.status, 0, label);
      assert.match(text(run), new RegExp(message), label);
      assert.ok(alive(stranger.pid), `${label}: the unrelated process must still be running`);
      assert.deepEqual(toolbox.removals(), [], `${label}: no container may be removed`);
      assert.equal(fs.existsSync(path.join(dir, HASH_LIST)), false, `${label}: nothing may be finalised`);
    } finally { toolbox.cleanup(); removeTree(dir); }
  }
});

test("a reused process id is detected by its creation time even when name and command line match", async () => {
  const toolbox = createToolbox();
  const dir = fixtureDirectory();
  try {
    const lookalike = toolbox.impostor("typeperf.exe", path.join(dir, "host-counters.csv"));
    await ready(lookalike);
    writeState(dir, {schema_version: 2, pids: {...unusedPids, typeperf: lookalike.pid},
      children: {typeperf: {pid: lookalike.pid, name: "typeperf.exe", creation_utc: "2020-01-01T00:00:00.0000000Z"}}});
    const run = runStop(dir, toolbox.env(sidecarMode()));
    assert.notEqual(run.status, 0);
    assert.match(text(run), /id has been reused/);
    assert.ok(alive(lookalike.pid));
  } finally { toolbox.cleanup(); removeTree(dir); }
});

test("a running process with no recorded identity is not signalled", async () => {
  const toolbox = createToolbox();
  const dir = fixtureDirectory();
  try {
    const stranger = toolbox.impostor("typeperf.exe", path.join(dir, "host-counters.csv"));
    await ready(stranger);
    writeState(dir, {pids: {...unusedPids, typeperf: stranger.pid}});
    const run = runStop(dir, toolbox.env(sidecarMode()));
    assert.notEqual(run.status, 0);
    assert.match(text(run), /no recorded identity/);
    assert.ok(alive(stranger.pid));
  } finally { toolbox.cleanup(); removeTree(dir); }
});

test("invalid recorded process ids stop the run before anything is touched", () => {
  for (const bad of [0, -5, 4, 3.5, "abc", "", null, "99999999999999999999", 2147483648]) {
    const toolbox = createToolbox();
    const dir = fixtureDirectory();
    try {
      writeState(dir, {pids: {...unusedPids, logs: bad}});
      const run = runStop(dir, toolbox.env(sidecarMode()));
      assert.notEqual(run.status, 0, JSON.stringify(bad));
      assert.match(text(run), /invalid pid for logs/, JSON.stringify(bad));
      assert.deepEqual(toolbox.calls(), [], `${JSON.stringify(bad)}: docker was not consulted`);
    } finally { toolbox.cleanup(); removeTree(dir); }
  }
});

test("tampered ownership fields stop the run before docker is consulted", () => {
  const cases = [
    ["a project that is not owned", {project: "production"}, /not an owned ho-verify-m17-\* project/],
    ["a project with an injected suffix", {project: "ho-verify-m17-x --all"}, /not an owned ho-verify-m17-\* project/],
    ["a sidecar of another project", {sidecar: "ho-verify-m17-other-p09-diag"}, /does not belong to its project/],
    ["a sidecar naming an unrelated container", {sidecar: "harborops-postgres-1"}, /does not belong to its project/],
    ["a malformed sidecar id", {sidecar_id: "../../etc"}, /invalid sidecar id/],
    ["a missing start time", {started_utc: "never"}, /no valid started_utc/]
  ];
  for (const [label, overrides, message] of cases) {
    const toolbox = createToolbox();
    const dir = fixtureDirectory();
    try {
      writeState(dir, overrides);
      const run = runStop(dir, toolbox.env(sidecarMode()));
      assert.notEqual(run.status, 0, label);
      assert.match(text(run), message, label);
      assert.deepEqual(toolbox.calls(), [], `${label}: docker was not consulted`);
    } finally { toolbox.cleanup(); removeTree(dir); }
  }
});

test("a container that is not exactly this run's sidecar is never removed", () => {
  const otherId = "b".repeat(64);
  const cases = [
    ["another label value", sidecarMode({}, {Config: {Labels: {owner: "someone-else"}}}), /not labelled as this diagnostics sidecar/],
    ["no labels", sidecarMode({}, {Config: {Labels: null}}), /not labelled as this diagnostics sidecar/],
    ["a different name", sidecarMode({}, {Name: "/harborops-postgres-1"}), /does not carry the sidecar name/],
    ["a compose project member", sidecarMode({}, {Config: {Labels: {owner: "p09-controlled-diagnostics", "com.docker.compose.project": "harborops"}}}), /belongs to a compose project/],
    ["a container with network access", sidecarMode({}, {HostConfig: {NetworkMode: "bridge"}}), /not the network-less sidecar/],
    ["another container id than the one this run created", sidecarMode({ids: [otherId]}, {Id: otherId}), /differs from the one this run created/],
    ["two containers matching the name", sidecarMode({ids: [sidecarId, otherId]}), /More than one container matches/],
    ["an inspect that fails", sidecarMode({inspectExit: 1}), /could not be inspected/],
    ["a listing that fails", sidecarMode({lsExit: 1}), /could not list/],
    ["a removal that fails", sidecarMode({rmExit: 1}), /could not be removed/]
  ];
  for (const [label, mode, message] of cases) {
    const toolbox = createToolbox();
    const dir = fixtureDirectory();
    try {
      writeState(dir);
      const run = runStop(dir, toolbox.env(mode));
      assert.notEqual(run.status, 0, label);
      assert.match(text(run), message, label);
      const removals = toolbox.removals();
      if (label === "a removal that fails") assert.deepEqual(removals, [["rm", "-f", sidecarId]], label);
      else assert.deepEqual(removals, [], `${label}: nothing may be removed`);
      assert.equal(fs.existsSync(path.join(dir, HASH_LIST)), false, `${label}: nothing may be finalised`);
    } finally { toolbox.cleanup(); removeTree(dir); }
  }
});

test("missing processes and an absent sidecar are a normal, verifiable stop", () => {
  const toolbox = createToolbox();
  const dir = fixtureDirectory();
  try {
    writeState(dir, {schema_version: 2});
    const run = runStop(dir, toolbox.env(sidecarMode({ids: []})));
    assert.equal(run.status, 0, text(run));
    const state = JSON.parse(fs.readFileSync(path.join(dir, STATE_FILE), "utf8").replace(/^\uFEFF/, ""));
    assert.deepEqual(state.stop_outcomes, {typeperf: "already-exited", events: "already-exited", logs: "already-exited", sidecar: "absent"});
    assert.deepEqual(verifyDiagnosticDirectory(dir).errors, []);
    assert.deepEqual(toolbox.removals(), []);
  } finally { toolbox.cleanup(); removeTree(dir); }
});

test("a repeated Stop is idempotent and signals nothing, even if the state is edited to name a live process", async () => {
  const toolbox = createToolbox();
  const dir = fixtureDirectory();
  try {
    await liveRun(toolbox, dir);
    assert.equal(runStop(dir, toolbox.env(sidecarMode())).status, 0);
    const firstState = fs.readFileSync(path.join(dir, STATE_FILE), "utf8");
    const firstList = fs.readFileSync(path.join(dir, HASH_LIST), "utf8");
    const callsAfterFirst = toolbox.calls().length;

    const decoy = toolbox.impostor("typeperf.exe", path.join(dir, "host-counters.csv"));
    const identity = await ready(decoy);
    const stopped = JSON.parse(firstState.replace(/^\uFEFF/, ""));
    stopped.pids.typeperf = decoy.pid;
    stopped.children.typeperf = {pid: decoy.pid, name: identity.name, creation_utc: identity.creation_utc};
    fs.writeFileSync(path.join(dir, STATE_FILE), "\uFEFF" + JSON.stringify(stopped, null, 4) + "\n");

    const again = runStop(dir, toolbox.env(sidecarMode()));
    assert.equal(again.status, 0, text(again));
    assert.ok(alive(decoy.pid), "a second Stop must not signal anything");
    assert.equal(toolbox.calls().length, callsAfterFirst, "a second Stop makes no docker call");
    assert.notEqual(fs.readFileSync(path.join(dir, HASH_LIST), "utf8"), firstList,
      "the hash list describes the edited state, which is the bytes now on disk");
    assert.deepEqual(verifyDiagnosticDirectory(dir).errors, []);
  } finally { toolbox.cleanup(); removeTree(dir); }
});

// Late writes ----------------------------------------------------------------------------------------------------

function foreignWriter(script, ...args) {
  return spawn(process.execPath, ["-e", script, ...args], {stdio: "ignore", windowsHide: true, env: {...process.env, NODE_OPTIONS: ""}});
}

test("negative control: a foreign process that keeps appending is detected before anything is hashed", async () => {
  const toolbox = createToolbox();
  const dir = fixtureDirectory();
  const writer = foreignWriter(
    "const fs=require('fs');setInterval(()=>fs.appendFileSync(process.argv[1],'x'),40)", path.join(dir, "host-counters.csv"));
  try {
    writeState(dir, {schema_version: 2});
    const run = runStop(dir, toolbox.env(sidecarMode({ids: []})), ["-SettleMilliseconds", "250", "-SettleTimeoutSeconds", "3"]);
    assert.notEqual(run.status, 0, text(run));
    assert.match(text(run), /still changing after 3 seconds \(late write\)/);
    assert.equal(fs.existsSync(path.join(dir, HASH_LIST)), false, "no hash list is produced for unstable output");
    assert.equal(JSON.parse(fs.readFileSync(path.join(dir, STATE_FILE), "utf8").replace(/^\uFEFF/, "")).stopped_utc, undefined,
      "the state is not finalised either");
  } finally { writer.kill("SIGKILL"); toolbox.cleanup(); removeTree(dir); }
});

test("negative control: a write that lands after the hash list is created is detected, and the verifier agrees", async () => {
  const toolbox = createToolbox();
  const dir = fixtureDirectory();
  const writer = foreignWriter(
    "const fs=require('fs');const [list,target]=process.argv.slice(1);" +
    "const t=setInterval(()=>{if(fs.existsSync(list)){fs.appendFileSync(target,'late');clearInterval(t);}},10)",
    path.join(dir, HASH_LIST), path.join(dir, "vm-pressure.log"));
  try {
    writeState(dir, {schema_version: 2});
    const run = runStop(dir, toolbox.env(sidecarMode({ids: []})));
    assert.notEqual(run.status, 0, text(run));
    assert.match(text(run), /Late write detected: vm-pressure\.log changed after it was hashed/);
    const verdict = verifyDiagnosticDirectory(dir);
    assert.equal(verdict.ok, false);
    assert.ok(verdict.errors.includes("sha256 mismatch for vm-pressure.log"), JSON.stringify(verdict.errors));
  } finally { writer.kill("SIGKILL"); toolbox.cleanup(); removeTree(dir); }
});

test("negative control: a new file that appears after hashing is detected", async () => {
  const toolbox = createToolbox();
  const dir = fixtureDirectory();
  const writer = foreignWriter(
    "const fs=require('fs');const [list,target]=process.argv.slice(1);" +
    "const t=setInterval(()=>{if(fs.existsSync(list)){fs.writeFileSync(target,'late');clearInterval(t);}},10)",
    path.join(dir, HASH_LIST), path.join(dir, "appeared-late.txt"));
  try {
    writeState(dir, {schema_version: 2});
    const run = runStop(dir, toolbox.env(sidecarMode({ids: []})));
    assert.notEqual(run.status, 0, text(run));
    assert.match(text(run), /Late write detected: appeared-late\.txt appeared after hashing/);
    assert.ok(verifyDiagnosticDirectory(dir).errors.includes("file is not covered by the hash list: appeared-late.txt"));
  } finally { writer.kill("SIGKILL"); toolbox.cleanup(); removeTree(dir); }
});

test("an output directory holding a subdirectory or a link cannot be hashed", () => {
  for (const [label, prepare, message] of [
    ["subdirectory", dir => fs.mkdirSync(path.join(dir, "nested")), /contains a subdirectory \(nested\)/],
    ["link", dir => fs.symlinkSync(path.join(dir, "host-counters.csv"), path.join(dir, "alias.csv"), "file"), /contains a link \(alias\.csv\)/]
  ]) {
    const toolbox = createToolbox();
    const dir = fixtureDirectory();
    try {
      writeState(dir, {schema_version: 2});
      prepare(dir);
      const run = runStop(dir, toolbox.env(sidecarMode({ids: []})));
      assert.notEqual(run.status, 0, label);
      assert.match(text(run), message, label);
      assert.equal(fs.existsSync(path.join(dir, HASH_LIST)), false, label);
    } finally { toolbox.cleanup(); removeTree(dir); }
  }
});

test("Start refuses to proceed over existing state, an unowned project name, or an existing sidecar", () => {
  const toolbox = createToolbox();
  const dir = fixtureDirectory();
  try {
    const noProject = runProducer(["-Action", "Start", "-OutDir", dir, "-Project", "production"], toolbox.env({}));
    assert.notEqual(noProject.status, 0);
    assert.match(text(noProject), /owned ho-verify-m17-\* project name is required/);
    const taken = runProducer(["-Action", "Start", "-OutDir", dir, "-Project", project], toolbox.env({ids: [sidecarId]}));
    assert.notEqual(taken.status, 0);
    assert.match(text(taken), /already exists; refusing to reuse or remove it/);
    assert.deepEqual(toolbox.removals(), [], "an existing container is never removed by Start");
    assert.equal(fs.existsSync(path.join(dir, STATE_FILE)), false);
    writeState(dir);
    const again = runProducer(["-Action", "Start", "-OutDir", dir, "-Project", project], toolbox.env({}));
    assert.notEqual(again.status, 0);
    assert.match(text(again), /already started for this directory/);
  } finally { toolbox.cleanup(); removeTree(dir); }
});

test("a real Start then Stop against the stand-in docker records identities and verifies strictly", async () => {
  const toolbox = createToolbox();
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "g17-diag-e2e-"));
  try {
    const mode = sidecarMode({ids: [], runId: sidecarId});
    const started = runProducer(["-Action", "Start", "-OutDir", dir, "-Project", project], toolbox.env(mode));
    assert.equal(started.status, 0, text(started));
    const state = JSON.parse(fs.readFileSync(path.join(dir, STATE_FILE), "utf8").replace(/^\uFEFF/, ""));
    assert.equal(state.schema_version, 2);
    assert.equal(state.sidecar_id, sidecarId);
    for (const [kind, name] of [["typeperf", "typeperf.exe"], ["events", "docker.exe"], ["logs", "docker.exe"]]) {
      assert.equal(state.children[kind].name, name);
      assert.equal(state.children[kind].pid, state.pids[kind]);
      assert.ok(alive(state.pids[kind]), `${kind} must be running after Start`);
    }
    const afterStart = toolbox.calls();
    assert.ok(afterStart.some(call => call[0] === "run" && call.includes("--label") && call.includes(`owner=p09-controlled-diagnostics`)));
    const stopped = runStop(dir, toolbox.env(sidecarMode({ids: [sidecarId]})));
    assert.equal(stopped.status, 0, text(stopped));
    for (const kind of ["typeperf", "events", "logs"])
      assert.ok(await waitUntil(() => !alive(state.pids[kind]), 4000), `${kind} must be gone after Stop`);
    assert.deepEqual(verifyDiagnosticDirectory(dir).errors, []);
    assert.deepEqual(toolbox.removals(), [["rm", "-f", sidecarId]]);
  } finally { toolbox.cleanup(); removeTree(dir); }
});

test("the producer source keeps its ownership guards (structure check for mutation control)", () => {
  const source = fs.readFileSync(producer, "utf8");
  for (const needle of ["Get-ChildExpectation", "id has been reused", "refusing to signal it", "Wait-Process",
    "Late write detected", "refusing to remove it", "ReparsePoint", "Get-FileHash"])
    assert.ok(source.includes(needle), needle);
});

test("a removal that reports success but leaves the container behind is a failure", () => {
  const toolbox = createToolbox();
  const dir = fixtureDirectory();
  try {
    writeState(dir);
    const run = runStop(dir, toolbox.env(sidecarMode({rmKeeps: true})));
    assert.notEqual(run.status, 0, text(run));
    assert.match(text(run), /still present after removal/);
    assert.equal(fs.existsSync(path.join(dir, HASH_LIST)), false, "nothing is finalised");
  } finally { toolbox.cleanup(); removeTree(dir); }
});

test("negative control: a writer that rewrites identical bytes (same length, new modification time) is detected", () => {
  const toolbox = createToolbox();
  const dir = fixtureDirectory();
  const writer = foreignWriter(
    "const fs=require('fs');const f=process.argv[1];const b=fs.readFileSync(f);setInterval(()=>fs.writeFileSync(f,b),40)", path.join(dir, "host-counters.csv"));
  try {
    writeState(dir, {schema_version: 2});
    const run = runStop(dir, toolbox.env(sidecarMode({ids: []})), ["-SettleMilliseconds", "250", "-SettleTimeoutSeconds", "3"]);
    assert.notEqual(run.status, 0, text(run));
    assert.match(text(run), /still changing after 3 seconds \(late write\)/);
  } finally { writer.kill("SIGKILL"); toolbox.cleanup(); removeTree(dir); }
});

// ---- Exit of the children is awaited and proven. A stop request that is only honoured a few seconds later, or a wait
// that fails for a reason other than the timeout, must never be recorded as a terminated child. PowerShell resolves a
// function before a cmdlet, so a wrapper script that defines Stop-Process or Wait-Process and then runs the real
// producer reproduces both conditions without touching any real process.
function shadowedStop(dir, shadows) {
  const folder = fs.mkdtempSync(path.join(os.tmpdir(), "g17-diag-shadow-"));
  const wrapper = path.join(folder, "stop-with-shadows.ps1");
  fs.writeFileSync(wrapper, `$ErrorActionPreference = 'Stop'\n${shadows}\n& '${producer}' -Action Stop -OutDir '${dir}' -SettleMilliseconds 200 -SettleTimeoutSeconds 6\n`);
  return {wrapper, folder};
}

test("a child that outlives the stop request by eight seconds is awaited, then recorded as terminated", async () => {
  const toolbox = createToolbox();
  const dir = fixtureDirectory();
  let folder;
  try {
    const children = await liveRun(toolbox, dir);
    // The kill is carried out by a background job eight seconds after the request, later than the 5 second settle window and earlier than the 10 second wait, so the process is still present
    // when Stop-Process returns.
    const shadow = shadowedStop(dir, "function Stop-Process { param([int]$Id, [switch]$Force) Start-Job -ScriptBlock { param($p) Start-Sleep -Seconds 8; Stop-Process -Id $p -Force } -ArgumentList $Id | Out-Null }");
    folder = shadow.folder;
    const run = runProducer([], toolbox.env(sidecarMode()), shadow.wrapper);
    assert.equal(run.status, 0, text(run));
    for (const [kind, child] of Object.entries(children))
      assert.ok(await waitUntil(() => !alive(child.pid), 4000), `${kind} child must have exited`);
    const state = JSON.parse(fs.readFileSync(path.join(dir, STATE_FILE), "utf8").replace(/^﻿/, ""));
    assert.deepEqual(state.stop_outcomes, {typeperf: "terminated", events: "terminated", logs: "terminated", sidecar: "removed"});
    assert.deepEqual(verifyDiagnosticDirectory(dir).errors, []);
  } finally { toolbox.cleanup(); removeTree(dir); if (folder) removeTree(folder); }
});

test("a child that is still present after the wait fails the stop and is never recorded as terminated", async () => {
  const toolbox = createToolbox();
  const dir = fixtureDirectory();
  let folder;
  try {
    const children = await liveRun(toolbox, dir);
    // The stop request has no effect and the wait fails with an error other than the timeout (the producer swallows it).
    const shadow = shadowedStop(dir, "function Stop-Process { param([int]$Id, [switch]$Force) }\nfunction Wait-Process { throw 'simulated failure that is not a timeout' }");
    folder = shadow.folder;
    const run = runProducer([], toolbox.env(sidecarMode()), shadow.wrapper);
    assert.notEqual(run.status, 0, text(run));
    assert.match(text(run), /is still running after the stop request/);
    for (const [kind, child] of Object.entries(children)) assert.ok(alive(child.pid), `${kind} child was never signalled`);
    assert.equal(fs.existsSync(path.join(dir, HASH_LIST)), false, "nothing may be finalised");
    assert.deepEqual(toolbox.removals(), [], "the sidecar is not touched before every child is proven gone");
    const state = JSON.parse(fs.readFileSync(path.join(dir, STATE_FILE), "utf8").replace(/^﻿/, ""));
    assert.equal(state.stop_outcomes, undefined, "no outcome is recorded for a child that is still running");
  } finally { toolbox.cleanup(); removeTree(dir); if (folder) removeTree(folder); }
});
