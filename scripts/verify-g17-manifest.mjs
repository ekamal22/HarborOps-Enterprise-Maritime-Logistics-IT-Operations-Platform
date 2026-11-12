import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";

function crawl(root, prefix = "") {
  const files = [];
  for (const entry of fs.readdirSync(root, { withFileTypes: true })) {
    const name = path.posix.join(prefix, entry.name);
    if (name === "manifest.json") continue;
    if (entry.isSymbolicLink()) throw Error("Unexpected symlink " + name);
    if (entry.isDirectory()) {
      files.push(...crawl(path.join(root, entry.name), name));
    } else if (entry.isFile()) {
      const bytes = fs.readFileSync(path.join(root, entry.name));
      files.push({
        path: name, bytes: bytes.length,
        sha256: crypto.createHash("sha256").update(bytes).digest("hex")
      });
    } else throw Error("Unsupported evidence object " + name);
  }
  return files.sort((a,b) => a.path.localeCompare(b.path));
}

export function verifyManifest(manifestPath, expectedSha) {
  const root = path.dirname(manifestPath);
  const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
  if (manifest.schema_version !== 2 || manifest.gate !== "G17" ||
      manifest.gate_acceptance !== "NOT_ACCEPTED" ||
      !/^[a-f0-9]{40}$/.test(manifest.source_sha)) {
    throw Error("Invalid source-locked G17 candidate manifest header");
  }
  if (expectedSha && manifest.source_sha !== expectedSha)
    throw Error("Wrong source SHA in G17 manifest");
  if (!Array.isArray(manifest.files)) throw Error("Missing file index");
  const actual = crawl(root);
  if (manifest.files.length !== actual.length)
    throw Error("Manifest coverage mismatch, extra or missing files");
  for (let i = 0; i < actual.length; i++) {
    const claimed = manifest.files[i], observed = actual[i];
    if (claimed.path !== observed.path || claimed.sha256 !== observed.sha256 ||
        claimed.bytes !== observed.bytes) throw Error("Evidence tampered or unlisted: " + observed.path);
  }
  if (manifest.assertion_disposition !== "NOT_REVIEWED")
    throw Error("Unverified evidence may not claim reviewed assertions");
  return {source_sha: manifest.source_sha,files:actual.length};
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const manifest = process.argv[2], sha = process.argv[3];
  if (!manifest || !sha || !/^[a-f0-9]{40}$/.test(sha))
    throw Error("Usage: node scripts/verify-g17-manifest.mjs <path> <40-character SHA>");
  console.log("Verified G17 evidence index:", verifyManifest(manifest, sha));
}
