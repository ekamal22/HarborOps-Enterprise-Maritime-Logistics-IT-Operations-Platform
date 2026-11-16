// Test support for the diagnostics producer (docs/m17-s25-p09-controlled-protocol-20261115/diagnostics.ps1).
// The producer talks to the Windows process table and to the Docker CLI. These helpers let the tests drive the real
// PowerShell script against a hermetic stand-in for docker: a copy of node.exe named docker.exe, found first on PATH,
// whose subcommands are tiny extensionless scripts in the working directory (Node resolves the first argument of
// "docker <subcommand> ..." as its main script). No real container, project or unrelated process is ever consulted,
// signalled or removed.
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import {spawn, spawnSync} from "node:child_process";
import {fileURLToPath} from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
export const repo = path.resolve(here, "..");
export const protocol = path.join(repo, "docs", "m17-s25-p09-controlled-protocol-20261115");
export const producer = path.join(protocol, "diagnostics.ps1");
export const SIDECAR_OWNER = "p09-controlled-diagnostics";

// The campaign runner is PowerShell 7. Its PSModulePath, inherited through Node, hides the Windows PowerShell 5.1
// utility module from a child powershell.exe (Get-FileHash then "is not recognized"). Each child host must compute
// its own default module path.
export function cleanEnvironment(extra = {}) {
  const env = {...process.env};
  for (const key of Object.keys(env)) if (key.toLowerCase() === "psmodulepath") delete env[key];
  return {...env, ...extra};
}

let cachedShell = null;
export function shellFor() {
  if (cachedShell) return cachedShell;
  for (const exe of ["powershell.exe", "pwsh"]) {
    const probe = spawnSync(exe, ["-NoProfile", "-Command", "$PSVersionTable.PSVersion.Major"], {encoding: "utf8", env: cleanEnvironment()});
    if (probe.status === 0) return (cachedShell = exe);
  }
  throw new Error("A PowerShell host is required to execute the real diagnostic producer; none was found");
}

const stubCore = `
const fs = require("node:fs");
const sub = process.argv[1];
const a = process.argv.slice(2);
const logFile = process.env.STUB_LOG;
const mode = JSON.parse(process.env.STUB_MODE || "{}");
if (logFile) fs.appendFileSync(logFile, JSON.stringify([sub, ...a]) + "\\n");
const removedMarker = logFile ? logFile + ".removed" : null;
const removed = Boolean(removedMarker && fs.existsSync(removedMarker));
const out = text => process.stdout.write(text + "\\n");
if (sub === "events" || sub === "logs") {
  setInterval(() => out('{"status":"stub"}'), 200);
} else if (sub === "run") {
  out(mode.runId || "c".repeat(64));
  process.exit(mode.runExit || 0);
} else if (sub === "container" && a[0] === "inspect") {
  if (mode.inspectExit) process.exit(mode.inspectExit);
  out(JSON.stringify(mode.inspect));
  process.exit(0);
} else if (sub === "container") {
  const ids = removed ? [] : (mode.ids || []);
  for (const id of ids) out(id);
  process.exit(mode.lsExit || 0);
} else if (sub === "rm") {
  if (mode.rmExit) process.exit(mode.rmExit);
  if (mode.rmKeeps) process.exit(0);
  if (removedMarker) fs.writeFileSync(removedMarker, "1");
  process.exit(0);
} else {
  process.exit(0);
}
`;

// The stand-in executables are copies of node.exe (about 80 MB each). Copying them for every test made the temporary
// directories slow to delete under load (a virus scanner or indexer keeps a copy open for a while, and a running
// process keeps its image locked), which failed a full parallel run with EPERM. They are copied ONCE per test process
// and removed when it exits; each test only gets a small directory of stub scripts that holds no executable.
let shared = null;
function sharedBinaries() {
  if (shared) return shared;
  const bin = fs.mkdtempSync(path.join(os.tmpdir(), "g17-diag-bin-"));
  fs.copyFileSync(process.execPath, path.join(bin, "docker.exe"));
  // typeperf.exe stand-ins live OUTSIDE the PATH directory, so a real Start still launches the real typeperf.
  fs.mkdirSync(path.join(bin, "impostors"));
  fs.copyFileSync(process.execPath, path.join(bin, "impostors", "typeperf.exe"));
  process.on("exit", () => { try { removeTree(bin); } catch { /* residue in the temp directory is harmless */ } });
  return (shared = bin);
}

