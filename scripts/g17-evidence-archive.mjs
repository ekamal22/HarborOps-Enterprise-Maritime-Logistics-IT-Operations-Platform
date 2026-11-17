// Packaging of a G17 evidence directory as a REAL zip archive, and its independent verification.
//   node scripts/g17-evidence-archive.mjs create <evidence-directory> <archive.zip>
//   node scripts/g17-evidence-archive.mjs verify <archive.zip> [<inventory.json>]
// create writes <archive>, <archive>.inventory.json (relative path, byte size and SHA-256 of every file, plus the
// source identity found in source-sha.txt) and <archive>.sha256. verify never trusts the extension or the checksum
// alone: it checks the zip signature and rejects a tar or gzip stream, reads the archive's own entry table and
// extracts it with a standard reader (the .NET ZipFile class, not the tool that wrote it) into a fresh directory,
// and compares every extracted file with the inventory. A mismatch of any file, an extra or missing file, a link or
// an entry that could leave the extraction directory fails verification.
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import crypto from "node:crypto";
import {spawnSync} from "node:child_process";
import {fileURLToPath} from "node:url";

const sha256 = bytes => crypto.createHash("sha256").update(bytes).digest("hex");

export function buildInventory(root) {
  const files = [];
  const walk = (directory, prefix) => {
    for (const entry of fs.readdirSync(directory, {withFileTypes: true})) {
      const relative = prefix ? `${prefix}/${entry.name}` : entry.name;
      const full = path.join(directory, entry.name);
      if (entry.isSymbolicLink()) throw new Error(`Refusing a link in evidence: ${relative}`);
      if (entry.isDirectory()) walk(full, relative);
      else if (entry.isFile()) { const bytes = fs.readFileSync(full); files.push({path: relative, bytes: bytes.length, sha256: sha256(bytes)}); }
      else throw new Error(`Unsupported object in evidence: ${relative}`);
    }
  };
  walk(root, "");
  return files.sort((a, b) => (a.path < b.path ? -1 : a.path > b.path ? 1 : 0));
}

export function inventoryDocument(root) {
  const files = buildInventory(root);
  const sourceFile = path.join(root, "source-sha.txt");
  const source = fs.existsSync(sourceFile) ? fs.readFileSync(sourceFile, "utf8").trim() : null;
  return {schema_version: 1, source_sha: source, file_count: files.length, total_bytes: files.reduce((n, f) => n + f.bytes, 0), files};
}

export function archiveKind(bytes) {
  if (bytes.length >= 4 && bytes[0] === 0x50 && bytes[1] === 0x4b && [0x03, 0x05, 0x07].includes(bytes[2])) return "zip";
  if (bytes.length >= 2 && bytes[0] === 0x1f && bytes[1] === 0x8b) return "gzip";
  if (bytes.length >= 262 && bytes.subarray(257, 262).toString("latin1") === "ustar") return "tar";
  return "unknown";
}

const windowsTar = () => path.join(process.env.SystemRoot ?? "C:\\Windows", "System32", "tar.exe");
const powershell = () => {
  for (const exe of ["powershell.exe", "pwsh"]) if (spawnSync(exe, ["-NoProfile", "-Command", "1"], {encoding: "utf8"}).status === 0) return exe;
  throw new Error("A PowerShell host is required to read the archive with the standard zip reader");
};
const cleanEnv = extra => {
  const env = {...process.env, ...extra};
  for (const key of Object.keys(env)) if (key.toLowerCase() === "psmodulepath") delete env[key];
  return env;
};

export function createArchive(evidenceDirectory, archivePath) {
  if (!archivePath.toLowerCase().endsWith(".zip")) throw new Error("The archive name must end in .zip");
  if (fs.existsSync(archivePath)) throw new Error("The archive already exists. Never overwrite evidence");
  const document = inventoryDocument(evidenceDirectory);
  // bsdtar (Windows tar.exe, libarchive) writes a standard zip when the name ends in .zip.
  const run = spawnSync(windowsTar(), ["-a", "-c", "-f", archivePath, "-C", evidenceDirectory, "."], {encoding: "utf8", timeout: 600000});
  if (run.status !== 0) throw new Error("tar.exe failed to create the zip archive: " + run.stderr);
  const bytes = fs.readFileSync(archivePath);
  fs.writeFileSync(archivePath + ".inventory.json", JSON.stringify(document, null, 2) + "\n", {flag: "wx"});
  fs.writeFileSync(archivePath + ".sha256", `${sha256(bytes)}  ${path.basename(archivePath)}\n`, {flag: "wx"});
  return {archive: archivePath, archive_sha256: sha256(bytes), archive_bytes: bytes.length, file_count: document.file_count, total_bytes: document.total_bytes};
}

