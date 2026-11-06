#!/usr/bin/env node
// Examine all evidence before any review copy is made. ZIP/trace archives
// are quarantined because compressed cookies cannot be certified by a text scan.
import assert from "node:assert/strict";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { join, extname } from "node:path";

const root = process.argv[2];
assert.ok(root && /^(?:[Ee]:[\\/]|\/e\/)/.test(root),
  "An E:-backed verification root is required");
const evidence = join(root, "evidence");
assert.ok(existsSync(evidence), "Evidence root missing");
const privatePassword = join(root, "private", "test-password.txt");
const canaries = [
  existsSync(privatePassword) ? readFileSync(privatePassword, "utf8").trim() : "",
  process.env.HARBOROPS_DB_PASSWORD,
  process.env.HARBOROPS_API_DB_PASSWORD,
  process.env.HARBOROPS_WORKER_DB_PASSWORD
].filter(value => value && value.length >= 12);
let files = 0;
function check(folder) {
  for (const entry of readdirSync(folder, { withFileTypes: true })) {
    const name = join(folder, entry.name);
    if (entry.isDirectory()) { check(name); continue; }
    assert.ok(entry.isFile() && !entry.isSymbolicLink(),
      "Unexpected evidence file type");
    assert.ok(![".zip", ".gz", ".tar", ".7z"].includes(extname(name).toLowerCase()),
      "Compressed evidence requires separate private inspection: " + name);
    const content = readFileSync(name);
    for (const canary of canaries)
      assert.ok(!content.includes(Buffer.from(canary)),
        "Private credential in evidence: " + name);
    const text = content.toString("utf8");
    assert.ok(!/hop_v1_[0-9a-f]{32}_[A-Za-z0-9_-]{43}/.test(text),
      "Source bearer in evidence: " + name);
    assert.ok(!/HarborOps\.Session=[A-Za-z0-9%._-]{15,}/i.test(text),
      "Session cookie in evidence: " + name);
    assert.ok(!/HarborOps\.Antiforgery=[A-Za-z0-9%._-]{15,}/i.test(text),
      "CSRF cookie in evidence: " + name);
    assert.ok(!/authorization["': ]+bearer\s+[A-Za-z0-9._-]{15,}/i.test(text),
      "Authorization header in evidence: " + name);
    files++;
  }
}
check(evidence);
assert.ok(files > 0, "No evidence examined");
console.log("G16 evidence redaction probe passed for", files, "uncompressed files");
