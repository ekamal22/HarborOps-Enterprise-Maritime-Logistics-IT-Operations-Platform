import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {spawnSync} from "node:child_process";
import {fileURLToPath} from "node:url";
import {CURRENT_STATE_RULES, FORBIDDEN_ACCEPTANCE_CLAIMS, currentStateProblems, forbiddenClaimProblems, g17StatementProblems}
  from "./planning-current-state.mjs";

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const read = relative => fs.readFileSync(path.join(repo, relative), "utf8");
const docs = () => ({status: read("docs/STATUS.md"), roadmap: read("docs/ROADMAP.md"), contributors: read("docs/CONTRIBUTING.md"),
  readme: read("README.md"), acceptance: read("docs/ACCEPTANCE_CRITERIA.md")});

test("the real documents state the current accepted-gate set and keep G17 not accepted", () => {
  assert.deepEqual(currentStateProblems(docs()), []);
  assert.deepEqual(g17StatementProblems(docs()), []);
});

test("each current-state rule fails on its own when its statement is removed", () => {
  const original = docs();
  const edits = {
    "status-accepted-set": d => { d.status = d.status.replaceAll("M0-M12/G0-G12 and M16/G16 are accepted and integrated", "some gates are accepted"); },
    "status-m11-ledger": d => { d.status = d.status.replace("G11 ACCEPTED, merged in PR #33", "G11 acceptance pending"); },
    "status-m12-ledger": d => { d.status = d.status.replace("G12 ACCEPTED, merged in PR #33", "G12 acceptance pending"); },
    "status-g13-conditional": d => { d.status = d.status.replace("G13 conditionally accepted", "G13 accepted"); },
    "status-g15-accepted": d => { d.status = d.status.replace("G15 and S25 ACCEPTED", "G15 pending"); },
    "status-g14-planned": d => { d.status = d.status.replace(/(\| \*\*M14 [^|]*\*\* \| )Planned/, "$1Accepted"); },
    "roadmap-accepted-set": d => { d.roadmap = d.roadmap.replace("G0-G12, G15 and G16 accepted and merged", "G0-G12 accepted"); },
    "roadmap-release-pending": d => { d.roadmap = d.roadmap.replace("G24 release acceptance remain pending", "G24 release is accepted"); },
    "contributors-accepted-set": d => { d.contributors = d.contributors.replace("G0-G12, G15 and G16 are accepted and integrated. G13 remains conditionally accepted", "gates are accepted"); }
  };
  assert.deepEqual(Object.keys(edits).sort(), CURRENT_STATE_RULES.map(r => r.id).sort(), "every rule has a negative control");
  for (const [id, edit] of Object.entries(edits)) {
    const changed = structuredClone(original);
    edit(changed);
    const problems = currentStateProblems(changed);
    assert.ok(problems.some(p => p.startsWith(id + ":")), `${id}: ${JSON.stringify(problems)}`);
    assert.equal(problems.length, 1, `${id} fails alone`);
  }
});

test("the stale phrases that caused the four historical failures are not required any more", () => {
  const checker = read("scripts/check-planning-docs.mjs");
  assert.ok(!checker.includes('status.includes("G0-G12 and G16 are accepted")'));
  assert.ok(!checker.includes('"G13-G15 and G17-G24 remain open"'));
  assert.ok(!checker.includes('["Contributors",contributors,"G0-G12 and G16 are accepted"]'));
});

test("a later gate stated as accepted is rejected, while prerequisites and quoted denials are not claims", () => {
  const claims = [
    "**G17 ACCEPTED** on 2026-11-20", "G17 accepted 2026-11-20", "G17 accepted on 2026-11-20", "G17-S25 ACCEPTED",
    "G18 acceptance was granted", "M17 is accepted", "M18 has been accepted", "G24 ACCEPTED", "G20 accepted 2027-01-01"
  ];
  for (const claim of claims) assert.equal(forbiddenClaimProblems({file: claim}).length, 1, claim);
  const notClaims = [
    "G17 NOT ACCEPTED; acceptance pending", "G17 not accepted", "Entry: G17 accepted, G16 accepted before work packages",
    "Once G17 is accepted, M18 can consume diagnostics", "G13-G15 and G17-G24 remain open", "G16 accepted", "G15-S25 ACCEPTED", "M16 is accepted"
  ];
  for (const text of notClaims) assert.deepEqual(forbiddenClaimProblems({file: text}), [], text);
  assert.equal(FORBIDDEN_ACCEPTANCE_CLAIMS.length, 4);
});

test("the G17 statements are individually required", () => {
  const original = docs();
  const edits = {
    "g17-not-accepted": d => { d.status = d.status.replaceAll("G17 NOT ACCEPTED", "G17 pending"); },
    "p09-qualified-pass": d => { d.status = d.status.replaceAll("QUALIFIED PASS", "pass"); },
    "g17-not-accepted-readme": d => { d.readme = d.readme.replaceAll("G17 NOT ACCEPTED", "G17 pending"); },
    "g17-not-accepted-roadmap": d => { d.roadmap = d.roadmap.replaceAll("G17 NOT ACCEPTED", "G17 pending"); }
  };
  for (const [id, edit] of Object.entries(edits)) {
    const changed = structuredClone(original);
    edit(changed);
    assert.ok(g17StatementProblems(changed).includes(id + " is missing"), id);
  }
});

test("the checker passes on the repository and fails when a current statement is removed", () => {
  const run = spawnSync(process.execPath, [path.join(repo, "scripts", "check-planning-docs.mjs")], {encoding: "utf8", cwd: repo});
  assert.equal(run.status, 0, run.stdout + run.stderr);
  assert.match(run.stdout, /Planning docs are internally consistent/);
});
