#!/usr/bin/env node
// Usage:
//   check-g9-evidence-safety.mjs evidence <evidence-dir> [canary-file ...]
//   check-g9-evidence-safety.mjs archive <scratch-dir> <archive.tar.gz> <manifest.sha256> [canary-file ...]
// Canary files hold disposable secrets generated for this run. Their values must never appear in
// evidence. The archive mode also proves the manifest matches the archive contents byte for byte.
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";

const [mode, ...rest] = process.argv.slice(2);
const patterns = [
    [/hop_v1_[0-9a-f]{32}_[A-Za-z0-9_-]{43}/, "integration bearer token"],
    [/Password=[^;"\s]{1,}/i, "connection string password"],
    [/Authorization:\s*Bearer\s+[A-Za-z0-9._~+/=-]{16,}/i, "authorization header"],
    [/-----BEGIN [A-Z ]*PRIVATE KEY-----/, "private key block"],
    [/\.AspNetCore\.(?:Identity|Cookies|Antiforgery)[^\s=]*=[A-Za-z0-9_%-]{20,}/, "session or antiforgery cookie value"],
    [/Set-Cookie:/i, "raw cookie header"]
];

function* walk(dir) {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        const full = path.join(dir, entry.name);
        if (entry.isDirectory()) yield* walk(full);
        else if (entry.isFile()) yield full;
    }
}
function canaries(files) {
    return files.flatMap(file => {
        const value = fs.readFileSync(file, "utf8").trim();
        assert.ok(value.length >= 12, "Canary value too short to be meaningful: " + path.basename(file));
        return [value];
    });
}
function scan(dir, secretValues) {
    const findings = [];
    let files = 0;
    for (const file of walk(dir)) {
        files++;
        const text = fs.readFileSync(file).toString("latin1");
        for (const [pattern, label] of patterns) if (pattern.test(text)) findings.push(`${label}: ${path.relative(dir, file)}`);
        for (const value of secretValues) if (text.includes(value)) findings.push(`disposable secret value present: ${path.relative(dir, file)}`);
    }
    return { files, findings };
}
const sha256 = file => createHash("sha256").update(fs.readFileSync(file)).digest("hex");

if (mode === "evidence") {
    const [dir, ...canaryFiles] = rest;
    assert.ok(dir && fs.existsSync(dir), "Evidence directory required");
    const { files, findings } = scan(dir, canaries(canaryFiles));
    console.log(`Scanned ${files} evidence files with ${canaryFiles.length} canary values; findings: ${findings.length}`);
    for (const finding of findings) console.error("FINDING " + finding);
    process.exit(findings.length === 0 ? 0 : 1);
} else if (mode === "archive") {
    const [scratch, archive, manifest, ...canaryFiles] = rest;
    assert.ok(scratch && archive && manifest && fs.existsSync(archive) && fs.existsSync(manifest), "Archive and manifest required");
    assert.ok(/^(?:[Ee]:[\\/]|\/e\/)/.test(scratch), "Verification scratch must be on E:");
    assert.ok(!fs.existsSync(scratch), "Archive verification directory must be new: " + scratch);
    fs.mkdirSync(scratch, { recursive: true });
    // GNU tar reads "E:\..." as host:path, so run it from the scratch directory with relative paths.
    const relativeArchive = path.relative(scratch, archive).split(path.sep).join("/");
    const extract = spawnSync("tar", ["-xzf", relativeArchive, "-C", "."], { cwd: scratch, encoding: "utf8" });
    assert.equal(extract.status, 0, "tar extraction failed: " + (extract.stderr || "").slice(-300));
    const base = path.join(scratch, "evidence");
    assert.ok(fs.existsSync(base), "Archive must extract under an evidence/ prefix");
    const listed = new Map();
    for (const line of fs.readFileSync(manifest, "utf8").split("\n").filter(Boolean)) {
        const match = /^([0-9a-f]{64}) [ *](.+)$/.exec(line);
        assert.ok(match, "Malformed manifest line");
        listed.set(match[2].replace(/^\.\//, ""), match[1]);
    }
    const actual = new Map([...walk(base)].map(file => [path.relative(base, file).split(path.sep).join("/"), sha256(file)]));
    const problems = [];
    for (const [file, hash] of listed) {
        if (!actual.has(file)) problems.push("manifest entry missing from archive: " + file);
        else if (actual.get(file) !== hash) problems.push("hash mismatch: " + file);
    }
    for (const file of actual.keys()) if (!listed.has(file)) problems.push("archive file not in manifest: " + file);
    const { files, findings } = scan(base, canaries(canaryFiles));
    console.log(`Archive holds ${actual.size} files; manifest lists ${listed.size}; secret scan covered ${files} files`);
    for (const item of [...problems, ...findings]) console.error("PROBLEM " + item);
    process.exit(problems.length === 0 && findings.length === 0 ? 0 : 1);
} else {
    console.error("usage: evidence <dir> [canaries...] | archive <scratch> <archive> <manifest> [canaries...]");
    process.exit(2);
}