// A disposable directory of stub scripts and the call log; docker.exe and typeperf.exe come from the shared binaries.
export function createToolbox() {
  const bin = sharedBinaries();
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "g17-diag-tools-"));
  fs.writeFileSync(path.join(dir, "stub-core.cjs"), stubCore);
  for (const subcommand of ["container", "rm", "run", "events", "logs"])
    fs.writeFileSync(path.join(dir, subcommand), `process.argv[1] = ${JSON.stringify(subcommand)};\nrequire("./stub-core.cjs");\n`);
  const log = path.join(dir, "docker-calls.log");
  fs.writeFileSync(log, "");
  const spawned = [];
  const toolbox = {
    dir, log,
    calls() { return fs.readFileSync(log, "utf8").split("\n").filter(Boolean).map(line => JSON.parse(line)); },
    removals() { return toolbox.calls().filter(call => call[0] === "rm"); },
    // Environment for the PowerShell host: the stub docker first on PATH, scripted by STUB_MODE. STUB_CWD names the
    // working directory the host must start in, which is how Node finds the subcommand scripts.
    env(mode = {}) {
      return cleanEnvironment({
        PATH: bin + path.delimiter + process.env.PATH,
        STUB_LOG: log, STUB_MODE: JSON.stringify(mode), STUB_CWD: dir
      });
    },
    // A long-lived stand-in process with a chosen executable name and command line. The extra arguments are what the
    // producer reads back from the process table as its command line.
    impostor(name, ...marker) {
      const exe = name === "node.exe" ? process.execPath
        : name === "typeperf.exe" ? path.join(bin, "impostors", name) : path.join(bin, name);
      const child = spawn(exe, ["-e", "setInterval(()=>{},1000)", ...marker], {stdio: "ignore", windowsHide: true, cwd: os.tmpdir()});
      spawned.push(child);
      return child;
    },
    // Windows keeps an executable locked until its process is really gone, and a kill is asynchronous. Wait for every
    // stand-in to disappear from the process table, then retry the removal instead of failing on a transient lock.
    cleanup() {
      for (const child of spawned) { try { child.kill("SIGKILL"); } catch { /* already gone */ } }
      const until = Date.now() + 10000;
      while (Date.now() < until && spawned.some(child => child.pid && alive(child.pid))) sleepSync(50);
      removeTree(dir);
    }
  };
  return toolbox;
}

export function sleepSync(milliseconds) { Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, milliseconds); }

// Removal that tolerates the short window in which Windows still reports a file as in use.
export function removeTree(target) {
  fs.rmSync(target, {recursive: true, force: true, maxRetries: 40, retryDelay: 250});
}

export function alive(pid) {
  try { process.kill(pid, 0); return true; } catch (error) { return error.code === "EPERM"; }
}

export async function waitUntil(predicate, milliseconds = 8000) {
  const until = Date.now() + milliseconds;
  while (Date.now() < until) { if (predicate()) return true; await new Promise(resolve => setTimeout(resolve, 50)); }
  return predicate();
}

// Identity as the Windows process table reports it (name, creation instant as an ISO-8601 UTC string).
export function identityOf(pid) {
  const command = `$p = Get-CimInstance Win32_Process -Filter 'ProcessId = ${Number(pid)}'; ` +
    "if ($p) { ($p.Name + '|' + ([DateTime]$p.CreationDate).ToUniversalTime().ToString('o')) }";
  const run = spawnSync(shellFor(), ["-NoProfile", "-Command", command], {encoding: "utf8", env: cleanEnvironment()});
  const [name, creationUtc] = String(run.stdout).trim().split("|");
  return name ? {name, creation_utc: creationUtc} : null;
}

export function powershellArguments(script, args) {
  const exe = shellFor();
  return [exe, [...(exe === "powershell.exe" ? ["-NoProfile", "-ExecutionPolicy", "Bypass"] : ["-NoProfile"]), "-File", script, ...args]];
}

// Output goes to files, never pipes. A Start leaves long-lived children (typeperf, docker events, docker logs) that
// inherit every inheritable handle of the producer's host, including the capture pipes of a caller that waits for
// them to close; such a caller then hangs until its own timeout. Files cannot hold a caller hostage.
export function runProducer(args, env, script = producer) {
  const [exe, argv] = powershellArguments(script, args);
  const capture = fs.mkdtempSync(path.join(os.tmpdir(), "g17-diag-capture-"));
  const out = fs.openSync(path.join(capture, "stdout.txt"), "w");
  const err = fs.openSync(path.join(capture, "stderr.txt"), "w");
  try {
    const run = spawnSync(exe, argv, {stdio: ["ignore", out, err], env, cwd: env.STUB_CWD || undefined, timeout: 90000});
    fs.closeSync(out); fs.closeSync(err);
    return {status: run.status, signal: run.signal,
      stdout: fs.readFileSync(path.join(capture, "stdout.txt"), "utf8"), stderr: fs.readFileSync(path.join(capture, "stderr.txt"), "utf8")};
  } finally { fs.rmSync(capture, {recursive: true, force: true}); }
}

export function runStop(dir, env, extra = ["-SettleMilliseconds", "200", "-SettleTimeoutSeconds", "6"], script = producer) {
  return runProducer(["-Action", "Stop", "-OutDir", dir, ...extra], env, script);
}

export function validInspect(sidecar, id, overrides = {}) {
  return {
    Id: id, Name: `/${sidecar}`,
    Config: {Labels: {owner: SIDECAR_OWNER}},
    HostConfig: {NetworkMode: "none"},
    ...overrides
  };
}
