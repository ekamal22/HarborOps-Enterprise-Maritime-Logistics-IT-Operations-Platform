// The ownership-checked cleanup (scripts/g17-docker-ownership.ps1, scripts/cleanup-g17-owned.ps1) is executed against
// REAL Docker with throwaway resources that this file creates under unique project names. Decoys that look similar
// (other project, same prefix, wrong name, unlabelled, foreign endpoint) must always survive, and every decoy and
// every volume is removed by exact name when the test ends. No prune command is ever used.
import test, {after} from "node:test";
import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import {spawnSync} from "node:child_process";
import {fileURLToPath} from "node:url";
import {cleanEnvironment, shellFor} from "./g17-diagnostic-test-support.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const ownership = path.join(here, "g17-docker-ownership.ps1");
const cleanup = path.join(here, "cleanup-g17-owned.ps1");
const runnerSource = fs.readFileSync(path.join(here, "run-g17-local.ps1"), "utf8");
const IMAGE = "alpine/socat:1.8.0.3";
const nonce = crypto.randomBytes(4).toString("hex");
const created = {containers: new Set(), networks: new Set(), volumes: new Set()};
const tempDirs = [];

const docker = (...args) => spawnSync("docker", args, {encoding: "utf8", timeout: 60000});
const dockerOk = (...args) => { const run = docker(...args); assert.equal(run.status, 0, `docker ${args.join(" ")}: ${run.stderr}`); return run.stdout.trim(); };
const exists = (kind, name) => {
  const probe = kind === "container" ? ["container", "inspect", name] : kind === "network" ? ["network", "inspect", name] : ["volume", "inspect", name];
  return docker(...probe).status === 0;
};

let subnetCounter = 0;
function freeSubnet() {
  // A range in 10.213.0.0/16 chosen per test and checked against every existing network.
  const used = new Set(dockerOk("network", "ls", "-q", "--no-trunc").split(/\s+/).filter(Boolean).flatMap(id =>
    JSON.parse(dockerOk("network", "inspect", id))[0].IPAM.Config?.map(c => c.Subnet) ?? []));
  for (let attempt = 0; attempt < 200; attempt++) {
    const subnet = `10.213.${(parseInt(nonce.slice(0, 2), 16) + ++subnetCounter * 7) % 250}.0/24`;
    if (!used.has(subnet)) return subnet;
  }
  throw new Error("no free test subnet");
}
const project = tag => `ho-verify-m17-selftest-${nonce}-${tag}`;

function network(name, labels, subnet) {
  const args = ["network", "create", ...(subnet ? ["--subnet", subnet] : []), ...Object.entries(labels).flatMap(([k, v]) => ["--label", `${k}=${v}`]), name];
  dockerOk(...args);
  created.networks.add(name);
}
function container(name, labels, {net, running = false} = {}) {
  const args = [running ? "run" : "create", ...(running ? ["-d"] : []), "--name", name,
    ...Object.entries(labels).flatMap(([k, v]) => ["--label", `${k}=${v}`]), ...(net ? ["--network", net] : []), "--entrypoint", "sleep", IMAGE, "600"];
  dockerOk(...args);
  created.containers.add(name);
}
const composeLabels = (proj, service, extra = {}) => ({"com.docker.compose.project": proj, "com.docker.compose.service": service, ...extra});

function runCleanup(proj, evidence, extra = []) {
  const exe = shellFor();
  const args = [...(exe === "powershell.exe" ? ["-NoProfile", "-ExecutionPolicy", "Bypass"] : ["-NoProfile"]), "-File", cleanup,
    "-ProjectName", proj, "-EvidenceDirectory", evidence, ...extra];
  return spawnSync(exe, args, {encoding: "utf8", env: cleanEnvironment(), timeout: 120000});
}
function ps(command) {
  const exe = shellFor();
  const args = [...(exe === "powershell.exe" ? ["-NoProfile", "-ExecutionPolicy", "Bypass"] : ["-NoProfile"]), "-Command", `. '${ownership}'; ${command}`];
  return spawnSync(exe, args, {encoding: "utf8", env: cleanEnvironment(), timeout: 60000});
}
function freshEvidence() {
  const dir = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "g17-own-")), "evidence");
  tempDirs.push(path.dirname(dir));
  return dir;
}
const record = dir => JSON.parse(fs.readFileSync(path.join(dir, "m17-owned-cleanup.json"), "utf8").replace(/^\uFEFF/, ""));
// PowerShell wraps an error message to the console width and prefixes continuation lines with a margin that depends on the
// host, so matches are made against the message with each such break folded to a single space.
const text = run => `${run.stdout}\n${run.stderr}`.replace(/\r?\n[ \t]*(?:\|[ \t]*)?/g, " ");

