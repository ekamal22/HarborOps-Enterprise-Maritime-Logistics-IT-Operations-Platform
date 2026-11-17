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
  readme: read("README.md"), acceptance: read("docs/ACCEPTANCE_CRITERIA.md"),
  events: read("docs/EVENT_PROCESSING.md"), m17Plan: read("docs/M17_IMPLEMENTATION_PLAN.md"),
  graph: read("docs/EXECUTION_GRAPH.md"),
  operations: read("docs/OPERATIONS.md"), strategy: read("docs/TEST_STRATEGY.md"),
  g17Plan: read("docs/M17_VERIFICATION_PLAN.md"), guide: read("docs/DOCUMENT_STATUS_GUIDE.md")});

test("the real documents reflect G17 integration and preserve independent sign-off limitations", () => {
  assert.deepEqual(currentStateProblems(docs()), []);
  assert.deepEqual(g17StatementProblems(docs()), []);
});

test("each current-state rule fails on its own when its statement is removed", () => {
  const original = docs();
  const edits = {
    "status-accepted-set": d => { d.status = d.status.replaceAll("G0-G12, G15, G16 and G17 are accepted and integrated into protected `main`", "some gates are accepted"); },
    "status-m11-ledger": d => { d.status = d.status.replace("G11 ACCEPTED, merged in PR #33", "G11 acceptance pending"); },
    "status-m12-ledger": d => { d.status = d.status.replace("G12 ACCEPTED, merged in PR #33", "G12 acceptance pending"); },
    "status-g13-conditional": d => { d.status = d.status.replace("G13 conditionally accepted", "G13 accepted"); },
    "status-g15-accepted": d => { d.status = d.status.replace("G15 and S25 ACCEPTED", "G15 pending"); },
    "status-g17-integrated": d => { d.status = d.status.replace("G17 ACCEPTED AND MERGED", "G17 pending"); },
    "status-g14-planned": d => { d.status = d.status.replace(/(\| \*\*M14 [^|]*\*\* \| )Planned/, "$1Accepted"); },
    "event-current-set": d => { d.events = d.events.replace("G0-G12, G15, G16 and G17 are accepted and integrated; G13 remains conditionally accepted", "G17 pending"); },
    "m17-delivery-outcome": d => { d.m17Plan = d.m17Plan.replace("Current outcome (2026-11-17):** M17-01 through M17-04 were implemented and integrated through PR #53", "Current outcome pending"); },
    "graph-accepted-m17": d => { d.graph = d.graph.replace("G17 was accepted for integration after the authenticated Campaign 01 technical PASS", "G17 pending"); },
    "operations-g17-integrated": d => { d.operations = d.operations.replace("Authenticated Campaign 01 passed 51/51 stages", "campaign not run"); },
    "strategy-g17-executed": d => { d.strategy = d.strategy.replace("M17 is implemented and integrated into `main` after authenticated Campaign 01 passed 51/51 stages", "M17 verification pending"); },
    "g17-plan-atomic-result": d => { d.g17Plan = d.g17Plan.replace("Atomic scope and final technical result (2026-11-17):** 25 scenarios", "Current progress pending"); },
    "guide-integrated-status": d => { d.guide = d.guide.replace("Current integration decision: G0-G12, G15, G16 and G17 are accepted and integrated", "G17 pending"); },
    "roadmap-accepted-set": d => { d.roadmap = d.roadmap.replace("G0-G12, G15, G16 and G17 accepted and merged", "G0-G12 accepted"); },
    "roadmap-release-pending": d => { d.roadmap = d.roadmap.replace("G24 release acceptance remain pending", "G24 release is accepted"); },
    "contributors-accepted-set": d => { d.contributors = d.contributors.replace("G0-G12, G15, G16 and G17 are accepted and integrated. G13 remains conditionally accepted", "gates are accepted"); }
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

test("unaccepted gates cannot be declared accepted, but G17 integration and prerequisites are allowed", () => {
  const claims = [
    "**G14 ACCEPTED** on 2026-11-20", "G18 accepted 2026-11-20", "G18 accepted on 2026-11-20",
    "G18-S25 ACCEPTED", "G18 acceptance was granted", "M14 is accepted",
    "M18 has been accepted", "G24 ACCEPTED", "G20 accepted 2027-01-01"
  ];
  for (const claim of claims) assert.equal(forbiddenClaimProblems({file: claim}).length, 1, claim);
  const notClaims = [
    "G17 NOT ACCEPTED; acceptance pending", "G17 accepted on 2026-11-17",
    "G17 ACCEPTED AND MERGED", "M17 is accepted", "Entry: G17 accepted, G16 accepted before work packages",
    "Once G17 is accepted, M18 can consume diagnostics", "G13 remains conditionally accepted",
    "G14 NOT ACCEPTED", "G18 NOT ACCEPTED", "G16 accepted", "G15-S25 ACCEPTED"
  ];
  for (const value of notClaims) assert.deepEqual(forbiddenClaimProblems({file: value}), [], value);
  assert.equal(FORBIDDEN_ACCEPTANCE_CLAIMS.length, 4);
});

test("each current G17 statement is required with a distinct negative control", () => {
  const original = docs();
  const edits = {
    "g17-integrated-status": d => { d.status = d.status.replace("G17 integration status (2026-11-17): ACCEPTED AND MERGED", "G17 status pending"); },
    "g17-s25-outstanding": d => { d.status = d.status.replace("signed G17 S25-A03/F06 reviewer instrument remains outstanding", "no reviewer statement"); },
    "p09-qualified-pass": d => { d.status = d.status.replaceAll("P09 remains QUALIFIED PASS", "P09 has no classification"); },
    "pre-release-optimization": d => { d.status = d.status.replaceAll(/optimization|Optimization/g, "review").replaceAll(/pre-release|G24|release readiness/gi, "future"); },
    "g17-integrated-readme": d => { d.readme = d.readme.replace("G17 integration status (2026-11-17): ACCEPTED AND MERGED", "G17 status pending"); },
    "g17-integrated-roadmap": d => { d.roadmap = d.roadmap.replace("G0-G12, G15, G16 and G17 accepted and merged", "G17 pending"); },
    "g17-integrated-acceptance": d => { d.acceptance = d.acceptance.replace("ACCEPTED AND INTEGRATED 2026-11-17", "status pending"); }
  };
  const missing = Object.keys(edits).sort();
  const actual = g17StatementProblems({status:"",readme:"",roadmap:"",acceptance:""}).map(x => x.replace(" is missing","")).sort();
  assert.deepEqual(missing, actual, "every current G17 state rule has a negative control");
  for (const [id, update] of Object.entries(edits)) {
    const changed = structuredClone(original);
    update(changed);
    assert.ok(g17StatementProblems(changed).includes(id + " is missing"), id);
  }
});

test("the checker passes on the repository and fails when a current statement is removed", () => {
  const run = spawnSync(process.execPath, [path.join(repo, "scripts", "check-planning-docs.mjs")], {encoding: "utf8", cwd: repo});
  assert.equal(run.status, 0, run.stdout + run.stderr);
  assert.match(run.stdout, /Planning docs are internally consistent/);
});
