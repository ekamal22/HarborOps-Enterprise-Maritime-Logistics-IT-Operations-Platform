import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { execFileSync } from "node:child_process";

const directory = process.env.HARBOROPS_G17_EVIDENCE_DIR;
if (!directory || !/^E:[\\/]/i.test(directory))
  throw Error("G17 evidence must be written under an explicitly selected E: directory");
if (!fs.existsSync(directory) || !fs.statSync(directory).isDirectory())
  throw Error("The owned G17 evidence directory must already exist");
const manifestPath = path.join(directory, "manifest.json");
if (fs.existsSync(manifestPath))
  throw Error("Refusing to replace an existing G17 manifest");

const files = [];
function visit(current, prefix = "") {
  for (const entry of fs.readdirSync(current, { withFileTypes: true })) {
    if (entry.name === ".private" || entry.name === "manifest.json")
      continue;
    const relative = path.posix.join(prefix, entry.name);
    const absolute = path.join(current, entry.name);
    if (entry.isSymbolicLink()) throw Error("Symlink evidence is not allowed: " + relative);
    if (entry.isDirectory()) visit(absolute, relative);
    else if (entry.isFile()) {
      // Only reviewed sanitized report formats may enter the evidence manifest.
      if (!/\.(json|txt|trx|xml)$/i.test(entry.name)) continue;
      const bytes = fs.readFileSync(absolute);
      const body = bytes.toString("utf8");
      const canary = process.env.HARBOROPS_G17_PRIVACY_CANARY;
      if (canary && body.includes(canary))
        throw Error("Private test canary found in evidence file: " + relative);
      const unsafe = [
        /\\bBearer\\s+[A-Za-z0-9._~+/=-]{16,}/i,
        /\\b(?:HARBOROPS_DB_PASSWORD|HARBOROPS_API_DB_PASSWORD|HARBOROPS_WORKER_DB_PASSWORD)\\s*[:=]\\s*[^\\s,;]{6,}/i,
        /\\bCookie:\\s*[^\\r\\n]{16,}/i,
        /\\bPassword\\s*=\\s*[^\\s;]{8,}/i
      ];
      if (unsafe.some(pattern => pattern.test(body)))
        throw Error("Potential credential material in evidence file: " + relative);
      const digest = crypto.createHash("sha256").update(bytes).digest("hex");
      files.push({ path: relative, sha256: digest, bytes: bytes.length });
    }
  }
}
visit(directory);
files.sort((a, b) => a.path.localeCompare(b.path));
const sha = execFileSync("git", ["rev-parse", "HEAD"], { encoding: "utf8" }).trim();
const result = {
  schema_version: 1,
  gate: "G17",
  source_sha: sha,
  captured_at_utc: new Date().toISOString(),
  files,
  assertion_disposition: "NOT_REVIEWED"
};
fs.writeFileSync(manifestPath, JSON.stringify(result, null, 2) + "\n", { flag: "wx" });
console.log("Recorded", files.length, "sanitized evidence files at", sha);
