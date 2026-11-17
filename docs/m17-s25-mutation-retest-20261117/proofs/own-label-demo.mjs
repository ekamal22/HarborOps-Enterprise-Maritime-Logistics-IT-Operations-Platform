// Runs Get-G17OwnedResources from the ORIGINAL ownership script and from the M06+M07 mutants (both label comparisons
// disabled) against real docker decoys whose project label is a near miss of the project under test.
import {spawnSync} from "node:child_process"; import fs from "node:fs"; import os from "node:os"; import path from "node:path"; import crypto from "node:crypto";
const src = process.argv[2], outDir = process.argv[3];
const IMG = "alpine/socat:1.8.0.3", n = crypto.randomBytes(3).toString("hex"), P = `ho-verify-m17-lbl-${n}`;
const d = (...a) => { const r = spawnSync("docker", a, {encoding: "utf8"}); if (r.status !== 0) throw new Error(a.join(" ") + r.stderr); return r.stdout.trim(); };
const near = [P + " ", " " + P, P.toUpperCase(), P + "x", P.slice(0, -1), ""];
const made = {c: [], n: []};
try {
  near.forEach((v, i) => {
    // container names follow the exact <project>-<service>-<index> pattern so only the label can reject them
    const name = `${P}-svc-${i + 1}`; d("create", "--name", name, "--label", `com.docker.compose.project=${v}`, "--label", "com.docker.compose.service=svc", "--entrypoint", "sleep", IMG, "600"); made.c.push(name);
    const net = `${P}_net${i + 1}`; d("network", "create", "--label", `com.docker.compose.project=${v}`, "--label", "com.docker.compose.network=net" + (i + 1), net); made.n.push(net);
  });
  const own = `${P}-svc-0`; d("create", "--name", own, "--label", `com.docker.compose.project=${P}`, "--label", "com.docker.compose.service=svc", "--entrypoint", "sleep", IMG, "600"); made.c.push(own);
  const source = fs.readFileSync(path.join(src, "scripts/g17-docker-ownership.ps1"), "utf8");
  const variants = {original: source,
    "mutant M06+M07": source.replace('if ($labelProject -cne $Project) { $problems.Add("container $name carries project label', 'if ($false) { $problems.Add("container $name carries project label')
      .replace('if ($labelProject -cne $Project) { $problems.Add("network', 'if ($false) { $problems.Add("network')};
  const results = {};
  for (const [label, text] of Object.entries(variants)) {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "own-label-")); const file = path.join(dir, "o.ps1"); fs.writeFileSync(file, text);
    const run = spawnSync("pwsh", ["-NoProfile", "-Command", `. '${file}'; $r = Get-G17OwnedResources '${P}'; [pscustomobject]@{containers=@($r.Containers | % Name | sort); networks=@($r.Networks | % Name | sort); problems=@($r.Problems)} | ConvertTo-Json -Compress`], {encoding: "utf8"});
    results[label] = {exit: run.status, output: run.stdout.trim(), stderr: run.stderr.trim()};
  }
  const same = results.original.output === results["mutant M06+M07"].output;
  const report = {project: P, near_miss_labels: near, owned_container: `${P}-svc-0`, results, identical_output: same};
  fs.mkdirSync(outDir, {recursive: true}); fs.writeFileSync(path.join(outDir, "own-label-demo.json"), JSON.stringify(report, null, 2)); console.log(JSON.stringify(report, null, 2));
} finally { for (const c of made.c) spawnSync("docker", ["rm", "-f", c]); for (const x of made.n) spawnSync("docker", ["network", "rm", x]); }