after(() => {
  for (const name of created.containers) docker("rm", "-f", name);
  for (const name of created.networks) docker("network", "rm", name);
  for (const name of created.volumes) docker("volume", "rm", name);
  for (const dir of tempDirs) fs.rmSync(dir, {recursive: true, force: true});
});

// A realistic owned stack: two stopped services, a one-off run container, one network, one labelled volume.
function ownedStack(proj, {running = false} = {}) {
  const subnet = freeSubnet();
  const net = `${proj}_internal`;
  network(net, {"com.docker.compose.project": proj, "com.docker.compose.network": "internal"}, subnet);
  container(`${proj}-postgres-1`, composeLabels(proj, "postgres"), {net, running});
  container(`${proj}-api-1`, composeLabels(proj, "api", {"com.docker.compose.oneoff": "False"}), {net, running});
  container(`${proj}-api-run-0123abcd`, composeLabels(proj, "api", {"com.docker.compose.oneoff": "True"}), {net, running});
  const volume = `${proj}_state`;
  dockerOk("volume", "create", "--label", `com.docker.compose.project=${proj}`, volume);
  created.volumes.add(volume);
  return {net, subnet, volume};
}
function decoys(proj) {
  const other = project("decoy");
  const unlabelled = `${proj}-api-1x`;
  network(`${other}_internal`, {"com.docker.compose.project": other, "com.docker.compose.network": "internal"}, freeSubnet());
  container(`${other}-api-1`, composeLabels(other, "api"), {net: `${other}_internal`});
  container(`${proj}-unlabelled-1`, {});
  // A project whose name merely starts with the owned project's name.
  const longer = `${proj}-b`;
  network(`${longer}_internal`, {"com.docker.compose.project": longer, "com.docker.compose.network": "internal"}, freeSubnet());
  container(`${longer}-api-1`, composeLabels(longer, "api"), {net: `${longer}_internal`});
  return [`${other}-api-1`, `${proj}-unlabelled-1`, `${longer}-api-1`, `${other}_internal`, `${longer}_internal`, unlabelled].filter(n => exists(n.endsWith("_internal") ? "network" : "container", n));
}
const survivors = names => names.every(n => exists(n.endsWith("_internal") ? "network" : "container", n));

test("a finished stack is removed completely, state is captured first, volumes and decoys are untouched", () => {
  const proj = project("done");
  const stack = ownedStack(proj);
  const kept = decoys(proj);
  const evidence = freshEvidence();
  const run = runCleanup(proj, evidence);
  assert.equal(run.status, 0, text(run));
  const result = record(evidence);
  assert.equal(result.outcome, "REMOVED");
  assert.equal(result.containers_before, 3);
  assert.equal(result.containers_removed, 3);
  assert.equal(result.networks_removed, 1);
  assert.equal(result.containers_after, 0);
  assert.equal(result.networks_after, 0);
  assert.deepEqual(result.volumes_retained, [stack.volume]);
  assert.deepEqual(result.subnets_released, [stack.subnet]);
  assert.deepEqual(result.subnet_still_held, []);
  for (const name of [`${proj}-postgres-1`, `${proj}-api-1`, `${proj}-api-run-0123abcd`]) assert.equal(exists("container", name), false, name);
  assert.equal(exists("network", stack.net), false);
  assert.equal(exists("volume", stack.volume), true, "retained database volumes are never removed");
  assert.ok(survivors(kept), "decoys of other projects, an unlabelled container and a longer project name all survive");
  const inventory = JSON.parse(fs.readFileSync(path.join(evidence, "m17-cleanup-state", "owned-inventory.json"), "utf8").replace(/^\uFEFF/, ""));
  assert.equal(inventory.containers.length, 3);
  assert.equal(inventory.networks[0].subnets[0], stack.subnet);
  assert.ok(fs.readdirSync(path.join(evidence, "m17-cleanup-state")).filter(n => n.endsWith(".state.json")).length === 3, "one state record per container");
});

