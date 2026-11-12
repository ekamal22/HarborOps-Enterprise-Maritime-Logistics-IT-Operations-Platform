import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { execFileSync } from "node:child_process";

const directory = process.env.HARBOROPS_G17_EVIDENCE_DIR;
if (!directory || !/^E:[\\/]/i.test(directory))
  throw Error("Use a new, positively owned E: evidence directory");
if (!fs.existsSync(directory) || !fs.statSync(directory).isDirectory())
  throw Error("The evidence directory must already exist");
const manifestPath = path.join(directory, "manifest.json");
if (fs.existsSync(manifestPath))
  throw Error("Never overwrite an existing G17 evidence manifest");

const allowed = /\.(json|txt|trx|xml|png|webm|zip)$/i;
const textFile = /\.(json|txt|trx|xml)$/i;
const canary = process.env.HARBOROPS_G17_PRIVACY_CANARY;
const entries = [];
let binaryCount = 0;

function visit(current, prefix = "") {
  for (const entry of fs.readdirSync(current, { withFileTypes: true })) {
    const relative = path.posix.join(prefix, entry.name);
    if (relative === "manifest.json") continue;
    const absolute = path.join(current, entry.name);
    if (entry.isSymbolicLink()) throw Error("Symlink not permitted: " + relative);
    if (entry.isDirectory()) { visit(absolute, relative); continue; }
    if (!entry.isFile() || !allowed.test(entry.name))
      throw Error("Unlisted or unsupported evidence type: " + relative);

    const bytes = fs.readFileSync(absolute);
    if (canary && bytes.includes(Buffer.from(canary, "utf8")))
      throw Error("Privacy canary found in evidence: " + relative);
    if (textFile.test(entry.name)) {
      const body = bytes.toString("utf8");
      const forbidden = [
        /\bBearer\s+[A-Za-z0-9._~+\/=-]{16,}/i,
        /\b(?:HARBOROPS_DB_PASSWORD|HARBOROPS_API_DB_PASSWORD|HARBOROPS_WORKER_DB_PASSWORD)\s*[:=]\s*[^\s,;]{6,}/i,
        /\bCookie:\s*[^\r\n]{16,}/i,
        /\bPassword\s*=\s*[^\s;]{8,}/i
      ];
      if (forbidden.some(pattern => pattern.test(body)))
        throw Error("Potential credential material in evidence: " + relative);
    } else {
      // Binary screenshots and compressed Playwright traces require separate
      // manual privacy review; a hash is not a redaction certificate.
      binaryCount++;
    }
    entries.push({
      path: relative,
      sha256: crypto.createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length
    });
  }
}
visit(directory);
entries.sort((a, b) => a.path.localeCompare(b.path));
const sha = execFileSync("git", ["rev-parse", "HEAD"], { encoding: "utf8" }).trim();
if (!/^[a-f0-9]{40}$/.test(sha))
  throw Error("The source SHA is not an exact commit identifier");

const result = {
  schema_version: 2, gate: "G17", source_sha: sha,
  captured_at_utc: new Date().toISOString(),
  candidate_execution: process.env.HARBOROPS_G17_DISPOSABLE_STACK_STARTED === "true"
    ? "RUNTIME_CANDIDATE_NOT_REVIEWED" : "SOURCE_ONLY_NOT_REVIEWED",
  files: entries, binary_files_requiring_manual_review: binaryCount,
  assertion_disposition: "NOT_REVIEWED",
  gate_acceptance: "NOT_ACCEPTED"
};
fs.writeFileSync(manifestPath, JSON.stringify(result, null, 2) + "\n", { flag: "wx" });
console.log("Manifested", entries.length, "files for unreviewed G17 candidate", sha);
