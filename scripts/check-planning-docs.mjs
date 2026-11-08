import { existsSync, readFileSync, readdirSync } from "node:fs";
import { dirname, isAbsolute, join, normalize, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const get = path => readFileSync(join(root, path), "utf8");
const graph = JSON.parse(get("docs/execution-graph.json"));
const roadmap = get("docs/ROADMAP.md");
const prd = get("docs/PRD.md");
const status = get("docs/STATUS.md");
const acceptance = get("docs/ACCEPTANCE_CRITERIA.md");
const decisions = get("docs/DECISIONS.md");
const graphView = get("docs/EXECUTION_GRAPH.md");
const readme = get("README.md");
const storagePolicy = get("docs/LOCAL_STORAGE_POLICY.md");
const implementationPlan = get("docs/IMPLEMENTATION_PLAN.md");
const parallelPolicy = get("docs/PARALLEL_DELIVERY_POLICY.md");
const contributors = get("docs/CONTRIBUTING.md");
const m8Plan = get("docs/M8_VERIFICATION_PLAN.md");
const m8Status = get("docs/M8_IMPLEMENTATION_STATUS.md");
const secondRemediation = get("docs/M8_SECOND_REMEDIATION_STATUS.md");
const runbook = get("docs/M8_G8_VERIFICATION_RUNBOOK.md");
const m9Architecture = get("docs/M9_ARCHITECTURE_DESIGN.md");
const m9Plan = get("docs/M9_IMPLEMENTATION_PLAN.md");
const m9Contract = get("docs/M9_LOCAL_CONTRACT_PROPOSAL.md");
const g9Verification = get("docs/M9_VERIFICATION_PLAN.md");
const m9Status = get("docs/M9_PLANNING_STATUS.md");
const m9Implementation = get("docs/M9_IMPLEMENTATION_STATUS.md");
const m9Decision = get("docs/M9_LOCAL_CONTRACT_DECISION.md");
const g9Package = get("docs/M9_G9_ACCEPTANCE_PACKAGE.md");
const m9Browsers = get("docs/M9_BROWSER_SUPPLEMENT.md");
const m9Audit = get("docs/M9_CORRECTNESS_AUDIT_20261104.md");

function assert(ok, message) {
  if (!ok) throw Error(message);
}

assert(graph.schema_version === 2, "Expected version 2 execution graph");
const m11SecondAudit = get("docs/M11_SECOND_CORRECTNESS_AUDIT_20261107.md");
const m11ThirdAudit = get("docs/M11_THIRD_CORRECTNESS_AUDIT_20261108.md");
const m11FourthAudit = get("docs/M11_FOURTH_CORRECTNESS_AUDIT_20261108.md");
const m11FifthAudit = get("docs/M11_FIFTH_CORRECTNESS_AUDIT_20261108.md");
const m11SixthAudit = get("docs/M11_SIXTH_CORRECTNESS_AUDIT_20261108.md");
const m11Implementation = get("docs/M11_IMPLEMENTATION_STATUS.md");
assert(m11SecondAudit.includes("G11 acceptance pending") &&
  m11ThirdAudit.includes("G11 acceptance pending") &&
  m11ThirdAudit.includes("m11-eval-v2") &&
  m11FourthAudit.includes("G11 acceptance pending") &&
  m11FourthAudit.includes("HistoryPageAsync") &&
  m11FourthAudit.includes("NOT EXECUTED") &&
  m11FifthAudit.includes("G11 acceptance pending") &&
  m11FifthAudit.includes("F11-036") &&
  m11FifthAudit.includes("NOT EXECUTED") &&
  m11SixthAudit.includes("G11 acceptance pending") &&
  m11SixthAudit.includes("F11-039") &&
  m11SixthAudit.includes("NOT EXECUTED") &&
  m11Implementation.includes("G11 are NOT VERIFIED") &&
  m11Implementation.includes("G11-S01 is **BLOCKED**"),
  "M11 must retain the complete unverified, acceptance-pending disposition");
for (const [name, path] of [
  ["README", "README.md"],
  ["Status", "docs/STATUS.md"],
  ["Roadmap", "docs/ROADMAP.md"],
  ["Architecture", "docs/ARCHITECTURE.md"],
  ["API contracts", "docs/API_CONTRACTS.md"],
  ["Security", "docs/SECURITY.md"],
  ["Test strategy", "docs/TEST_STRATEGY.md"],
  ["Operations", "docs/OPERATIONS.md"]
]) {
  assert(get(path).includes("M11_SIXTH_CORRECTNESS_AUDIT_20261108.md"),
    name + " omits the current M11 source audit");
}
assert(storagePolicy.includes("Persistent project-specific writes to C: are prohibited"), "E-drive storage policy missing its persistent-storage prohibition");
// The policy names the E: requirement generically; a workstation-specific folder name is
// deliberately absent so shared documentation does not disclose local machine layout.
assert(storagePolicy.includes("Configured E:-backed workspace path"), "E-drive workspace requirement missing from storage policy");
assert(!storagePolicy.includes("APP IDEAS"), "Storage policy must not name the workstation folder");
for (const [name, content] of [
  ["README", readme],
  ["Status", status],
  ["Roadmap", roadmap],
  ["PRD", prd],
  ["Acceptance", acceptance]
]) {
  assert(content.includes("LOCAL_STORAGE_POLICY.md"), name + " does not reference the mandatory E-drive policy");
}
assert(status.includes("G0 verified") && status.includes("accepted"), "Status ledger must reflect accepted G0");
for (const [name,body,needle] of [
  ["README",readme,"M8 implementation passed source-locked technical verification"],
  ["Status",status,"M8/G8 accepted 2026-11-04"],
  ["Roadmap",roadmap,"G11-G15 and G17-G24 remain open"],
  ["Contributors",contributors,"G0-G10 and G16 are explicitly accepted and integrated"],
  ["Parallel delivery",parallelPolicy,"M8 verification and independent parallel implementation boundary"],
  ["Implementation plan",implementationPlan,"M8 accepted after independent G8 review"],
  ["G8 plan",m8Plan,"11 committed, source-isolated security mutation variants"],
  ["M8 status",m8Status,"One complete source-locked local run passed all 24 stages"],
  ["Second remediation",secondRemediation,"Five findings patched in source"],
  ["Verification runbook",runbook,"G8 technical verification"]
]) assert(body.includes(needle), name + " contains stale or missing M8 status");
for (const [name,body] of [
  ["README",readme],["Status",status],["Roadmap",roadmap],
  ["Contributors",contributors],["Parallel",parallelPolicy],
  ["Implementation plan",implementationPlan],["G8 plan",m8Plan]
]) {
  assert(!/G0-G6 are verified and explicitly accepted|G7-G24 each require independent verification|no M8 code or G8 evidence exists|Suggested future runner|M8 parallel implementation boundary \(planned\)/i.test(body),
    "Superseded milestone statement returned in " + name);
}
assert(status.includes("D-006") && status.includes("accepted"), "Status must record accepted D-006");
assert(m8Status.includes("G8") && m8Status.includes("Explicitly accepted 2026-11-04") && m8Status.includes("M8_G8_GATE_CLOSURE.md"), "G8 acceptance must reference the independently reviewed gate closure");
for (const [name, body, marker] of [
  ["README", readme, "M9 idempotent worker and reconciliation (accepted and merged)"],
  ["Status", status, "G9 accepted 2026-11-05"],
  ["Roadmap", roadmap, "M9_LOCAL_CONTRACT_PROPOSAL.md"],
  ["Implementation plan", implementationPlan, "M9 architecture and accepted implementation"],
  ["M9 architecture", m9Architecture, "Claim, processing and effect-once transaction"],
  ["M9 plan", m9Plan, "M9-04: Integrated verification and controlled recovery"],
  ["M9 contract", m9Contract, "**Decision:** Acceptance pending."],
  ["M9 status", m9Status, "**G9:** Not executed, no evidence, no acceptance."],
  ["G9 plan", g9Verification, "G9-S01 through G9-S25"],
  ["M9 implementation", m9Implementation, "Final G9 status (2026-11-05)"],
  ["M9 decision", m9Decision, "All six choices approved with one mandatory operating condition"],
  ["G9 package", g9Package, "Contract accepted by subsequent decision"],
  ["Browser supplement", m9Browsers, "174 test executions"],
  ["M9 source audit", m9Audit, "No application build, test suite, PostgreSQL query"]
]) assert(body.includes(marker), name + " missing or inconsistent M9 planning state");
assert(g9Verification.split("\n").filter(line => line.startsWith("| **G9-S")).length === 25,
  "G9 must contain exactly 25 explicit scenarios");
assert(!/G9 (?:accepted|verified|executed) 2026/i.test(m9Status),
  "Planning cannot claim G9 executable acceptance");
assert(readme.includes("docs/M9_LOCAL_CONTRACT_DECISION.md") &&
  status.includes("M9_LOCAL_CONTRACT_DECISION.md") &&
  roadmap.includes("M9_LOCAL_CONTRACT_DECISION.md"),
  "Current M9 contract approval must be linked from project overview, status and roadmap");
const g9Closure = get("docs/M9_G9_GATE_CLOSURE.md");
const g9Review = get("docs/M9_G9_INDEPENDENT_EVIDENCE_REVIEW.md");
assert(m9Decision.includes("2026-11-05") &&
  m9Decision.includes("Do not enable sustained transport-event processing") &&
  m9Decision.includes("4684352") &&
  g9Closure.includes("G9 accepted") &&
  g9Closure.includes("4684352c70132bebb5a4adc04753eb9651b84fa5") &&
  g9Review.includes("Accepted with disclosed limitations") &&
  m9Implementation.includes("Final G9 status (2026-11-05)") &&
  readme.includes("G9_GATE_CLOSURE.md") &&
  status.includes("G9 accepted 2026-11-05") &&
  roadmap.includes("G11-G15 and G17-G24 remain open"),
  "M9 acceptance, evidence review, merge or backlog condition is missing");
assert(prd.includes("M0-M24"), "PRD omits granular milestone range");
assert(readme.includes("M0 through M24"), "README points to a stale milestone range");
assert(roadmap.includes("## Legacy milestone mapping"), "Missing version migration map");
assert(graphView.includes("## Legacy identifier migration"), "Missing graph ID migration warning");
const unique = new Set();
for (const milestone of graph.milestones) {
  assert(!unique.has(milestone.id), "Duplicate milestone " + milestone.id);
  unique.add(milestone.id);
  const number = Number(milestone.id.slice(1));
  assert(roadmap.includes("#### " + milestone.id + ": " + milestone.title), "Roadmap omits " + milestone.id);
  assert(graphView.includes("### " + milestone.id + ": " + milestone.title), "Rendered graph omits " + milestone.id);
  assert(status.includes("| **" + milestone.id + " " + milestone.title + "** |"), "Status ledger omits " + milestone.id);
  assert(acceptance.includes("| **G" + number + "** | " + milestone.id + " "), "Acceptance gate missing for " + milestone.id);
  for (const id of milestone.decisions || []) {
    const line = decisions.split("\n").find(s => s.startsWith("| " + id + " |"));
    const accepted = { "D-001": "M1", "D-003": "M4" };
    if (Object.hasOwn(accepted, id)) {
      assert(new RegExp("^#{2,3} " + id + ":", "m").test(decisions) && milestone.id === accepted[id],
        "Approved decision " + id + " missing or misallocated");
    } else {
      // The final cell may name a work package ("M8-01 and M9"); it must reference the owning milestone.
      const requiredBefore = line ? line.replace(/\s*\|\s*$/, "").split("|").pop() : "";
      assert(new RegExp("\\b" + milestone.id + "\\b").test(requiredBefore), "Decision deadline mismatch " + id);
    }
  }
}
assert(unique.size === 25, "Milestone count must remain deliberate; update docs and checks if changed");
const quarterly = [["M6-04", "M6"], ["M12-04", "M12"], ["M18-04", "M18"], ["M24-03", "M24"]];
for (const [id, milestone] of quarterly) {
  const node = graph.nodes.find(n => n.id === id);
  assert(node && node.milestone === milestone && /historical-volume compatibility checkpoint/i.test(node.verify), "Quarter-mile snapshot checkpoint missing: " + id);
}
assert(roadmap.includes("Historical Docker/PostgreSQL volume compatibility is **not an M0 acceptance requirement**"), "Roadmap incorrectly requires existing-volume testing at G0");
assert(acceptance.includes("G0 requires fresh, disposable database migration/seed"), "M0 gate must use only fresh test data");
assert(roadmap.includes("M19 remains the mandatory"), "Comprehensive M19 recovery gate must remain intact");
assert(graph.nodes.some(n => n.id === "M24-04" && n.requirements.includes("NFR-009") && /accessibility/i.test(n.title)), "Final M24-04 accessibility gate is missing");
for (const node of graph.nodes) {
  if (node.milestone !== "M24") {
    assert(!node.requirements.includes("NFR-009"), "Accessibility requirement cannot block early gate: " + node.id);
    assert(!/accessibility|screen.reader|keyboard\/focus|WCAG/i.test(node.verify), "Early milestone has accessibility verification: " + node.id);
  }
}
assert(roadmap.includes("All dedicated accessibility testing is deferred to **M24-04"), "Roadmap must reserve accessibility verification for final M24");
assert(acceptance.includes("exclusively for M24-04"), "Acceptance policy must defer accessibility to M24-04");
assert(graph.nodes.filter(n => n.gate).length === unique.size, "Gate count mismatch");
for (const node of graph.nodes) {
  assert(graphView.includes("| **" + node.id + ": ") || graphView.includes("**" + node.id + " acceptance evidence:**"), "Missing work package or gate in rendered view: " + node.id);
}
const docsDir = join(root, "docs");
const documents = [
  "README.md",
  ...readdirSync(docsDir).filter(s=>s.endsWith(".md")).map(s=>"docs/"+s),
  "third_party/dcsa-track-and-trace/README.md"
];
// Include the pinned third-party reference index, not only the root and docs
// directories. Its original reference snapshots remain immutable.
assert(documents.length >= 98, "Repository Markdown inventory unexpectedly shrank");
for (const path of documents) {
  const body = get(path);
  assert(!body.includes("\u2014"), "Em dash in " + path);
  assert(!/^\s*(?:Co-authored-by:|Generated-by:|Assisted-by:|Authored-by:)\s*\S/im.test(body),
    "Attribution footer in " + path);
  for (const match of body.matchAll(/\]\(([^)]+)\)/g)) {
    const target = match[1].split("#")[0].split("?")[0];
    if (!target || /^(?:https?:|mailto:)/.test(target)) continue;
    const candidate = normalize(join(root, dirname(path), decodeURIComponent(target)));
    const rel = relative(root, candidate);
    assert(rel !== ".." && !rel.startsWith(".." + sep) && !isAbsolute(rel), "Link leaves repository: " + path + " -> " + target);
    assert(existsSync(candidate), "Broken Markdown link: " + path + " -> " + target);
  }
}
assert(graph.version === "3.0", "The milestone graph must use prerequisite-based v3 DAG");
assert(graphView.includes("## Full milestone dependency DAG"), "Parallel dependency map is absent");
assert(!graphView.includes("Gate transitions are sequential."), "Legacy serial gate policy returned");
assert(roadmap.includes("## Parallel execution and integration policy") &&
  readme.includes("PARALLEL_DELIVERY_POLICY.md"), "Parallel gate policy is not linked");
