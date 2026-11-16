// Independent check of a controlled-protocol diagnostic directory against its own diag-hashes.txt.
// Line format written by docs/m17-s25-p09-controlled-protocol-20261115/diagnostics.ps1:
//   <sha256 lowercase hex>  <byte length>  <file name>
// The hash list never describes itself. diag-state.json must already be final (stopped_utc present),
// because the list is only correct when it is produced after the last write of every listed file.
//
// Two modes exist and must not be confused:
//   strict (the default, and the only mode for new evidence): every entry must exist, match its size and checksum,
//     be a plain regular file (no link, no directory), and the directory must hold nothing the list does not cover.
//   historical characterization (onlyListed): checks only the listed files that are present. It exists solely so the
//     two committed r1c/r2/r3 records can be characterised without their bulky raw captures. Its result says so,
//     and it can never report a complete, trustworthy directory.
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import {fileURLToPath} from "node:url";

export const HASH_LIST = "diag-hashes.txt";
export const STATE_FILE = "diag-state.json";
// Names may not contain path separators, control characters, or the characters Windows reserves (a colon would
// also address an alternate data stream of another file), and may not start or end with whitespace.
const line = /^([0-9a-f]{64}) {2}(0|[1-9][0-9]*) {2}([^\\/:<>"|?*\x00-\x1f\s](?:[^\\/:<>"|?*\x00-\x1f]*[^\\/:<>"|?*\x00-\x1f\s])?)$/;
const CHILD_OUTCOMES = new Set(["terminated", "already-exited"]);
const SIDECAR_OUTCOMES = new Set(["removed", "absent"]);

export function sha256(bytes) {
  return crypto.createHash("sha256").update(bytes).digest("hex");
}

// Strict UTC instant as written by .NET ToString("o"): yyyy-MM-ddTHH:mm:ss[.fffffff]Z. Returns a comparable BigInt
// count of 100 ns ticks since the epoch, or null when the text is not exactly such an instant (Date.parse alone
// accepts far looser forms, including a bare year, and silently turns a missing value into NaN).
export function parseUtcTicks(text) {
  if (typeof text !== "string") return null;
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?Z$/.exec(text);
  if (!match) return null;
  const [year, month, day, hour, minute, second] = match.slice(1, 7).map(Number);
  const instant = new Date(Date.UTC(year, month - 1, day, hour, minute, second));
  if (instant.getUTCFullYear() !== year || instant.getUTCMonth() !== month - 1 || instant.getUTCDate() !== day ||
      instant.getUTCHours() !== hour || instant.getUTCMinutes() !== minute || instant.getUTCSeconds() !== second) return null;
  const fraction = BigInt((match[7] ?? "").padEnd(7, "0"));
  return BigInt(instant.getTime()) * 10000n + fraction;
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
    const size = Number(match[2]);
    if (!Number.isSafeInteger(size)) { errors.push(`line ${index + 1} has an impossible size`); continue; }
    entries.push({sha256: match[1], size, name});
  }
  return {entries, errors};
}

// Reads a regular file without following a link. Returns {bytes} or {error}.
function readRegularFile(directory, name) {
  const file = path.join(directory, name);
  let stat;
  try { stat = fs.lstatSync(file); } catch { return {missing: true}; }
  if (stat.isSymbolicLink()) return {error: `${name} is a symbolic link`};
  if (!stat.isFile()) return {error: `${name} is not a regular file`};
  return {bytes: fs.readFileSync(file)};
}

function checkFinalState(directory, errors) {
  const statePath = path.join(directory, STATE_FILE);
  const read = readRegularFile(directory, STATE_FILE);
  if (read.missing || read.error) return; // reported by the entry checks
  let state;
  try { state = JSON.parse(read.bytes.toString("utf8").replace(/^﻿/, "")); }
  catch { errors.push(`${STATE_FILE} is not valid JSON`); return; }
  if (state === null || typeof state !== "object" || Array.isArray(state)) { errors.push(`${STATE_FILE} is not a JSON object`); return; }
  const started = parseUtcTicks(state.started_utc);
  const stopped = parseUtcTicks(state.stopped_utc);
  if (started === null) errors.push(`${STATE_FILE} has no valid started_utc`);
  if (stopped === null) errors.push(`${STATE_FILE} has no valid stopped_utc`);
  if (started !== null && stopped !== null && stopped < started) errors.push(`${STATE_FILE} stopped_utc precedes started_utc`);
  if (state.schema_version === 2) {
    if (typeof state.project !== "string" || !/^ho-verify-m17-[a-z0-9-]+$/.test(state.project))
      errors.push(`${STATE_FILE} names no owned project`);
    else if (state.sidecar !== `${state.project}-p09-diag`) errors.push(`${STATE_FILE} sidecar does not belong to its project`);
    const outcomes = state.stop_outcomes;
    if (outcomes === null || typeof outcomes !== "object" || Array.isArray(outcomes)) errors.push(`${STATE_FILE} records no stop outcomes`);
    else {
      for (const kind of ["typeperf", "events", "logs"])
        if (!CHILD_OUTCOMES.has(outcomes[kind])) errors.push(`${STATE_FILE} does not confirm that ${kind} exited`);
      if (!SIDECAR_OUTCOMES.has(outcomes.sidecar)) errors.push(`${STATE_FILE} does not confirm the sidecar removal`);
    }
  }
}

// options.onlyListed: historical characterization (see the header). requireFinalState: demand a valid final state.
export function verifyDiagnosticDirectory(directory, {onlyListed = false, requireFinalState = true} = {}) {
  const errors = [];
  const mode = onlyListed ? "historical-characterization" : "strict";
  const listRead = readRegularFile(directory, HASH_LIST);
  if (listRead.missing) return {ok: false, mode, errors: [`${HASH_LIST} is missing`], entries: []};
  if (listRead.error) return {ok: false, mode, errors: [listRead.error], entries: []};
  const {entries, errors: parseErrors} = parseHashList(listRead.bytes.toString("utf8"));
  errors.push(...parseErrors);
  const seen = new Set();
  const seenFolded = new Map();
  for (const entry of entries) {
    if (entry.name === HASH_LIST) { errors.push(`${HASH_LIST} lists itself`); continue; }
    if (seen.has(entry.name)) { errors.push(`duplicate entry ${entry.name}`); continue; }
    const folded = entry.name.toLowerCase();
    if (seenFolded.has(folded)) { errors.push(`entries differ only by case: ${seenFolded.get(folded)} and ${entry.name}`); continue; }
    seen.add(entry.name);
    seenFolded.set(folded, entry.name);
    const read = readRegularFile(directory, entry.name);
    if (read.missing) {
      if (!onlyListed) errors.push(`listed file is missing: ${entry.name}`);
      continue;
    }
    if (read.error) { errors.push(read.error); continue; }
    if (read.bytes.length !== entry.size) errors.push(`size mismatch for ${entry.name}: listed ${entry.size}, actual ${read.bytes.length}`);
    if (sha256(read.bytes) !== entry.sha256) errors.push(`sha256 mismatch for ${entry.name}`);
  }
  if (!onlyListed) {
    for (const dirent of fs.readdirSync(directory, {withFileTypes: true})) {
      if (dirent.name === HASH_LIST) continue;
      if (dirent.isSymbolicLink()) { if (!seen.has(dirent.name)) errors.push(`${dirent.name} is a symbolic link`); continue; }
      if (!dirent.isFile()) { errors.push(`${dirent.name} is not a regular file and is not covered by the hash list`); continue; }
      if (!seen.has(dirent.name)) errors.push(`file is not covered by the hash list: ${dirent.name}`);
    }
  }
  if (!seen.has(STATE_FILE)) errors.push(`${STATE_FILE} is not listed`);
  if (requireFinalState) checkFinalState(directory, errors);
  // A historical characterization can never vouch for a directory: it did not look at the files it skipped.
  return {ok: errors.length === 0 && !onlyListed, mode, errors, entries};
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const directory = process.argv[2];
  if (!directory) { console.error("usage: node g17-diagnostic-hash-list.mjs <diagnostic-directory> [--historical-characterization]"); process.exit(2); }
  const historical = process.argv.includes("--historical-characterization");
  const result = verifyDiagnosticDirectory(directory, {onlyListed: historical});
  console.log(JSON.stringify({directory, mode: result.mode, ok: result.ok, errors: result.errors}, null, 2));
  process.exitCode = result.ok ? 0 : 1;
}
