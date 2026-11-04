#!/usr/bin/env node
// Fail closed on credentials or complete publisher payloads in shareable evidence.
import assert from "node:assert/strict";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { join, extname } from "node:path";

const root = process.argv[2];
assert.ok(root && /^(?:[Ee]:[\\/]|\/e\/)/.test(root), "E:-backed G8 root required");
const evidence = join(root, "evidence");
assert.ok(existsSync(evidence), "Evidence directory required");
const read = name => {
  const path = join(root, "private", name);
  return existsSync(path) ? readFileSync(path, "utf8").trim() : "";
};
const bearer = read("source-bearer.txt");
const password = read("test-user-password.txt");
const suffixes = new Set([".log", ".json", ".tsv", ".txt", ".trx", ".xml", ".csv"]);
let scanned = 0;
const walk = dir => {
  for (const entry of readdirSync(dir, { withFileTypes:true })) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) { walk(path); continue; }
    if (!entry.isFile() || !suffixes.has(extname(path).toLowerCase())) continue;
    const text = readFileSync(path, "utf8");
    assert.ok(!bearer || !text.includes(bearer), "Source bearer found in evidence: " + path);
    assert.ok(!password || !text.includes(password), "Fixture password found in evidence: " + path);
    assert.ok(!/hop_v1_[0-9a-f]{32}_[A-Za-z0-9_-]{43}/.test(text),
      "Machine credential pattern found in evidence: " + path);
    assert.ok(!/"sourceEventId"\s*:\s*"[^"]+"/.test(text),
      "Raw publisher payload found in shareable evidence: " + path);
    scanned++;
  }
};
walk(evidence);
assert.ok(scanned > 0, "No evidence files inspected");
console.log("G8 sanitized evidence safety checked in " + scanned + " text files");