assert(existsSync(join(root, "docs/PARALLEL_DELIVERY_POLICY.md")), "Parallel policy missing");
const graphNodes = new Map(graph.nodes.map(n => [n.id, n]));
const requiredStarts = {
  "M4-01": ["G3"], "M5-01": ["G3"], "M6-01": ["G4", "G5"],
  "M7-01": ["G3"], "M12-01": ["G5"], "M16-01": ["G3"],
  "M17-01": ["G3"], "M19-01": ["G3"], "M20-01": ["G3"],
  "M23-01": ["G3"], "M24-01": ["G22", "G23"]
};
for (const [id, expected] of Object.entries(requiredStarts)) {
  const node = graphNodes.get(id);
  assert(node && node.depends_on.join(",") === expected.join(","),
    "Unexpected parallel dependency on " + id);
}
assert(graphNodes.get("M9-01").depends_on.join(",") === "G8" &&
  graphNodes.get("M9-02").depends_on.join(",") === "M9-01" &&
  graphNodes.get("M9-03").depends_on.join(",") === "M9-01" &&
  graphNodes.get("M9-04").depends_on.includes("M9-02") &&
  graphNodes.get("M9-04").depends_on.includes("M9-03"),
  "M9 work-package prerequisite graph diverged from accepted G8");