test("a running stack from a failed or interrupted campaign needs -StopRunning, and without it nothing is touched", () => {
  const proj = project("run");
  const stack = ownedStack(proj, {running: true});
  const refused = freshEvidence();
  const first = runCleanup(proj, refused);
  assert.notEqual(first.status, 0);
  assert.match(text(first), /still running and -StopRunning was not given/);
  assert.equal(record(refused).outcome, "FAILED");
  assert.equal(record(refused).containers_removed, 0);
  assert.equal(exists("container", `${proj}-postgres-1`), true);
  assert.equal(docker("inspect", "--format", "{{.State.Running}}", `${proj}-postgres-1`).stdout.trim(), "true", "the refusal did not stop anything");

  const evidence = freshEvidence();
  const second = runCleanup(proj, evidence, ["-StopRunning"]);
  assert.equal(second.status, 0, text(second));
  assert.equal(record(evidence).containers_removed, 3);
  assert.equal(exists("network", stack.net), false);
  assert.equal(exists("volume", stack.volume), true);
});

test("partially initialised stacks are removed: only a network, only containers, and nothing at all", () => {
  const onlyNetwork = project("pn");
  const subnet = freeSubnet();
  network(`${onlyNetwork}_internal`, {"com.docker.compose.project": onlyNetwork, "com.docker.compose.network": "internal"}, subnet);
  const evidenceA = freshEvidence();
  assert.equal(runCleanup(onlyNetwork, evidenceA).status, 0);
  assert.equal(record(evidenceA).networks_removed, 1);
  assert.equal(exists("network", `${onlyNetwork}_internal`), false);

  const onlyContainers = project("pc");
  container(`${onlyContainers}-api-1`, composeLabels(onlyContainers, "api"));
  const evidenceB = freshEvidence();
  assert.equal(runCleanup(onlyContainers, evidenceB).status, 0);
  assert.equal(record(evidenceB).containers_removed, 1);
  assert.equal(exists("container", `${onlyContainers}-api-1`), false);

  const nothing = project("pz");
  const evidenceC = freshEvidence();
  assert.equal(runCleanup(nothing, evidenceC).status, 0);
  assert.equal(record(evidenceC).containers_removed + record(evidenceC).networks_removed, 0);
});

test("ambiguous ownership removes nothing at all, not even the resources that are clearly owned", () => {
  const cases = [
    ["a container with the project label but another name", proj => container(`${proj}-oddly-named`, composeLabels(proj, "api")), /is not <project>-<service>-<index>/],
    ["a container whose service label differs from its name", proj => container(`${proj}-api-9`, composeLabels(proj, "web")), /is not <project>-<service>-<index>/],
    ["a container with no service label", proj => container(`${proj}-api-8`, {"com.docker.compose.project": proj}), /is not <project>-<service>-<index>/],
    ["a network with the project label but another name", proj => network(`${proj}_x`.replace("_x", "-extra"), {"com.docker.compose.project": proj, "com.docker.compose.network": "extra"}, freeSubnet()), /is not .*_<network> of this project/],
    ["a network whose logical name does not match its name", proj => network(`${proj}_other`, {"com.docker.compose.project": proj, "com.docker.compose.network": "internal"}, freeSubnet()), /is not .*_<network> of this project/]
  ];
  for (const [label, addDoubt, message] of cases) {
    const proj = project("amb" + crypto.randomBytes(2).toString("hex"));
    ownedStack(proj);
    addDoubt(proj);
    const evidence = freshEvidence();
    const run = runCleanup(proj, evidence);
    assert.notEqual(run.status, 0, label);
    assert.match(text(run), /Ambiguous ownership, nothing was removed/, label);
    assert.match(text(run), message, label);
    assert.equal(record(evidence).outcome, "FAILED", label);
    assert.equal(record(evidence).containers_removed + record(evidence).networks_removed, 0, label);
    assert.equal(exists("container", `${proj}-postgres-1`), true, `${label}: the clearly owned container must survive`);
    assert.equal(exists("network", `${proj}_internal`), true, `${label}: the clearly owned network must survive`);
    assert.ok(fs.existsSync(path.join(evidence, "m17-cleanup-state", "owned-inventory.json")), `${label}: state was preserved before the refusal`);
  }
});

