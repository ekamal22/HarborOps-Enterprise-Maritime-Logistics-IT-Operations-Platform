import test, {after} from "node:test";
import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import {spawnSync} from "node:child_process";
import {createArchive, verifyArchive, buildInventory, archiveKind} from "./g17-evidence-archive.mjs";
import {cleanEnvironment, shellFor} from "./g17-diagnostic-test-support.mjs";

const root = fs.mkdtempSync(path.join(os.tmpdir(), "g17-archive-test-"));
after(() => fs.rmSync(root, {recursive: true, force: true}));
let counter = 0;
const fresh = name => path.join(root, `${++counter}-${name}`);
const tar = path.join(process.env.SystemRoot ?? "C:\\Windows", "System32", "tar.exe");

function evidenceDirectory() {
  const dir = fresh("evidence");
  fs.mkdirSync(path.join(dir, "browser-artifacts", "deep"), {recursive: true});
  fs.writeFileSync(path.join(dir, "source-sha.txt"), "a".repeat(40));
  fs.writeFileSync(path.join(dir, "run-status.json"), JSON.stringify({run_state: "COMPLETE"}));
  fs.writeFileSync(path.join(dir, "empty.txt"), "");
  fs.writeFileSync(path.join(dir, "name with spaces.log"), "line\r\nline\r\n");
  fs.writeFileSync(path.join(dir, "browser-artifacts", ".last-run.json"), "{}");
  fs.writeFileSync(path.join(dir, "browser-artifacts", "deep", "trace.bin"), crypto.randomBytes(70000));
  return dir;
}
function packaged() {
  const dir = evidenceDirectory();
  const archive = fresh("bundle") + ".zip";
  const made = createArchive(dir, archive);
  return {dir, archive, made, inventory: archive + ".inventory.json"};
}

