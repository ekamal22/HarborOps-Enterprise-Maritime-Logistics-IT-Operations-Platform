#!/usr/bin/env node
// Provision a synthetic machine source in the disposable G7 database only.
import assert from "node:assert/strict";
import { createHash, randomBytes, randomUUID } from "node:crypto";
import { execFileSync } from "node:child_process";
import { existsSync, mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";

const root = process.env.HARBOROPS_VERIFICATION_ROOT;
const project = "ho-verify-g7";
assert.match(root || "", /^(?:[Ee]:[\\/]|\/e\/)/, "E: verification root required");
const privateFile = join(root, "private", "source-bearer.txt");
assert.ok(!existsSync(privateFile), "Existing private fixture found; refusing overwrite");
const call = sql => execFileSync("docker",
  ["compose", "-p", project, "exec", "-T", "postgres",
    "psql", "-U", "harborops", "-d", "harborops", "-At", "-c", sql],
  { encoding:"utf8", timeout:30_000, maxBuffer:1024 * 1024 }).trim();
const organizationId = call("SELECT id FROM organizations WHERE slug='north-terminal'");
assert.match(organizationId, /^[0-9a-f-]{36}$/);
const sourceId = randomUUID();
const credentialId = randomUUID();
const secret = randomBytes(32);
const digest = createHash("sha256").update(secret).digest("hex");
const bearer = "hop_v1_" + credentialId.replaceAll("-", "") + "_" + secret.toString("base64url");
assert.equal(bearer.length, 83);
const sql = [
  "BEGIN;",
  "INSERT INTO integration_sources",
  "  (id,organization_id,source_code,display_name,status,capability,version,created_at_utc,updated_at_utc)",
  "VALUES ('" + sourceId + "','" + organizationId + "','g7_disposable',",
  "  'Synthetic G7 Source','Active','tracking_event_validate',1,now(),now());",
  "INSERT INTO integration_credentials",
  "  (id,organization_id,source_id,secret_digest,key_version,not_before_utc,expires_at_utc,created_at_utc)",
  "VALUES ('" + credentialId + "','" + organizationId + "','" + sourceId + "',",
  "  decode('" + digest + "','hex'),'v1',now(),now()+interval '90 days',now());",
  "COMMIT;"
].join("\n");
const result = call(sql);
assert.match(result, /COMMIT/, "Fixture transaction did not commit");
mkdirSync(dirname(privateFile), { recursive:true });
writeFileSync(privateFile, bearer, { flag:"wx", mode:0o600 });
secret.fill(0);
console.log("Disposable source provisioned: tenant-scoped digest, one active credential");