function readEntries(archivePath) {
  const script = "Add-Type -AssemblyName System.IO.Compression.FileSystem; $z = [System.IO.Compression.ZipFile]::OpenRead($env:G17_ARCHIVE); " +
    "try { @($z.Entries | ForEach-Object { [pscustomobject]@{ n = $_.FullName; l = [long]$_.Length } }) | ConvertTo-Json -Compress } finally { $z.Dispose() }";
  const run = spawnSync(powershell(), ["-NoProfile", "-Command", script], {encoding: "utf8", env: cleanEnv({G17_ARCHIVE: archivePath}), maxBuffer: 64 * 1024 * 1024});
  if (run.status !== 0) throw new Error("The standard zip reader could not read the archive: " + (run.stderr || run.stdout).slice(0, 300));
  const parsed = JSON.parse(run.stdout.trim() || "[]");
  return Array.isArray(parsed) ? parsed : [parsed];
}

function extract(archivePath, destination) {
  const script = "Add-Type -AssemblyName System.IO.Compression.FileSystem; [System.IO.Compression.ZipFile]::ExtractToDirectory($env:G17_ARCHIVE, $env:G17_DEST)";
  const run = spawnSync(powershell(), ["-NoProfile", "-Command", script], {encoding: "utf8", env: cleanEnv({G17_ARCHIVE: archivePath, G17_DEST: destination}), maxBuffer: 64 * 1024 * 1024});
  if (run.status !== 0) throw new Error("The standard zip reader could not extract the archive: " + (run.stderr || run.stdout).slice(0, 300));
}

export function verifyArchive(archivePath, inventoryPath = archivePath + ".inventory.json") {
  const problems = [];
  const head = Buffer.alloc(512);
  const handle = fs.openSync(archivePath, "r");
  const read = fs.readSync(handle, head, 0, 512, 0);
  fs.closeSync(handle);
  const kind = archiveKind(head.subarray(0, read));
  if (kind !== "zip") return {ok: false, problems: [`the file is a ${kind} stream, not a zip archive`], kind};
  const checksumFile = archivePath + ".sha256";
  if (fs.existsSync(checksumFile)) {
    const listed = fs.readFileSync(checksumFile, "utf8").trim().split(/\s+/)[0];
    if (listed !== sha256(fs.readFileSync(archivePath))) problems.push("the archive checksum differs from its .sha256 record");
  } else problems.push("no .sha256 record");
  const inventory = JSON.parse(fs.readFileSync(inventoryPath, "utf8"));
  const expected = new Map(inventory.files.map(f => [f.path, f]));
  if (expected.size !== inventory.files.length) problems.push("the inventory lists a path twice");
  if (inventory.file_count !== inventory.files.length) problems.push("the inventory file count is wrong");
  if (inventory.total_bytes !== inventory.files.reduce((n, f) => n + f.bytes, 0)) problems.push("the inventory byte total is wrong");

  let entries;
  try { entries = readEntries(archivePath); } catch (error) { return {ok: false, problems: [...problems, error.message], kind}; }
  const names = new Set();
  for (const entry of entries) {
    const name = entry.n.replace(/^\.\//, "");
    if (name === "" || name.endsWith("/")) continue; // directory entries
    if (name.startsWith("/") || /^[A-Za-z]:/.test(name) || name.includes("\\") || name.split("/").includes("..")) problems.push(`entry could leave the extraction directory: ${entry.n}`);
    if (names.has(name)) problems.push(`duplicate archive entry ${name}`);
    names.add(name);
    if (!expected.has(name)) problems.push(`entry not in the inventory: ${name}`);
    else if (expected.get(name).bytes !== entry.l) problems.push(`entry size differs from the inventory: ${name}`);
  }
  for (const name of expected.keys()) if (!names.has(name)) problems.push(`inventory file missing from the archive: ${name}`);

  const destination = fs.mkdtempSync(path.join(os.tmpdir(), "g17-archive-verify-"));
  try {
    extract(archivePath, destination);
    const actual = buildInventory(destination);
    const actualMap = new Map(actual.map(f => [f.path, f]));
    for (const [name, file] of expected) {
      const found = actualMap.get(name);
      if (!found) problems.push(`not extracted: ${name}`);
      else if (found.bytes !== file.bytes) problems.push(`extracted size differs: ${name}`);
      else if (found.sha256 !== file.sha256) problems.push(`extracted sha256 differs: ${name}`);
    }
    for (const name of actualMap.keys()) if (!expected.has(name)) problems.push(`extracted file not in the inventory: ${name}`);
  } catch (error) { problems.push(error.message); }
  finally { fs.rmSync(destination, {recursive: true, force: true}); }
  return {ok: problems.length === 0, problems, kind, file_count: inventory.files.length, total_bytes: inventory.total_bytes,
    archive_sha256: sha256(fs.readFileSync(archivePath))};
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [command, first, second] = process.argv.slice(2);
  if (command === "create" && first && second) console.log(JSON.stringify(createArchive(path.resolve(first), path.resolve(second)), null, 2));
  else if (command === "verify" && first) {
    const result = verifyArchive(path.resolve(first), second ? path.resolve(second) : undefined);
    console.log(JSON.stringify(result, null, 2));
    process.exitCode = result.ok ? 0 : 1;
  } else { console.error("usage: g17-evidence-archive.mjs create <evidence-directory> <archive.zip> | verify <archive.zip> [<inventory.json>]"); process.exitCode = 2; }
}
