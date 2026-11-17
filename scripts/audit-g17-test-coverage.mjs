import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const base = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const read = relative => fs.readFileSync(path.join(base, relative), "utf8");
const states = new Set(["TEST_AUTHORED", "PARTIAL", "MISSING_TEST", "INDEPENDENT_REVIEW"]);

export function auditG17Coverage(inventory, coverage, readWitness, { strict = false } = {}) {
  const errors = [];
  const matrix = [];
  const seen = new Set();
  const stats = {TEST_AUTHORED:0, PARTIAL:0, MISSING_TEST:0, INDEPENDENT_REVIEW:0};
  if (inventory.gate_acceptance !== "NOT_ACCEPTED")
    errors.push("G17 gate must remain unaccepted during technical test preparation");
  if (coverage.gate !== "G17" || coverage.schema_version !== 1)
    errors.push("Missing or invalid explicit G17 test coverage schema");
  if (!Array.isArray(inventory.scenarios) || inventory.scenarios.length !== 25)
    errors.push("Expected exactly 25 inventory scenarios");
  if (!Array.isArray(coverage.scenarios) || coverage.scenarios.length !== 25)
    errors.push("Expected exactly 25 independently mapped test scenarios");

  for (let i = 1; i <= 25; i++) {
    const id = "G17-S" + String(i).padStart(2,"0");
    const source = inventory.scenarios?.[i-1];
    const mapped = coverage.scenarios?.[i-1];
    if (source?.id !== id || mapped?.id !== id) {
      errors.push("Scenario not in required order, missing or duplicated: " + id);
      continue;
    }
    if (source.name !== mapped.name)
      errors.push("Scenario name differs from source inventory: " + id);
    if (source.assertions?.length !== 3 || mapped.assertions?.length !== 3)
      errors.push("Every G17 scenario requires exactly three atomic clauses: " + id);

    for (let k = 1; k <= 3; k++) {
      const key = id + "-A" + String(k).padStart(2,"0");
      const assertion = source.assertions?.[k-1];
      const witness = mapped.assertions?.[k-1];
      if (assertion?.id !== key || witness?.id !== key || seen.has(key)) {
        errors.push("Missing, out-of-order or duplicate atomic assertion: " + key);
        continue;
      }
      seen.add(key);
      if (witness.expected !== assertion.expected ||
          assertion.status !== "NOT_EXECUTED" || assertion.witness !== null)
        errors.push("Contradictory unexecuted assertion baseline: " + key);
      if (!states.has(witness.assessment))
        errors.push("Unknown test-coverage classification: " + key);
      else stats[witness.assessment]++;
      if (i === 25 && witness.assessment !== "INDEPENDENT_REVIEW")
        errors.push("Independent S25 may not be marked as an automated technical pass: " + key);
      if (i !== 25 && witness.assessment === "INDEPENDENT_REVIEW")
        errors.push("Technical scenario may not be skipped as manual review: " + key);
      if (witness.assessment === "MISSING_TEST" && witness.witness !== null)
        errors.push("Missing test must not claim executable evidence: " + key);
      if (witness.assessment === "TEST_AUTHORED" || witness.assessment === "PARTIAL") {
        const declared = witness.witness;
        if (!declared || typeof declared.path !== "string" ||
            typeof declared.case !== "string" || !declared.case.trim() ||
            !/^[a-zA-Z0-9_./-]+$/.test(declared.path) ||
            declared.path.startsWith("/") || declared.path.includes("..")) {
          errors.push("Absent or unsafe named technical witness: " + key);
        }
        else {
          try {
            const sourceText = readWitness(declared.path);
            if (!sourceText.includes(declared.case))
              errors.push("Named test or behavior absent from source: " + key);
            if (witness.assessment === "TEST_AUTHORED" &&
                /\b(?:test|describe)\.(?:skip|fixme|todo)\s*\(|\b(?:xdescribe|xit)\s*\(|\[\s*Fact\s*\(\s*Skip\s*=|\[\s*Theory\s*\(\s*Skip\s*=|continue-on-error:\s*true/i.test(sourceText))
              errors.push("Required technical witness contains a skip or ignored failure: " + key);
          } catch {
            errors.push("Named test source is missing or inaccessible: " + key);
          }
        }
      }
      if (strict && i < 25 && witness.assessment !== "TEST_AUTHORED")
        errors.push("Independent S25 preflight blocks untested or partially tested clause: " + key);
      matrix.push({
        id:key, scenario:id, classification:witness.assessment,
        path:witness.witness?.path ?? null,
        case:witness.witness?.case ?? null,
        deficiency:witness.coverage_gap
      });
    }
  }

  if (seen.size !== 75 || matrix.length !== 75)
    errors.push("Required atomic G17 inventory must contain 75 unique clauses");
  if (strict && stats.TEST_AUTHORED !== 72)
    errors.push("Technical campaign cannot be dispatched without 72 individually authored witnesses");
  if (stats.INDEPENDENT_REVIEW !== 3)
    errors.push("Exactly three S25 reviewer assertions must remain independent and pending");
  return {gate:"G17", strict, technically_authored:stats.TEST_AUTHORED,
    technical_required:72, independent_review_required:3,
    stats, scenarios:25, assertions:matrix.length, matrix, errors,
    passed:errors.length===0};
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const strict = process.argv.includes("--strict");
  const inventory = JSON.parse(read("docs/M17_G17_ASSERTION_INVENTORY_20261112.json"));
  const coverage = JSON.parse(read("docs/M17_G17_TEST_COVERAGE_20261113.json"));
  const result = auditG17Coverage(inventory, coverage, read, {strict});
  const output = process.argv.find(arg=>arg.startsWith("--output="))?.slice(9);
  if (output) fs.writeFileSync(output, JSON.stringify(result,null,2)+"\n", {flag:"wx"});
  console.log(JSON.stringify({gate:result.gate, strict, stats:result.stats,
    scenarios:result.scenarios, assertions:result.assertions,
    errors:result.errors},null,2));
  if (!result.passed) process.exitCode=1;
}
