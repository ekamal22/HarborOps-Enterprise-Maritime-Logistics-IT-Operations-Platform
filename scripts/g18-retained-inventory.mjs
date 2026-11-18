// Read-only inventory of retained Docker resources and historical snapshot candidates (G18 Q3 checkpoint).
// Usage: node scripts/g18-retained-inventory.mjs --out <file.json>
// It never mounts a volume and never connects to a retained database. It records metadata and a digest so the same
// command run before and after the campaign proves that no retained resource changed.
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { writeFileSync, readdirSync, existsSync } from "node:fs";

const out = process.argv[process.argv.indexOf("--out") + 1];
const docker = (...args) => execFileSync("docker", args, { encoding: "utf8", maxBuffer: 64 * 1024 * 1024 }).trim();
const lines = text => text.split("\n").map(s => s.trim()).filter(Boolean);

const volumes = lines(docker("volume", "ls", "--format", "{{.Name}}"));
const named = volumes.filter(v => !/^[0-9a-f]{64}$/.test(v));
const candidates = named.filter(v => /harborops|ho-g1|ho-verify|oldcopy|snapshot|backup|s1scratch/i.test(v));
const inspected = candidates.map(name => {
  const info = JSON.parse(docker("volume", "inspect", name))[0];
  return { name, createdAt: info.CreatedAt, labels: info.Labels, driver: info.Driver, mountpoint: info.Mountpoint, scope: info.Scope };
});
const containers = lines(docker("ps", "-a", "--format", "{{.ID}}")).map(id => {
  const c = JSON.parse(docker("inspect", id))[0];
  return {
    name: c.Name, state: c.State.Status, startedAt: c.State.StartedAt, finishedAt: c.State.FinishedAt, restartCount: c.RestartCount,
    project: c.Config.Labels?.["com.docker.compose.project"] ?? null,
    volumes: c.Mounts.filter(m => m.Type === "volume").map(m => ({ name: m.Name, mode: m.RW ? "rw" : "ro" }))
  };
}).filter(c => !c.name.includes("ho-verify-g18") && !c.name.includes("ho-verify-mut")).sort((a, b) => a.name.localeCompare(b.name));
const usedBy = Object.fromEntries(candidates.map(v => [v, containers.filter(c => c.volumes.some(m => m.name === v)).map(c => `${c.name}:${c.state}`)]));
const archives = existsSync("E:/HarborOps-Verification/archives") ? readdirSync("E:/HarborOps-Verification/archives").sort() : [];
const dumps = existsSync("E:/HarborOps-Verification/dumps") ? readdirSync("E:/HarborOps-Verification/dumps").sort() : [];
const body = { volumes: inspected, usedBy, containers, evidenceArchives: archives, dumps };
const digest = createHash("sha256").update(JSON.stringify(body)).digest("hex");
writeFileSync(out, JSON.stringify({ generated_at_utc: new Date().toISOString(), digest, ...body }, null, 2));
console.log(digest);
