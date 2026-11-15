// Builds the final mutation result files and the individual survivor classification from the recorded campaigns.
import fs from "node:fs";
import path from "node:path";
const E = "E:/HarborOps-Verification/g17-s25-audit/";
const read = f => fs.readFileSync(f, "utf8").split("\n").filter(Boolean).map(l => JSON.parse(l));
const base = p => path.basename(p);
const key = (t, line, mut) => base(t) + "|" + line + "|" + mut.trim();

// ---- D: final strict campaign (517 other candidates + 288 manifest candidates)
const D = [];
for (const f of fs.readdirSync(E + "mutation-final-record").filter(f => f.endsWith(".jsonl")))
  D.push(...read(E + "mutation-final-record/" + f));
const manifestFirst = [];
for (let r = 0; r < 4; r++) manifestFirst.push(...read(E + "mutation-full-manifest/shard" + r + ".jsonl"));
const retest = new Map();
for (let r = 0; r < 4; r++)
  for (const x of read(E + "mutation-full-manifest-retest/shard" + r + ".jsonl")) retest.set(x.id, x);
// c200 and c204 were killed by the inclusive-budget test, added after the retest, and verified by direct runs.
const supplemental = new Map([
  ["verify-g17-manifest.mjs@c200", "killed by the inclusive-budget test (direct run, exit 1)"],
  ["verify-g17-manifest.mjs@c204", "killed by the inclusive-budget test (direct run, exit 1)"]]);
for (const r of manifestFirst) {
  const re = retest.get(r.id);
  const row = {...r, first_pass_status: r.status};
  if (re) {row.status = re.status; row.retest = true; row.killed_by = re.killed_by?.length ? re.killed_by : r.killed_by;}
  if (supplemental.has(r.id)) {row.status = "KILLED"; row.supplemental_direct_run = supplemental.get(r.id);}
  D.push(row);
}
// A file-level failure with zero executed tests means the mutated module throws on import: a separate kill category.
for (const r of D)
  if (r.status === "INVALID_INFRASTRUCTURE" && r.exit_status === 1 && r.tests_passed === 0 && r.tests_failed > 0 && !r.timed_out && !r.signal) {
    r.first_pass_status = r.status; r.status = "KILLED_LOAD_ERROR";
    r.reclassified = "module throws its usage error on import; evidence survivor-evidence/reconcile-main-guard-load-failure.txt";
  }
const count = (rows, s) => rows.filter(r => r.status === s).length;
const summary = {final_candidates: D.length, KILLED: count(D, "KILLED"), KILLED_LOAD_ERROR: count(D, "KILLED_LOAD_ERROR"),
  SURVIVED: count(D, "SURVIVED"), INVALID_INFRASTRUCTURE: count(D, "INVALID_INFRASTRUCTURE")};
console.log("FINAL", JSON.stringify(summary));
const byTarget = {};
for (const r of D) {
  const t = base(r.target);
  byTarget[t] ??= {candidates: 0, killed: 0, killed_load_error: 0, survived: 0, invalid: 0};
  const b = byTarget[t]; b.candidates++;
  if (r.status === "KILLED") b.killed++; else if (r.status === "KILLED_LOAD_ERROR") b.killed_load_error++;
  else if (r.status === "SURVIVED") b.survived++; else b.invalid++;
}
console.table(byTarget);

// ---- earlier campaigns
const A = [];
{
  const txt = fs.readFileSync(E + "preserved-20261115/original-mutation/mutation-final.txt", "utf8").split("\n");
  let target = null;
  for (const l of txt) {
    const h = l.match(/^\{"target":"([^"]+)"/);
    if (h) {target = h[1]; continue;}
    const s = l.match(/^SURVIVOR L(\d+): (.*)$/);
    if (s && target) {
      const cut = s[2].indexOf("  =>  ");
      A.push({campaign: "A", target, line: Number(s[1]), original: s[2].slice(0, cut).trim(), mutated: s[2].slice(cut + 6).trim()});
    }
  }
}
const survivorsOf = (dir, campaign, accept) => {
  const out = [];
  for (const f of fs.readdirSync(E + dir).filter(f => f.endsWith(".jsonl") && accept(f)))
    for (const r of read(E + dir + "/" + f))
      if (r.status === "survived") out.push({campaign, target: r.target, line: r.line, original: r.original, mutated: r.mutated, operator: r.operator});
  return out;
};
const B = survivorsOf("mutation-orig-record", "B", f => f !== "verify-g17-manifest.jsonl");
const M = survivorsOf("mutation-orig-record", "M", f => f === "verify-g17-manifest.jsonl");
const C = survivorsOf("mutation-post-record", "C", () => true);
const Dsurv = D.filter(r => r.first_pass_status === "SURVIVED" || r.status === "SURVIVED")
  .map(r => ({campaign: "D", target: r.target, line: r.line, original: r.original, mutated: r.mutated, operator: r.operator}));
