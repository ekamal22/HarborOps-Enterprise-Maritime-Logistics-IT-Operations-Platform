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

function assert(ok, message) {
  if (!ok) throw Error(message);
}

assert(graph.schema_version === 2, "Expected version 2 execution graph");
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
      assert(line && line.endsWith("| " + milestone.id + " |"), "Decision deadline mismatch " + id);
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
const documents = ["README.md", ...readdirSync(docsDir).filter(s=>s.endsWith(".md")).map(s=>"docs/"+s)];
for (const path of documents) {
  const body = get(path);
  assert(!body.includes("\u2014"), "Em dash in " + path);
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
