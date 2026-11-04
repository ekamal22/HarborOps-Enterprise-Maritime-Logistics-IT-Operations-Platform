#!/usr/bin/env node
// Independently unpack and validate the final tarball before it becomes shareable.
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { existsSync, mkdtempSync, readFileSync, readdirSync, rmSync } from "node:fs";
import { basename, join, relative, resolve, sep } from "node:path";
import { spawnSync } from "node:child_process";

const [root, archivePath, manifestPath] = process.argv.slice(2);
assert.ok(root && /^(?:[Ee]:[\\/]|\/e\/)/.test(root), "E:-backed G8 root required");
assert.ok(archivePath && manifestPath, "Archive and manifest are required");
assert.ok(existsSync(archivePath) && existsSync(manifestPath), "Archive inputs missing");
// GNU tar reads "E:\path" as host:path and a backslash followed by t as an escape, so run it
// from the verification root with relative forward-slash paths only.
const rel = path => {
  const value = relative(root, path).split(sep).join("/");
  assert.ok(value && !value.startsWith("..") && !value.includes(":"), "Path must stay under the G8 root");
  return value;
};
const call = args => {
  const result = spawnSync("tar", args, { encoding: "utf8", timeout: 120000,
    maxBuffer: 16 * 1024 * 1024, cwd: root });
  assert.equal(result.status, 0, "Unable to inspect G8 archive");
  return result.stdout;
};
const names = call(["-tzf", rel(archivePath)]).split("\n").filter(Boolean);
assert.ok(names.length > 0, "Empty evidence archive");
for (const name of names)
  assert.ok(name === "evidence/" ||
    (name.startsWith("evidence/") && !name.includes("../") &&
     !name.startsWith("/") && !name.includes("\\")), "Unsafe tar member");
const scratch = mkdtempSync(join(root, "private", "g8-archive-check-"));
try {
  call(["-xzf", rel(archivePath), "-C", rel(scratch)]);
  const files = new Map();
  const walk = folder => {
    for (const entry of readdirSync(folder, { withFileTypes:true })) {
      const file = join(folder, entry.name);
      if (entry.isDirectory()) { walk(file); continue; }
      assert.ok(entry.isFile() && !entry.isSymbolicLink(), "Unexpected tar member type");
      const relative = file.slice(join(scratch, "evidence").length + 1).split(sep).join("/");
      files.set("./" + relative, createHash("sha256").update(readFileSync(file)).digest("hex"));
    }
  };
  walk(join(scratch, "evidence"));
  const lines = readFileSync(manifestPath, "utf8").split("\n").filter(Boolean);
  assert.equal(files.size, lines.length, "Manifest and archive file counts differ");
  for (const line of lines) {
    // sha256sum marks text mode with a space and binary mode (the Git Bash default) with an asterisk.
    const match = /^([a-f0-9]{64}) [ *](.+)$/.exec(line);
    assert.ok(match, "Invalid manifest entry");
    assert.equal(files.get(match[2]), match[1], "Archived evidence digest mismatch");
  }
  // Check the bytes of the *extracted* archive, including binary outputs.
  const secret = name => {
    const file = join(root, "private", name);
    return existsSync(file) ? readFileSync(file, "utf8").trim() : "";
  };
  const canaries = [secret("source-bearer.txt"), secret("test-user-password.txt")]
    .filter(Boolean);
  const safety = folder => {
    for (const entry of readdirSync(folder, { withFileTypes:true })) {
      const file = join(folder, entry.name);
      if (entry.isDirectory()) { safety(file); continue; }
      const bytes = readFileSync(file);
      for (const value of canaries)
        assert.ok(!bytes.includes(Buffer.from(value)), "Private credential found in archive");
      const text = bytes.toString("utf8");
      assert.ok(!/hop_v1_[0-9a-f]{32}_[A-Za-z0-9_-]{43}/.test(text),
        "Machine bearer found in archive");
      assert.ok(!/"sourceEventId"\s*:\s*"[^"]+"/.test(text),
        "Raw tracking body found in archive");
    }
  };
  safety(join(scratch, "evidence"));
  console.log("G8 archive and manifest integrity PASS:", files.size, "files");
} finally {
  rmSync(scratch, { recursive: true, force: true });
}
