import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { TURKISH } from "../src/i18n/messages.mjs";
import { translate } from "../src/i18n/i18n.mjs";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const read = relative => readFileSync(resolve(root, relative), "utf8");
const workspace = read("web/src/incidents/IncidentWorkspace.tsx");
const migration = read("src/HarborOps.Infrastructure/Persistence/Migrations/20261118190000_M18IncidentLifecycle.cs");

function listFrom(source, constraint) {
  const block = source.split(constraint)[1];
  assert.ok(block, "constraint not found: " + constraint);
  const body = block.slice(0, block.indexOf(")")).replace(/\s+/g, " ");
  return [...body.matchAll(/'([A-Za-z0-9_-]+)'/g)].map(m => m[1]);
}

test("every statically referenced incident string exists in Turkish", () => {
  const keys = new Set([...workspace.matchAll(/\bt\("([^"\n]+)"/g)].map(m => m[1]));
  const errorBlock = workspace.slice(workspace.indexOf("function incidentError"),
    workspace.indexOf("function definitive"));
  for (const m of errorBlock.matchAll(/return\s*\n?\s*"([^"]+)"/g)) keys.add(m[1]);
  for (const m of errorBlock.matchAll(/\?\s*\n?\s*"([^"]+)"/g)) keys.add(m[1]);
  assert.ok(keys.size >= 60, "expected the full incident surface, found " + keys.size);
  for (const key of keys) assert.ok(Object.hasOwn(TURKISH, key), "missing Turkish text: " + key);
});

test("every server enumerated incident value has a Turkish presentation that is not the English token", () => {
  const vocabulary = new Set([
    ...listFrom(migration, 'CONSTRAINT "CK_m18_state" CHECK (state IN'),
    ...listFrom(migration, 'CONSTRAINT "CK_m18_action_state" CHECK(state IN'),
    ...listFrom(migration, 'CONSTRAINT "CK_m18_event_kind" CHECK (kind IN'),
    ...listFrom(migration, 'CONSTRAINT "CK_m18_evidence_kind" CHECK(kind IN'),
    ...listFrom(migration, 'CONSTRAINT "CK_m18_evidence_status" CHECK(source_status IN')
  ]);
  assert.ok(vocabulary.size >= 25);
  for (const value of vocabulary) {
    assert.ok(Object.hasOwn(TURKISH, value), "no Turkish text for server value " + value);
    assert.notEqual(translate(value, "tr-TR"), value, "Turkish text is the English token for " + value);
  }
});

test("the incident workspace carries no untranslated literal text or accessibility labels", () => {
  const markup = workspace.slice(workspace.indexOf("<section"));
  const literal = [...markup.matchAll(/>([^<>{}\n]*[A-Za-z]{3,}[^<>{}\n]*)</g)].map(m => m[1].trim());
  assert.deepEqual(literal.filter(Boolean), []);
  for (const m of workspace.matchAll(/\b(aria-label|placeholder|title|alt)="([^"]+)"/g))
    assert.fail("untranslated " + m[1] + " literal: " + m[2]);
  assert.doesNotMatch(workspace, /dangerouslySetInnerHTML/);
  assert.doesNotMatch(workspace, /\bstyle=\{/);
});

test("retries reuse an operation key only for the identical command at the identical version", () => {
  assert.match(workspace, /operationKeys\.current\.get\(fingerprint\) \?\? crypto\.randomUUID\(\)/);
  assert.match(workspace, /JSON\.stringify\(\[name,summary\.id,summary\.version,payload\]\)/);
  assert.match(workspace, /if\(definitive\(cause\)\)operationKeys\.current\.delete\(fingerprint\)/);
  const client = read("web/src/incidents/incidentClient.ts");
  assert.doesNotMatch(client, /randomUUID/);
});