console.log("survivors per campaign: A", A.length, "B", B.length, "M", M.length, "C", C.length, "D(first pass or final)", Dsurv.length);

const Dindex = new Map(D.map(r => [key(r.target, r.line, r.mutated), r]));
const all = new Map();
for (const s of [...A, ...B, ...M, ...C, ...Dsurv]) {
  const k = key(s.target, s.line, s.mutated);
  const e = all.get(k) ?? {key: k, target: base(s.target), line: s.line, original: s.original, mutated: s.mutated, operator: s.operator ?? null, campaigns: new Set()};
  e.campaigns.add(s.campaign);
  if (!e.operator && s.operator) e.operator = s.operator;
  all.set(k, e);
}
const RESIDUAL = JSON.parse(fs.readFileSync(E + "survivor-evidence/residual-classification.json", "utf8"));
const rows = [], unmatched = [];
for (const e of all.values()) {
  const d = Dindex.get(e.key);
  if (!d) {unmatched.push(e); continue;}
  let cls, evidence;
  if (d.status === "KILLED" || d.status === "KILLED_LOAD_ERROR") {
    cls = "MATERIAL_CORRECTED";
    evidence = {final_status: d.status, killed_by: d.killed_by ?? [], note: d.supplemental_direct_run ?? d.reclassified ?? null};
  } else if (d.status === "SURVIVED") {
    const rk = base(d.target) + ":L" + d.line + ":" + d.operator;
    const rc = RESIDUAL[rk];
    if (!rc) {cls = "UNCLASSIFIED"; evidence = {residual_key: rk};} else {cls = rc.class; evidence = rc;}
  } else {cls = "UNRESOLVED_INVALID_RUN"; evidence = {final_status: d.status};}
  rows.push({id: d.id, target: e.target, line: e.line, operator: e.operator ?? d.operator, original: e.original, mutated: e.mutated,
    seen_surviving_in: [...e.campaigns].sort(), final_status: d.status, classification: cls, evidence});
}
rows.sort((a, b) => a.target.localeCompare(b.target) || a.line - b.line || a.mutated.localeCompare(b.mutated));
const tally = {};
for (const r of rows) tally[r.classification] = (tally[r.classification] || 0) + 1;
console.log("distinct survivors", rows.length, JSON.stringify(tally), "unmatched", unmatched.length);
for (const u of unmatched) console.log("UNMATCHED", u.key, [...u.campaigns]);
for (const u of unmatched) {
  if (u.target === "verify-g17-manifest.mjs" && u.line === 161)
    rows.push({id: "A-manifest-L161", target: u.target, line: 161, operator: "&&>||", original: u.original, mutated: u.mutated,
      seen_surviving_in: [...u.campaigns], final_status: "NOT_APPLICABLE", classification: "IDENTITY_MUTANT_CONTAMINATED_BASELINE",
      evidence: {reason: "The campaign A worktree carried a leaked mutation: its line 161 read with the conjunction while every committed version since ee2f9133 has the disjunction. The reported mutant therefore reproduced the committed source byte for byte, so it could not be killed. Its denominator (289 candidates, 119 of 120 killed) is retired; the sound manifest figures are the strict exhaustive campaign.",
        source: "mutation-final.txt SURVIVOR L161; git blame L161 = ee2f9133"}});
}
for (const k of Object.keys(tally)) delete tally[k];
for (const r of rows) tally[r.classification] = (tally[r.classification] || 0) + 1;
console.log("FINAL TALLY", rows.length, JSON.stringify(tally));
fs.writeFileSync(E + "final-d-results.jsonl", D.map(r => JSON.stringify(r)).join("\n") + "\n");
fs.writeFileSync(E + "survivors-classified.json", JSON.stringify({summary, by_target: byTarget,
  survivor_counts: {A_reported: A.length, B_original_state_rerun: B.length, M_manifest_at_c954edec: M.length, C_post_correction: C.length, D_first_pass: Dsurv.length},
  distinct_survivors: rows.length, tally, unmatched: unmatched.map(u => ({key: u.key, campaigns: [...u.campaigns]})), rows}, null, 1));
