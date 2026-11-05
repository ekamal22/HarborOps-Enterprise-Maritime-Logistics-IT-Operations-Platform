#!/usr/bin/env node
// Keeps raw failure artifacts out of the shareable archive without losing them. Playwright page
// snapshots and traces can contain the disposable account password typed into the login form.
// Usage: redact-g9-browser-artifacts.mjs <evidence-dir> <private-dir> <canary-file>
//  - text artifacts holding the canary are copied raw to <private-dir>/failure-artifacts-raw and
//    rewritten in place with the canary replaced by a marker;
//  - trace archives (compressed, so a text scan cannot see inside) are moved to the private directory
//    and replaced by a stub that records their SHA-256.
// evidence/redactions.json records every action with before and after hashes.
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";

const [evidence, privateDir, canaryFile] = process.argv.slice(2);
assert.ok(evidence && privateDir && canaryFile, "usage: <evidence-dir> <private-dir> <canary-file>");
const canary = fs.readFileSync(canaryFile, "utf8").trim();
assert.ok(canary.length >= 12, "canary too short");
const artifacts = path.join(evidence, "playwright", "artifacts");
const rawRoot = path.join(privateDir, "failure-artifacts-raw");
const sha = buffer => createHash("sha256").update(buffer).digest("hex");
const actions = [];

function* walk(dir) {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        const full = path.join(dir, entry.name);
        if (entry.isDirectory()) yield* walk(full);
        else if (entry.isFile()) yield full;
    }
}
if (fs.existsSync(artifacts)) {
    for (const file of [...walk(artifacts)]) {
        const relative = path.relative(evidence, file).split(path.sep).join("/");
        const raw = fs.readFileSync(file);
        const isTrace = file.endsWith(".zip");
        const isText = !isTrace && !/\.(png|jpe?g|webm|mp4)$/i.test(file);
        const hasCanary = isText && raw.toString("latin1").includes(canary);
        if (!isTrace && !hasCanary) continue;
        const keep = path.join(rawRoot, relative);
        fs.mkdirSync(path.dirname(keep), { recursive: true });
        fs.copyFileSync(file, keep);
        if (isTrace) {
            const stub = `Trace archive withheld from the shareable evidence because it records typed form input.\n` +
                `Original SHA-256: ${sha(raw)}\nOriginal bytes: ${raw.length}\nRaw copy retained under the private run directory.\n`;
            fs.rmSync(file);
            fs.writeFileSync(file + ".REMOVED.txt", stub);
            actions.push({ path: relative, action: "withheld", rawSha256: sha(raw), rawBytes: raw.length });
        } else {
            const redacted = raw.toString("utf8").split(canary).join("[REDACTED-DISPOSABLE-PASSWORD]");
            fs.writeFileSync(file, redacted);
            actions.push({ path: relative, action: "redacted", rawSha256: sha(raw), redactedSha256: sha(Buffer.from(redacted)),
                replacements: raw.toString("utf8").split(canary).length - 1 });
        }
    }
}
fs.writeFileSync(path.join(evidence, "redactions.json"), JSON.stringify({
    note: "Disposable per-run credentials were removed from failure artifacts. Raw files are kept privately and hashed here.",
    count: actions.length, actions }, null, 2));
console.log(`Failure artifact sanitization: ${actions.length} file(s) processed`);