assert(graphNodes.get("M8-01").depends_on.includes("G7") &&
  graphNodes.get("M8-02").depends_on.includes("G6") &&
  graphNodes.get("M8-03").depends_on.includes("G6"),
  "Split M8 contract and assignment prerequisites missing");
const allPrior = new Set();
const visit = id => {
  if (allPrior.has(id)) return;
  const node = graphNodes.get(id);
  assert(node, "Missing DAG node " + id);
  allPrior.add(id);
  for (const dep of node.depends_on) visit(dep);
};
visit("G24");
for (let i = 0; i < 24; i++)
  assert(allPrior.has("G" + i), "Final release omits accepted prerequisite G" + i);
for (const m of graph.milestones.slice(1)) {
  const gates = [...new Set(graph.nodes.filter(n => n.milestone === m.id && !n.gate)
    .flatMap(n => n.depends_on.filter(d => /^G\d+$/.test(d))))];
  const row = roadmap.split("\n").find(line => line.startsWith("| **" + m.id + " "));
  assert(row && gates.every(gate => row.includes(gate + " accepted")),
    "Roadmap prerequisites diverged from graph " + m.id);
}
assert(acceptance.includes("G24 requires accepted G0-G23"),
  "Final release gate no longer requires all earlier accepted gates");
process.stdout.write("Planning docs are internally consistent: " + graph.milestones.length + " milestones, " + graph.nodes.length + " graph nodes, " + documents.length + " Markdown files checked.\n");
