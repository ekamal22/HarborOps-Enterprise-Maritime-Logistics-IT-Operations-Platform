import assert from "node:assert/strict";
import test from "node:test";
import { isD003Accepted } from "./g4-decision-gate.mjs";

const pending = `## Proposed D-003 decision for M4
**Status:** Proposed, decision pending.
## Open decisions
| ID | Status |
| --- | --- |
| D-003 | Pending |
## Decision change protocol
A later decision may be accepted.
`;
const accepted = `## D-003: Container identifier format
**Status:** Accepted
**Identifier policy:** Tagged ISO6346 and Synthetic
**Standard edition:** ISO 6346:2022
**Category scope:** U freight containers
## Open decisions
| ID | Status |
| --- | --- |
| D-002 | Pending |
## Decision change protocol
`;
test("A later accepted decision cannot approve pending D-003", () => {
  assert.equal(isD003Accepted(pending + "\n## D-009: Other\n**Status:** Accepted\n"), false);
});
test("A pending open-decisions row blocks even an accepted D-003 section", () => {
  assert.equal(isD003Accepted(accepted.replace("| D-002 | Pending |",
    "| D-003 | Pending |")), false);
});
test("Approval requires pinned edition and category scope in the D-003 section", () => {
  assert.equal(isD003Accepted(accepted.replace("**Category scope:** U freight containers", "")), false);
  assert.equal(isD003Accepted(accepted.replace("**Standard edition:** ISO 6346:2022", "")), false);
});
test("An explicitly accepted and fully specified D-003 record is recognized", () => {
  assert.equal(isD003Accepted(accepted), true);
});
