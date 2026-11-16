// Binding of an assertion witness to the specific executed case it names. A witness that only quotes a string which
// appears somewhere in a file (a YAML comment, an error message) proves nothing if the case behind it was deleted
// or never ran. These bindings require the named case to exist as an executable block with the expected content.

// The promtool fixture file names its cases by the comment that precedes each block. A case is bound only when that
// comment sits directly above a real "- interval:" block holding input series and at least one executable
// assertion, and (for the recovery cases) both a firing and a cleared expectation for the expected alert.
export const ALERT_CASE_REQUIREMENTS = {
  "Readiness must not remain critical": {alertname: "HarborOpsDatabaseUnavailable", firing: true, cleared: true},
  "Latency recovers below": {alertname: "HarborOpsHighApiP95", firing: true, cleared: true},
  "S18-A03 repeat suppression": {alertname: "HarborOpsElevatedApiFailureRate", firing: true, cleared: true}
};

export function alertCaseBlock(text, marker) {
  const lines = text.split("\n");
  const at = lines.findIndex(line => /^\s*#/.test(line) && line.includes(marker));
  if (at < 0) return null;
  let start = -1;
  for (let i = at; i < lines.length; i++) {
    if (/^ {2}- interval:/.test(lines[i])) { start = i; break; }
    if (lines[i].trim() !== "" && !/^\s*#/.test(lines[i])) return null; // something other than comments sits between
  }
  if (start < 0) return null;
  let end = lines.length;
  for (let i = start + 1; i < lines.length; i++) if (/^ {2}- interval:/.test(lines[i])) { end = i; break; }
  return lines.slice(start, end).join("\n");
}

export function alertCaseBound(text, marker) {
  const block = alertCaseBlock(text, marker);
  if (block === null) return false;
  if (!/input_series:\s*\n\s+- series:/.test(block)) return false;
  const executable = /alert_rule_test:\s*\n\s+- eval_time:[^\n]*\n\s+alertname:[^\n]*\n/.test(block) || /promql_expr_test:\s*\n\s+- expr:/.test(block);
  if (!executable) return false;
  const requirement = ALERT_CASE_REQUIREMENTS[marker];
  if (!requirement) return true;
  if (!block.includes(`alertname: ${requirement.alertname}`)) return false;
  if (requirement.firing && !/exp_alerts:\s*\n\s+- exp_labels:/.test(block)) return false;
  if (requirement.cleared && !/exp_alerts: \[\]/.test(block)) return false;
  return true;
}

// S23-A03 names the manifest verifier's refusal of a completed run with missing evidence or a failed stage. Presence
// of that sentence in the verifier and a COMPLETE run prove nothing about the refusal itself; the three Node cases
// that drive the verifier into exactly those conditions must have executed and passed.
export const MANIFEST_REJECTION_CASES = [
  "fake complete claim without required raw result is rejected",
  "fake complete claim with incomplete stage evidence is rejected",
  "altered evidence cannot pass hash verification"
];
export const manifestRejectionBound = executedNodeCases => MANIFEST_REJECTION_CASES.every(name => executedNodeCases.has(name));