test("a network that has another project's container attached is never removed", () => {
  const proj = project("fe");
  const other = project("fo");
  const stack = ownedStack(proj);
  container(`${other}-api-1`, composeLabels(other, "api"), {net: stack.net, running: true});
  const evidence = freshEvidence();
  const run = runCleanup(proj, evidence);
  assert.notEqual(run.status, 0);
  assert.match(text(run), /has a foreign endpoint/);
  assert.equal(exists("network", stack.net), true);
  assert.equal(exists("container", `${other}-api-1`), true);
  assert.equal(exists("container", `${proj}-postgres-1`), true);
});

test("project names that are not exactly an owned ho-verify-m17-* name are rejected before docker is consulted", () => {
  for (const bad of ["production", "harborops", "ho-verify-m17-", "HO-VERIFY-M17-X", "ho-verify-m17-x y", "ho-verify-m17-x;calc", "ho-verify-m17-a_b", "ho-verify-m18-x"]) {
    const evidence = freshEvidence();
    const run = runCleanup(bad, evidence);
    assert.notEqual(run.status, 0, bad);
    assert.equal(fs.existsSync(evidence), false, `${bad}: no evidence directory is even created`);
  }
});

test("an evidence directory that already exists is refused (never overwrite evidence)", () => {
  const evidence = freshEvidence();
  fs.mkdirSync(evidence, {recursive: true});
  const run = runCleanup(project("ex"), evidence);
  assert.notEqual(run.status, 0);
  // The script's own refusal, not the generic "item already exists" error that New-Item would raise on its own.
  assert.match(text(run), /Never overwrite evidence/);
  assert.equal(fs.existsSync(path.join(evidence, "m17-owned-cleanup.json")), false, "nothing is written into an existing evidence directory");
});

test("a held address range is reported with its holder, and is free again once its network is gone", () => {
  const proj = project("sn");
  const subnet = freeSubnet();
  network(`${proj}_internal`, {"com.docker.compose.project": proj, "com.docker.compose.network": "internal"}, subnet);
  const held = ps(`(Test-G17SubnetFree '${subnet}') | ConvertTo-Json -Depth 4 -Compress`);
  const parsed = JSON.parse(held.stdout.trim());
  assert.equal(parsed.Free, false);
  assert.equal(parsed.Holders[0].Name, `${proj}_internal`);
  assert.equal(parsed.Holders[0].Project, proj);
  assert.equal(runCleanup(proj, freshEvidence()).status, 0);
  assert.equal(JSON.parse(ps(`(Test-G17SubnetFree '${subnet}') | ConvertTo-Json -Depth 4 -Compress`).stdout.trim()).Free, true);
  const unrelated = JSON.parse(ps("(Test-G17SubnetFree '10.213.255.0/24') | ConvertTo-Json -Depth 4 -Compress").stdout.trim());
  assert.equal(unrelated.Free, true);
});