test("an evidence directory is packaged as a real zip and verified by extracting it with a standard reader", () => {
  const {dir, archive, made, inventory} = packaged();
  const head = fs.readFileSync(archive).subarray(0, 4);
  assert.deepEqual([...head], [0x50, 0x4b, 0x03, 0x04], "the file begins with the zip local header signature");
  assert.equal(archiveKind(fs.readFileSync(archive)), "zip");
  assert.equal(made.file_count, 6);
  const doc = JSON.parse(fs.readFileSync(inventory, "utf8"));
  assert.equal(doc.source_sha, "a".repeat(40));
  assert.deepEqual(doc.files.map(f => f.path), buildInventory(dir).map(f => f.path));
  assert.ok(doc.files.every(f => /^[0-9a-f]{64}$/.test(f.sha256) && Number.isInteger(f.bytes) && !f.path.includes("\\")));
  const verdict = verifyArchive(archive);
  assert.deepEqual(verdict.problems, []);
  assert.equal(verdict.ok, true);
  assert.equal(verdict.archive_sha256, made.archive_sha256);
  // A second, unrelated reader (bsdtar) lists the same files, so the format is not just what one tool accepts.
  const listing = spawnSync(tar, ["-tf", archive], {encoding: "utf8"});
  assert.equal(listing.status, 0);
  const names = listing.stdout.split(/\r?\n/).map(n => n.replace(/^\.\//, "")).filter(n => n && !n.endsWith("/")).sort();
  assert.deepEqual(names, doc.files.map(f => f.path).sort());
});

test("a tar or gzip stream with a .zip name is rejected by what it is, not by its name", () => {
  const dir = evidenceDirectory();
  for (const [kind, flags] of [["tar", ["-c", "--format", "ustar"]], ["gzip", ["-c", "-z"]]]) {
    const disguised = fresh(kind) + ".zip";
    const made = spawnSync(tar, [...flags, "-f", disguised, "-C", dir, "."], {encoding: "utf8"});
    assert.equal(made.status, 0, made.stderr);
    assert.equal(archiveKind(fs.readFileSync(disguised)), kind);
    const verdict = verifyArchive(disguised, path.join(dir, "run-status.json"));
    assert.equal(verdict.ok, false);
    assert.deepEqual(verdict.problems, [`the file is a ${kind} stream, not a zip archive`]);
  }
  assert.equal(archiveKind(Buffer.from("not an archive at all")), "unknown");
});

test("packaging refuses a name without .zip, an existing archive, and a link inside the evidence", () => {
  const dir = evidenceDirectory();
  assert.throws(() => createArchive(dir, fresh("x") + ".tar"), /must end in \.zip/);
  const {archive} = packaged();
  assert.throws(() => createArchive(dir, archive), /already exists/);
  fs.symlinkSync(path.join(dir, "run-status.json"), path.join(dir, "alias.json"), "file");
  assert.throws(() => createArchive(dir, fresh("y") + ".zip"), /Refusing a link/);
});

test("each kind of damage is reported on its own", () => {
  const cases = [
    ["a changed byte inside the archive", ({archive}) => {
      const bytes = fs.readFileSync(archive);
      const at = Math.floor(bytes.length / 2);
      bytes[at] ^= 0xff;
      fs.writeFileSync(archive, bytes);
    }, /./],
    ["a checksum record that differs", ({archive}) => fs.writeFileSync(archive + ".sha256", `${"0".repeat(64)}  x.zip\n`), /the archive checksum differs/],
    ["no checksum record", ({archive}) => fs.rmSync(archive + ".sha256"), /no \.sha256 record/],
    ["an inventory hash that differs", ({inventory}) => {
      const doc = JSON.parse(fs.readFileSync(inventory, "utf8")); doc.files[2].sha256 = "0".repeat(64);
      fs.writeFileSync(inventory, JSON.stringify(doc));
    }, /extracted sha256 differs/],
    ["an inventory size that differs", ({inventory}) => {
      const doc = JSON.parse(fs.readFileSync(inventory, "utf8")); doc.files[2].bytes += 1; doc.total_bytes += 1;
      fs.writeFileSync(inventory, JSON.stringify(doc));
    }, /entry size differs from the inventory/],
    ["a file the inventory does not list", ({inventory}) => {
      const doc = JSON.parse(fs.readFileSync(inventory, "utf8")); const removed = doc.files.pop(); doc.file_count -= 1; doc.total_bytes -= removed.bytes;
      fs.writeFileSync(inventory, JSON.stringify(doc));
    }, /entry not in the inventory/],
    ["a file the archive does not hold", ({inventory}) => {
      const doc = JSON.parse(fs.readFileSync(inventory, "utf8")); doc.files.push({path: "ghost.txt", bytes: 3, sha256: "1".repeat(64)});
      doc.file_count += 1; doc.total_bytes += 3; fs.writeFileSync(inventory, JSON.stringify(doc));
    }, /inventory file missing from the archive: ghost\.txt/],
    ["a wrong file count", ({inventory}) => {
      const doc = JSON.parse(fs.readFileSync(inventory, "utf8")); doc.file_count += 1; fs.writeFileSync(inventory, JSON.stringify(doc));
    }, /file count is wrong/],
    ["a wrong byte total", ({inventory}) => {
      const doc = JSON.parse(fs.readFileSync(inventory, "utf8")); doc.total_bytes += 1; fs.writeFileSync(inventory, JSON.stringify(doc));
    }, /byte total is wrong/]
  ];
  for (const [label, damage, expected] of cases) {
    const bundle = packaged();
    damage(bundle);
    const verdict = verifyArchive(bundle.archive);
    assert.equal(verdict.ok, false, label);
    assert.ok(verdict.problems.some(p => expected.test(p)), `${label}: ${JSON.stringify(verdict.problems)}`);
  }
});

test("an entry that could leave the extraction directory is rejected", () => {
  const dir = evidenceDirectory();
  const archive = fresh("traversal") + ".zip";
  const script = "Add-Type -AssemblyName System.IO.Compression; Add-Type -AssemblyName System.IO.Compression.FileSystem; " +
    "$s = [System.IO.File]::Create($env:G17_ARCHIVE); $z = New-Object System.IO.Compression.ZipArchive($s, [System.IO.Compression.ZipArchiveMode]::Create); " +
    "foreach ($n in @('source-sha.txt', '../escaped.txt')) { $e = $z.CreateEntry($n); $w = New-Object System.IO.StreamWriter($e.Open()); $w.Write('x'); $w.Dispose() }; " +
    "$z.Dispose(); $s.Dispose()";
  const run = spawnSync(shellFor(), ["-NoProfile", "-Command", script], {encoding: "utf8", env: cleanEnvironment({G17_ARCHIVE: archive})});
  assert.equal(run.status, 0, run.stderr);
  fs.writeFileSync(archive + ".sha256", `${crypto.createHash("sha256").update(fs.readFileSync(archive)).digest("hex")}  x.zip\n`);
  fs.writeFileSync(archive + ".inventory.json", JSON.stringify({schema_version: 1, source_sha: null, file_count: 1, total_bytes: 1,
    files: [{path: "source-sha.txt", bytes: 1, sha256: crypto.createHash("sha256").update("x").digest("hex")}]}));
  const verdict = verifyArchive(archive);
  assert.equal(verdict.ok, false);
  assert.ok(verdict.problems.some(p => p.includes("could leave the extraction directory: ../escaped.txt")), JSON.stringify(verdict.problems));
  assert.equal(fs.existsSync(path.join(root, "escaped.txt")) || fs.existsSync(path.join(os.tmpdir(), "escaped.txt")), false, "nothing was written outside the extraction directory");
  assert.ok(dir);
});

test("overwriting an existing archive is refused with the explicit message, before anything is written", () => {
  const {dir, archive} = packaged();
  const before = fs.readFileSync(archive);
  assert.throws(() => createArchive(dir, archive), /The archive already exists\. Never overwrite evidence/);
  assert.deepEqual(fs.readFileSync(archive), before);
});

test("an inventory that lists one path twice is rejected even when its count and total agree", () => {
  const {archive, inventory} = packaged();
  const doc = JSON.parse(fs.readFileSync(inventory, "utf8"));
  doc.files.push({...doc.files[0]});
  doc.file_count = doc.files.length;
  doc.total_bytes = doc.files.reduce((n, f) => n + f.bytes, 0);
  fs.writeFileSync(inventory, JSON.stringify(doc));
  const verdict = verifyArchive(archive);
  assert.equal(verdict.ok, false);
  assert.ok(verdict.problems.includes("the inventory lists a path twice"), JSON.stringify(verdict.problems));
});

test("an archive holding the same entry name twice is rejected", () => {
  const archive = fresh("duplicate-entries") + ".zip";
  const script = "Add-Type -AssemblyName System.IO.Compression; Add-Type -AssemblyName System.IO.Compression.FileSystem; " +
    "$s = [System.IO.File]::Create($env:G17_ARCHIVE); $z = New-Object System.IO.Compression.ZipArchive($s, [System.IO.Compression.ZipArchiveMode]::Create); " +
    "foreach ($n in @('a.txt', 'a.txt')) { $e = $z.CreateEntry($n); $w = New-Object System.IO.StreamWriter($e.Open()); $w.Write('x'); $w.Dispose() }; " +
    "$z.Dispose(); $s.Dispose()";
  const run = spawnSync(shellFor(), ["-NoProfile", "-Command", script], {encoding: "utf8", env: cleanEnvironment({G17_ARCHIVE: archive})});
  assert.equal(run.status, 0, run.stderr);
  fs.writeFileSync(archive + ".sha256", `${crypto.createHash("sha256").update(fs.readFileSync(archive)).digest("hex")}  x.zip\n`);
  fs.writeFileSync(archive + ".inventory.json", JSON.stringify({schema_version: 1, source_sha: null, file_count: 1, total_bytes: 1,
    files: [{path: "a.txt", bytes: 1, sha256: crypto.createHash("sha256").update("x").digest("hex")}]}));
  const verdict = verifyArchive(archive);
  assert.equal(verdict.ok, false);
  assert.ok(verdict.problems.includes("duplicate archive entry a.txt"), JSON.stringify(verdict.problems));
});
