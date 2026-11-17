// Per-mutant retest on a disposable copy of the frozen source. usage: node retest.mjs <frozen-src> <out-dir> [id ...]
import fs from "node:fs"; import path from "node:path"; import crypto from "node:crypto"; import {spawnSync} from "node:child_process"; import {pathToFileURL} from "node:url";
const [src, out, ...only] = process.argv.slice(2);
const sha = b => crypto.createHash("sha256").update(b).digest("hex");
const BS = String.fromCharCode(92), nul = BS + "u0000", R = String.raw, BOM = BS + "uFEFF";
const node = process.execPath;
const proto = "docs/m17-s25-p09-controlled-protocol-20261115";
const T = (pattern, ...files) => [node, "--test", ...(pattern ? [`--test-name-pattern=${pattern}`] : []), ...files];
const own = "scripts/g17-docker-ownership.test.mjs";
const reconcileTests = ["scripts/g17-first-failure-ledger.test.mjs", "scripts/g17-reconcile-derivation.test.mjs", "scripts/reconcile-g17-assertions.test.mjs"];
const NEW = process.env.G17_PHASE === "new";
const BROAD = process.env.G17_PHASE === "frozen-broad";
const ownFrozen = "finished stack|running stack|partially initialised|ambiguous ownership|another project|project names|already exists|held address|runner preserves";
const skipProducer = ["--test-skip-pattern=corrected producer finalises", "--test-skip-pattern=stopping twice leaves", "--test-skip-pattern=every kind of tampering"];
const diagBroad = [node, "--test", ...skipProducer, "scripts/g17-diagnostic-hash-list.test.mjs", "scripts/g17-diagnostic-verifier-strict.test.mjs"];
const diagStrict = T(null, "scripts/g17-diagnostic-verifier-strict.test.mjs");
const ownAll = "finished stack|running stack|partially initialised|ambiguous ownership|another project|project names|already exists|held address|runner preserves|remain make the cleanup fail|address range taken|volume that docker created";
const M = [
 {id:"M01",family:"reconcile binding",name:"promtool binding bypassed",target:"scripts/reconcile-g17-assertions.mjs",
  from:`return alertCaseBound(original,w.case)?results.get(w.path+"${nul}*")??null:null;`,to:`return results.get(w.path+"${nul}*")??null;`,cmd:T(null,...reconcileTests)},
 {id:"M02",family:"reconcile binding",name:"manifest rejection binding bypassed",target:"scripts/reconcile-g17-assertions.mjs",
  from:`return manifestRejectionBound(node)?results.get(w.path+"${nul}*")??null:null;`,to:`return results.get(w.path+"${nul}*")??null;`,cmd:T(null,...reconcileTests)},
 {id:"M03",family:"evidence archive",name:"extracted files not compared by size",target:"scripts/g17-evidence-archive.mjs",
  from:"else if (found.bytes !== file.bytes) problems.push",to:"else if (false) problems.push",cmd:T(null,"scripts/g17-evidence-archive.test.mjs")},
 {id:"M04",family:"evidence archive",name:"extra extracted files tolerated",target:"scripts/g17-evidence-archive.mjs",
  from:"for (const name of actualMap.keys()) if (!expected.has(name)) problems.push",to:"for (const name of actualMap.keys()) if (false) problems.push",cmd:T(null,"scripts/g17-evidence-archive.test.mjs")},
 {id:"M05",family:"evidence archive",name:"unextracted files tolerated",target:"scripts/g17-evidence-archive.mjs",
  from:"if (!found) problems.push(`not extracted",to:"if (false) problems.push(`not extracted",cmd:T(null,"scripts/g17-evidence-archive.test.mjs")},
 {id:"M06",family:"docker ownership",name:"project label not compared (container)",target:"scripts/g17-docker-ownership.ps1",
  from:'if ($labelProject -cne $Project) { $problems.Add("container $name carries project label',to:'if ($false) { $problems.Add("container $name carries project label',cmd:T(NEW ? ownAll : BROAD ? ownFrozen : "ambiguous ownership",own)},
 {id:"M07",family:"docker ownership",name:"network label project not compared",target:"scripts/g17-docker-ownership.ps1",
  from:'if ($labelProject -cne $Project) { $problems.Add("network',to:'if ($false) { $problems.Add("network',cmd:T(NEW ? ownAll : BROAD ? ownFrozen : "ambiguous ownership",own)},
 {id:"M08",family:"docker ownership",name:"residual resources tolerated",target:"scripts/g17-docker-ownership.ps1",
  from:"if ($after.Containers.Count -ne 0 -or $after.Networks.Count -ne 0) { throw",to:"if ($false) { throw",cmd:T(NEW ? ownAll : BROAD ? ownFrozen : "finished stack is removed",own)},
 {id:"M09",family:"docker ownership",name:"address range release not proven",target:"scripts/g17-docker-ownership.ps1",
  from:"if ($held.Count -gt 0) { throw",to:"if ($false) { throw",cmd:T(NEW ? ownAll : BROAD ? ownFrozen : "finished stack is removed",own)},
 {id:"M10",family:"docker ownership",name:"volumes removed with containers",target:"scripts/g17-docker-ownership.ps1",
  from:"Invoke-G17Docker @('rm', $container.Id)",to:"Invoke-G17Docker @('rm', '-v', $container.Id)",cmd:T(NEW ? ownAll : BROAD ? ownFrozen : "finished stack is removed",own)},
 {id:"M11",family:"docker ownership",name:"existing evidence directory overwritten",target:"scripts/cleanup-g17-owned.ps1",
  from:"if (Test-Path -LiteralPath $evidencePath) { throw 'The cleanup evidence directory already exists. Never overwrite evidence' }",to:"",cmd:T(NEW ? ownAll : BROAD ? ownFrozen : "already exists",own)},
 {id:"M12",family:"diagnostic producer",name:"exit of children not awaited",target:`${proto}/diagnostics.ps1`,
  from:'try { Wait-Process -Id $id -Timeout 10 -ErrorAction Stop } catch [System.TimeoutException] { throw "Child $kind (process $id) did not exit within 10 seconds." } catch { }',to:"",cmd:(NEW||BROAD) ? T(null,"scripts/g17-diagnostic-producer-guard.test.mjs") : T("owned run stops only","scripts/g17-diagnostic-producer-guard.test.mjs")},
 {id:"M13",family:"diagnostic producer",name:"lingering child tolerated",target:`${proto}/diagnostics.ps1`,
  from:'if (Get-ProcessIdentity $id) { throw "Child $kind (process $id) is still running after the stop request." }',to:"",cmd:(NEW||BROAD) ? T(null,"scripts/g17-diagnostic-producer-guard.test.mjs") : T("owned run stops only","scripts/g17-diagnostic-producer-guard.test.mjs")},
 {id:"M14",occurrence:1,family:"diagnostic verifier",name:"stream names accepted",target:"scripts/g17-diagnostic-hash-list.mjs",
  from:R`[^\\/:<>"|?*\x00-\x1f\s]`,to:R`[^\\/<>"|?*\x00-\x1f\s]`,cmd:BROAD ? diagBroad : diagStrict},
 {id:"M14b",occurrence:2,family:"diagnostic verifier",name:"stream names accepted (second position)",target:"scripts/g17-diagnostic-hash-list.mjs",from:R`[^\\/:<>"|?*\x00-\x1f\s]`,to:R`[^\\/<>"|?*\x00-\x1f\s]`,cmd:BROAD ? diagBroad : diagStrict},
 {id:"M15",family:"diagnostic verifier",name:"bom not stripped",target:"scripts/g17-diagnostic-hash-list.mjs",
  from:".replace(/^" + BOM + "/, \"\")",to:"",cmd:BROAD ? diagBroad : diagStrict},
 {id:"M16",family:"witness binding",name:"marker may be non-comment text",target:"scripts/g17-witness-binding.mjs",
  from:R`/^\s*#/.test(line) && line.includes(marker)`,to:"line.includes(marker)",cmd:T(null,"scripts/g17-witness-binding.test.mjs")},
 {id:"M17",family:"witness binding",name:"block start not required",target:"scripts/g17-witness-binding.mjs",
  from:"if (start < 0) return null;",to:"",cmd:T(null,"scripts/g17-witness-binding.test.mjs")},
];
export {M};
if (import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
fs.mkdirSync(out, {recursive: true});
const run = (cwd, cmd, tag) => {
  const t0 = Date.now(); const r = spawnSync(cmd[0], cmd.slice(1), {cwd, encoding: "utf8", timeout: 900000, maxBuffer: 256 << 20});
  fs.writeFileSync(tag + ".out.txt", (r.stdout ?? "") + (r.stderr ? "\n--- stderr ---\n" + r.stderr : ""));
  const text = (r.stdout ?? "") + (r.stderr ?? "");
  const failing = [...text.matchAll(/^\s*not ok \d+ - (.*)$/gm)].map(x => x[1]);
  const errs = [...text.matchAll(/^\s+name: '([A-Za-z]+)'$/gm)].map(x => x[1]);
  const msgs = [...text.matchAll(/^\s+error: (.*)$/gm)].map(x => x[1].slice(0, 300));
  const count = k => Number((new RegExp("^# " + k + " (\\d+)", "m").exec(text) ?? [])[1] ?? NaN);
  return {exit: r.status, signal: r.signal, timed_out: r.error?.code === "ETIMEDOUT", seconds: Math.round((Date.now() - t0) / 1000),
    tests: count("tests"), pass: count("pass"), fail: count("fail"), failing_tests: failing, error_names: [...new Set(errs)], error_messages: msgs.slice(0, 6), output: path.basename(tag) + ".out.txt"};
};
for (const m of M) {
  if (only.length && !only.includes(m.id)) continue;
  const work = path.join(out, "work-" + m.id); fs.rmSync(work, {recursive: true, force: true}); fs.cpSync(src, work, {recursive: true});
  const target = path.join(work, m.target); const original = fs.readFileSync(target);
  const text = original.toString("utf8"); const matches = m.from === "" ? 0 : text.split(m.from).length - 1;
  const rec = {id: m.id, family: m.family, name: m.name, target: m.target, source_sha256: sha(original), command: m.cmd.map((x, i) => i === 0 ? "node" : x),
    mutation: {from: m.from, to: m.to, occurrences_in_source: matches}};
  const tag = path.join(out, m.id);
  rec.control_before = run(work, m.cmd, tag + ".control-before");
  if (matches !== 1 && !(m.occurrence && matches >= m.occurrence)) { rec.disposition = "PATTERN_NOT_FOUND_OR_AMBIGUOUS"; fs.writeFileSync(tag + ".json", JSON.stringify(rec, null, 2)); console.log(m.id, rec.disposition, matches); continue; }
  const mutated = m.occurrence ? (() => { const p = text.split(m.from); return p.slice(0, m.occurrence).join(m.from) + m.to + p.slice(m.occurrence).join(m.from); })() : text.replace(m.from, () => m.to); fs.writeFileSync(target, mutated);
  rec.mutated_sha256 = sha(Buffer.from(mutated));
  rec.mutated_run = run(work, m.cmd, tag + ".mutated");
  fs.writeFileSync(target, original); rec.restored_sha256_matches = sha(fs.readFileSync(target)) === rec.source_sha256;
  rec.control_after = run(work, m.cmd, tag + ".control-after");
  const c = x => x.exit === 0 && !x.signal && !x.timed_out;
  const mr = rec.mutated_run;
  rec.controls_pass = c(rec.control_before) && c(rec.control_after);
  rec.auto = !rec.controls_pass ? "VOID_CONTROL" : mr.exit === 0 ? "SURVIVED" : (mr.timed_out || mr.signal) ? "INFRASTRUCTURE_FAILURE" :
    (mr.fail > 0 && mr.error_names.every(n => n === "AssertionError")) ? "KILLED_CANDIDATE" : "INFRASTRUCTURE_OR_UNRELATED";
  fs.writeFileSync(tag + ".json", JSON.stringify(rec, null, 2)); console.log(m.id, m.name, "=>", rec.auto, "failing:", mr.failing_tests.length);
  fs.rmSync(work, {recursive: true, force: true});
}}
