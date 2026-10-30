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
assert(storagePolicy.includes("APP IDEAS"), "E-drive workspace root missing from storage policy");
for (const [name, content] of [
  ["README", readme],
  ["Status", status],
  ["Roadmap", roadmap],
  ["PRD", prd],
  ["Acceptance", acceptance]
]) {
  assert(content.includes("LOCAL_STORAGE_POLICY.md"), name + " does not reference the mandatory E-drive policy");
}
assert(status.includes("G0 verified, acceptance pending"), "Status ledger does not reflect the verified M0 gate");
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
    assert(line && line.endsWith("| " + milestone.id + " |"), "Decision deadline mismatch " + id);
  }
}
assert(unique.size === 25, "Milestone count must remain deliberate; update docs and checks if changed");
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
process.stdout.write("Planning docs are internally consistent: " + graph.milestones.length + " milestones, " + graph.nodes.length + " graph nodes, " + documents.length + " Markdown files checked.\n");
