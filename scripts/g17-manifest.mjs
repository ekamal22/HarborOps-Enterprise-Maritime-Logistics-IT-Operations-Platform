import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { execFileSync } from "node:child_process";
import {allowedEvidencePath,textEvidencePath} from "./g17-evidence-files.mjs";

const directory = process.env.HARBOROPS_G17_EVIDENCE_DIR;
const hostedEvidence = process.env.GITHUB_ACTIONS === "true" &&
  process.env.HARBOROPS_G17_HOSTED === "true" &&
  process.env.RUNNER_TEMP &&
  path.resolve(process.env.HARBOROPS_G17_EVIDENCE_DIR || "").startsWith(
    path.resolve(process.env.RUNNER_TEMP) + path.sep);
if (!directory || (!/^E:[\\/]/i.test(directory) && !hostedEvidence))
  throw Error("Use a new, positively owned E: evidence directory");
if (!fs.existsSync(directory) || !fs.statSync(directory).isDirectory())
  throw Error("The evidence directory must already exist");
const manifestPath = path.join(directory, "manifest.json");
if (fs.existsSync(manifestPath))
  throw Error("Never overwrite an existing G17 evidence manifest");

// Only enumerated disposable-service logs are allowed as diagnostic text.
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
    if (!entry.isFile() || !allowedEvidencePath(relative))
      throw Error("Unlisted or unsupported evidence type: " + relative);

    const bytes = fs.readFileSync(absolute);
    if (canary && bytes.includes(Buffer.from(canary, "utf8")))
      throw Error("Privacy canary found in evidence: " + relative);
    if (textEvidencePath(relative)) {
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
const indexed = new Set(entries.map(e => e.path));
for (const required of ["source-sha.txt","expected-artifacts.json","run-status.json"]) {
  if (!indexed.has(required)) throw Error("Missing required run ledger: " + required);
}
const status = JSON.parse(fs.readFileSync(path.join(directory,"run-status.json"),"utf8"));
const expected = JSON.parse(fs.readFileSync(path.join(directory,"expected-artifacts.json"),"utf8"));
if (status.gate !== "G17" || expected.gate !== "G17" ||
    status.source_verified !== true ||
    status.source_sha !== expected.source_sha ||
    !["COMPLETE","FAILED"].includes(status.run_state) ||
    status.gate_acceptance !== "NOT_ACCEPTED" ||
    !Array.isArray(expected.required_artifacts)) {
  throw Error("Invalid or unsafe G17 campaign ledger");
}
const missing = expected.required_artifacts.filter(name => !indexed.has(name));
if (status.run_state === "COMPLETE" && missing.length)
  throw Error("A completed campaign has missing raw evidence: " + missing.join(", "));

const sha = execFileSync("git", ["rev-parse", "HEAD"], { encoding: "utf8" }).trim();
if (!/^[a-f0-9]{40}$/.test(sha))
  throw Error("The source SHA is not an exact commit identifier");

const inventoryBytes = fs.readFileSync("docs/M17_G17_ASSERTION_INVENTORY_20261112.json");
const inventory = JSON.parse(inventoryBytes.toString("utf8"));
const assertionCount = inventory.scenarios?.reduce((sum, item) => sum + item.assertions.length, 0);
if (inventory.scenarios?.length !== 25 || assertionCount !== 75 ||
    inventory.gate_acceptance !== "NOT_ACCEPTED")
  throw Error("The G17 assertion inventory no longer matches its source-locked contract");
const result = {
  schema_version: 2, gate: "G17", source_sha: sha,
  assertion_inventory_sha256: crypto.createHash("sha256").update(inventoryBytes).digest("hex"),
  scenario_count: 25, assertion_count: 75, reviewed_assertion_count: 0,
  captured_at_utc: new Date().toISOString(),
  candidate_execution: process.env.HARBOROPS_G17_DISPOSABLE_STACK_STARTED === "true"
    ? "RUNTIME_CANDIDATE_NOT_REVIEWED" : "SOURCE_ONLY_NOT_REVIEWED",
  files: entries, binary_files_requiring_manual_review: binaryCount,
  run_state: status.run_state, expected_artifact_count: expected.required_artifacts.length,
  missing_expected_artifacts: missing,
  assertion_disposition: "NOT_REVIEWED",
  gate_acceptance: "NOT_ACCEPTED"
};
fs.writeFileSync(manifestPath, JSON.stringify(result, null, 2) + "\n", { flag: "wx" });
console.log("Manifested", entries.length, "files for unreviewed G17 candidate", sha);
