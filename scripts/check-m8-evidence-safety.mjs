#!/usr/bin/env node
// Scan every shareable evidence file, including final runtime logs and binary artifacts.
import assert from "node:assert/strict";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";

const [root, evidenceOverride] = process.argv.slice(2);
assert.ok(root && /^(?:[Ee]:[\\/]|\/e\/)/.test(root), "E:-backed G8 root required");
const evidence = evidenceOverride || join(root, "evidence");
assert.ok(existsSync(evidence), "Evidence directory required");
const secret = name => {
  const target = join(root, "private", name);
  return existsSync(target) ? readFileSync(target, "utf8").trim() : "";
};
const canaries = [secret("source-bearer.txt"), secret("test-user-password.txt")]
  .filter(x => x.length);
let checked = 0;
const walk = folder => {
  for (const entry of readdirSync(folder, { withFileTypes:true })) {
    const path = join(folder, entry.name);
    if (entry.isDirectory()) { walk(path); continue; }
    assert.ok(entry.isFile() && !entry.isSymbolicLink(), "Unexpected evidence file type: " + path);
    const bytes = readFileSync(path);
    for (const canary of canaries)
      assert.ok(!bytes.includes(Buffer.from(canary)), "Private credential detected in evidence: " + path);
    const body = bytes.toString("utf8");
    assert.ok(!/hop_v1_[0-9a-f]{32}_[A-Za-z0-9_-]{43}/.test(body),
      "Machine credential detected in evidence: " + path);
    assert.ok(!/"sourceEventId"\s*:\s*"[^"]+"/.test(body),
      "Raw publisher event detected in evidence: " + path);
    checked++;
  }
};
walk(evidence);
assert.ok(checked > 0, "No evidence files checked");
console.log("G8 shareable evidence safety PASS:", checked, "files");
