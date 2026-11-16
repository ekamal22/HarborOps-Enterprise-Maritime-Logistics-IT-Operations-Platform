// Independent check of a controlled-protocol diagnostic directory against its own diag-hashes.txt.
// Line format written by docs/m17-s25-p09-controlled-protocol-20261115/diagnostics.ps1:
//   <sha256 lowercase hex>  <byte length>  <file name>
// The hash list never describes itself. diag-state.json must already be final (stopped_utc present),
// because the list is only correct when it is produced after the last write of every listed file.
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import {fileURLToPath} from "node:url";

export const HASH_LIST = "diag-hashes.txt";
export const STATE_FILE = "diag-state.json";
const line = /^([0-9a-f]{64}) {2}(0|[1-9][0-9]*) {2}([^\\/\r\n\s](?:[^\\/\r\n]*[^\\/\r\n\s])?)$/;

export function sha256(bytes) {
  return crypto.createHash("sha256").update(bytes).digest("hex");
}

export function parseHashList(text) {
  const errors = [];
  const entries = [];
  if (typeof text !== "string" || !text.length) return {entries, errors: ["empty hash list"]};
  const lines = text.split(/\r?\n/);
  if (lines[lines.length - 1] === "") lines.pop();
  for (const [index, raw] of lines.entries()) {
    const match = line.exec(raw);
    if (!match) { errors.push(`line ${index + 1} is not '<sha256>  <size>  <name>'`); continue; }
    const name = match[3];
    if (name === "." || name === "..") { errors.push(`line ${index + 1} names a directory`); continue; }
    entries.push({sha256: match[1], size: Number(match[2]), name});
  }
  return {entries, errors};
}

// options.onlyListed: verify the listed files that exist and do not demand completeness. It exists solely so the
// two historical files committed to the repository can be characterised without the bulky raw captures.
export function verifyDiagnosticDirectory(directory, {onlyListed = false, requireFinalState = true} = {}) {
  const errors = [];
  const listPath = path.join(directory, HASH_LIST);
  if (!fs.existsSync(listPath)) return {ok: false, errors: [`${HASH_LIST} is missing`], entries: []};
  const {entries, errors: parseErrors} = parseHashList(fs.readFileSync(listPath, "utf8"));
  errors.push(...parseErrors);
  const seen = new Set();
  for (const entry of entries) {
    if (entry.name === HASH_LIST) { errors.push(`${HASH_LIST} lists itself`); continue; }
    if (seen.has(entry.name)) { errors.push(`duplicate entry ${entry.name}`); continue; }
    seen.add(entry.name);
    const file = path.join(directory, entry.name);
    if (!fs.existsSync(file)) {
      if (!onlyListed) errors.push(`listed file is missing: ${entry.name}`);
      continue;
    }
    const bytes = fs.readFileSync(file);
    if (bytes.length !== entry.size) errors.push(`size mismatch for ${entry.name}: listed ${entry.size}, actual ${bytes.length}`);
    if (sha256(bytes) !== entry.sha256) errors.push(`sha256 mismatch for ${entry.name}`);
  }
  if (!onlyListed) {
    for (const name of fs.readdirSync(directory)) {
      if (name === HASH_LIST) continue;
      if (!fs.statSync(path.join(directory, name)).isFile()) continue;
      if (!seen.has(name)) errors.push(`file is not covered by the hash list: ${name}`);
    }
  }
  if (!seen.has(STATE_FILE)) errors.push(`${STATE_FILE} is not listed`);
  if (requireFinalState) {
    const statePath = path.join(directory, STATE_FILE);
    if (fs.existsSync(statePath)) {
      try {
        const state = JSON.parse(fs.readFileSync(statePath, "utf8").replace(/^\uFEFF/, ""));
        if (typeof state.stopped_utc !== "string" || Number.isNaN(Date.parse(state.stopped_utc)))
          errors.push(`${STATE_FILE} has no valid stopped_utc`);
        else if (Date.parse(state.stopped_utc) < Date.parse(state.started_utc))
          errors.push(`${STATE_FILE} stopped_utc precedes started_utc`);
      } catch { errors.push(`${STATE_FILE} is not valid JSON`); }
    }
  }
  return {ok: errors.length === 0, errors, entries};
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const directory = process.argv[2];
  if (!directory) { console.error("usage: node g17-diagnostic-hash-list.mjs <diagnostic-directory> [--only-listed]"); process.exit(2); }
  const result = verifyDiagnosticDirectory(directory, {onlyListed: process.argv.includes("--only-listed")});
  console.log(JSON.stringify({directory, ok: result.ok, errors: result.errors}, null, 2));
  process.exitCode = result.ok ? 0 : 1;
}