test("the runner preserves before it removes, removes only through the ownership checks, and never prunes", () => {
  const stripped = runnerSource.replace(/#.*$/gm, "");
  const ownershipSource = fs.readFileSync(ownership, "utf8").replace(/#.*$/gm, "");
  for (const forbidden of [/\bprune\b/, /\bdown\b[^\n]*-v\b/, /docker\s+rm\s+-f/, /\brm\s+-f\b/, /\bvolume\s+rm\b/, /\bimage\s+rm\b/, /\brmi\b/, /\bsystem\s+prune\b/])
    for (const [label, body] of [["runner", stripped], ["ownership module", ownershipSource]])
      assert.doesNotMatch(body, forbidden, `${label} contains ${forbidden}`);
  assert.doesNotMatch(ownershipSource, /@\('rm',\s*'-v'|'rm', '-v'/, "container removal never uses -v");
  const diagnostics = stripped.indexOf("m17-failure-diagnostics");
  const stop = stripped.indexOf("compose @composeArgs stop");
  const remove = stripped.indexOf("Remove-G17OwnedResources -Project");
  assert.ok(diagnostics > 0 && stop > diagnostics && remove > stop, "failure diagnostics, then stop, then ownership-checked removal");
  assert.match(stripped, /Test-G17SubnetFree/, "the subnet is checked before the stack is started");
  assert.match(stripped, /DisposableCleanupFailure/, "a cleanup failure is recorded as a verification failure");
  assert.match(runnerSource, /\$required \+= 'm17-owned-cleanup\.json'/, "the cleanup record is a required artifact");
});

// A docker front that forwards everything to the real docker except the commands it is told to fail. It is placed first
// on PATH for the cleanup script only, so the failure paths of the removal can be exercised against real resources.
function failingDocker(failing) {
  const real = spawnSync("where.exe", ["docker.exe"], {encoding: "utf8"}).stdout.split(/\r?\n/).find(line => line.toLowerCase().endsWith("docker.exe"));
  assert.ok(real, "the real docker.exe must be locatable");
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "g17-own-front-"));
  tempDirs.push(dir);
  const checks = failing.map(args => args[1]
    ? `if "%1"=="${args[0]}" if "%2"=="${args[1]}" exit /b 1` : `if "%1"=="${args[0]}" exit /b 1`).join("\r\n");
  fs.writeFileSync(path.join(dir, "docker.cmd"), `@echo off\r\n${checks}\r\n"${real.trim()}" %*\r\n`);
  return dir;
}
function runCleanupWith(proj, evidence, frontDir, extra = []) {
  const exe = shellFor();
  const args = [...(exe === "powershell.exe" ? ["-NoProfile", "-ExecutionPolicy", "Bypass"] : ["-NoProfile"]), "-File", cleanup,
    "-ProjectName", proj, "-EvidenceDirectory", evidence, ...extra];
  return spawnSync(exe, args, {encoding: "utf8", env: cleanEnvironment({PATH: frontDir + path.delimiter + process.env.PATH}), timeout: 120000});
}

test("a container that cannot be removed fails the cleanup, is recorded, and leaves the rest untouched in the record", () => {
  const proj = project("rmfail");
  const stack = ownedStack(proj);
  const evidence = freshEvidence();
  const run = runCleanupWith(proj, evidence, failingDocker([["rm"]]));
  assert.notEqual(run.status, 0, text(run));
  assert.match(text(run), /Could not remove container/);
  const result = record(evidence);
  assert.equal(result.outcome, "FAILED");
  assert.match(result.error, /Could not remove container/);
  assert.equal(result.containers_removed, 0);
  assert.equal(exists("container", `${proj}-postgres-1`), true, "nothing was removed");
  assert.equal(exists("network", stack.net), true);
});

test("a network that cannot be removed fails the cleanup", () => {
  const proj = project("nwfail");
  ownedStack(proj);
  const evidence = freshEvidence();
  const run = runCleanupWith(proj, evidence, failingDocker([["network", "rm"]]));
  assert.notEqual(run.status, 0, text(run));
  const result = record(evidence);
  assert.equal(result.outcome, "FAILED");
  assert.match(result.error, /Could not remove network/);
  assert.equal(result.containers_removed, 3, "containers were already removed when the network step failed");
});

test("a running container that cannot be stopped fails the cleanup before anything is removed", () => {
  const proj = project("stfail");
  const stack = ownedStack(proj, {running: true});
  const evidence = freshEvidence();
  const run = runCleanupWith(proj, evidence, failingDocker([["stop"]]), ["-StopRunning"]);
  assert.notEqual(run.status, 0, text(run));
  assert.match(record(evidence).error, /Could not stop/);
  assert.equal(exists("container", `${proj}-postgres-1`), true);
  assert.equal(exists("network", stack.net), true);
});

// A docker front driven by explicit rules, for the paths a healthy docker never produces. Each rule matches the leading
// arguments of one invocation and either fails it, lies about it (exit 0 without doing anything), or lets the real
// command run and then runs one more real docker command. The front is written as a small node script behind a
// docker.cmd (Windows) or docker (other systems) launcher, so one rule table serves both.
function ruleFront(rules) {
  const win = process.platform === "win32";
  let real;
  if (win) real = spawnSync("where.exe", ["docker.exe"], {encoding: "utf8"}).stdout.split(/\r?\n/).find(line => line.toLowerCase().endsWith("docker.exe"))?.trim();
  else real = (process.env.PATH ?? "").split(path.delimiter).map(d => path.join(d, "docker")).find(candidate => fs.existsSync(candidate));
  assert.ok(real, "the real docker must be locatable");
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "g17-own-rules-"));
  tempDirs.push(dir);
  const script = path.join(dir, "front.mjs");
  fs.writeFileSync(script, `import {spawnSync} from "node:child_process";
const real = ${JSON.stringify(real)};
const rules = ${JSON.stringify(rules)};
const args = process.argv.slice(2);
const run = list => spawnSync(real, list, {stdio: "inherit"}).status ?? 1;
const rule = rules.find(r => r.match.every((word, i) => args[i] === word));
if (rule?.action === "fail") process.exit(1);
if (rule?.action === "lie") process.exit(0);
const status = run(args);
if (rule?.action === "then" && status === 0) run(rule.then);
process.exit(status);
`);
  if (win) fs.writeFileSync(path.join(dir, "docker.cmd"), `@echo off\r\n"${process.execPath}" "${script}" %*\r\nexit /b %ERRORLEVEL%\r\n`);
  else { fs.writeFileSync(path.join(dir, "docker"), `#!/bin/sh\nexec "${process.execPath}" "${script}" "$@"\n`); fs.chmodSync(path.join(dir, "docker"), 0o755); }
  return dir;
}

test("containers that docker reports as removed but that remain make the cleanup fail instead of reporting success", () => {
  const proj = project("lierm");
  const subnet = freeSubnet();
  const net = `${proj}_internal`;
  network(net, {"com.docker.compose.project": proj, "com.docker.compose.network": "internal"}, subnet);
  // Not attached to the network, so the network can be removed while the containers are still there.
  container(`${proj}-postgres-1`, composeLabels(proj, "postgres"));
  container(`${proj}-api-1`, composeLabels(proj, "api"));
  const evidence = freshEvidence();
  const run = runCleanupWith(proj, evidence, ruleFront([{match: ["rm"], action: "lie"}]));
  assert.notEqual(run.status, 0, text(run));
  assert.match(text(run), /Owned resources remain after removal/);
  const result = record(evidence);
  assert.equal(result.outcome, "FAILED");
  assert.match(result.error, /Owned resources remain after removal/);
  assert.equal(result.containers_after, 2);
  assert.equal(result.networks_after, 0);
  assert.equal(exists("container", `${proj}-postgres-1`), true);
});

test("an address range taken by another network right after the removal fails the cleanup and names the holder", () => {
  const proj = project("heldafter");
  const stack = ownedStack(proj);
  const other = project("thief");
  const thief = `${other}_internal`;
  created.networks.add(thief);
  const front = ruleFront([{match: ["network", "rm"], action: "then",
    then: ["network", "create", "--subnet", stack.subnet, "--label", `com.docker.compose.project=${other}`, "--label", "com.docker.compose.network=internal", thief]}]);
  const evidence = freshEvidence();
  const run = runCleanupWith(proj, evidence, front);
  assert.notEqual(run.status, 0, text(run));
  assert.match(text(run), /An address range is still held after removal/);
  const result = record(evidence);
  assert.equal(result.outcome, "FAILED");
  assert.equal(result.containers_after, 0);
  assert.equal(result.networks_after, 0);
  assert.deepEqual(result.subnets_released, []);
  assert.equal(result.subnet_still_held.length, 1);
  assert.ok(result.subnet_still_held[0].startsWith(stack.subnet) && result.subnet_still_held[0].includes(thief), result.subnet_still_held[0]);
});

test("a volume that docker created for a container is retained when the container is removed", () => {
  const proj = project("anonvol");
  const net = `${proj}_internal`;
  network(net, {"com.docker.compose.project": proj, "com.docker.compose.network": "internal"}, freeSubnet());
  const name = `${proj}-postgres-1`;
  dockerOk("create", "--name", name, "--label", `com.docker.compose.project=${proj}`, "--label", "com.docker.compose.service=postgres",
    "--network", net, "-v", "/var/lib/state", "--entrypoint", "sleep", IMAGE, "600");
  created.containers.add(name);
  const volume = dockerOk("inspect", "--format", "{{range .Mounts}}{{.Name}}{{end}}", name);
  assert.match(volume, /^[0-9a-f]{64}$/, "the container owns one anonymous volume");
  created.volumes.add(volume);
  const evidence = freshEvidence();
  const run = runCleanup(proj, evidence);
  assert.equal(run.status, 0, text(run));
  assert.equal(exists("container", name), false);
  assert.equal(exists("volume", volume), true, "removing the container never removes its volume, anonymous or named");
});
