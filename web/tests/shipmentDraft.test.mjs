import test from "node:test";
import assert from "node:assert/strict";
import {
  editedFields, isDirty, mergeLatest, serverChanged, scheduleEditable, workflowOptions
} from "../src/lib/shipmentDraft.mjs";

const base = {
  referenceCode: "SHIP-1", displayName: "Original", plannedDepartureAtUtc: "2026-11-05T00:00:00Z",
  plannedArrivalAtUtc: "2026-11-09T00:00:00Z", workflowStatus: "Draft"
};

test("blank and null values are equivalent and untouched drafts are clean", () => {
  assert.equal(isDirty({ ...base, displayName: "" }, { ...base, displayName: "" }), false);
  assert.equal(isDirty({ ...base, displayName: null }, { ...base, displayName: "" }), false);
  assert.deepEqual(editedFields(base, base), []);
});

test("every editable field is detected independently", () => {
  const draft = { ...base, displayName: "X", plannedDepartureAtUtc: "", workflowStatus: "Booked" };
  assert.deepEqual(editedFields(base, draft).sort(),
    ["displayName", "plannedDepartureAtUtc", "workflowStatus"].sort());
});

test("fields the user did not touch follow the server snapshot", () => {
  const latest = { ...base, displayName: "Server name", plannedArrivalAtUtc: "2026-11-10T00:00:00Z" };
  const result = mergeLatest(base, base, latest);
  assert.deepEqual(result.draft, latest);
  assert.deepEqual(result.kept, []);
  assert.deepEqual(result.collisions, []);
});

test("edited fields keep the user's value and untouched fields take the server's", () => {
  const draft = { ...base, displayName: "Mine" };
  const latest = { ...base, plannedArrivalAtUtc: "2026-11-10T00:00:00Z" };
  const result = mergeLatest(base, draft, latest);
  assert.equal(result.draft.displayName, "Mine");
  assert.equal(result.draft.plannedArrivalAtUtc, "2026-11-10T00:00:00Z");
  assert.deepEqual(result.kept, ["displayName"]);
  assert.deepEqual(result.collisions, []);
});

test("an edit to a field the server also changed is a reported collision, not a silent overwrite", () => {
  const draft = { ...base, displayName: "Mine" };
  const latest = { ...base, displayName: "Theirs" };
  const result = mergeLatest(base, draft, latest);
  assert.equal(result.draft.displayName, "Mine");
  assert.deepEqual(result.collisions, ["displayName"]);
});

test("both sides converging on the same value is not a collision", () => {
  const draft = { ...base, displayName: "Same" };
  const latest = { ...base, displayName: "Same" };
  assert.deepEqual(mergeLatest(base, draft, latest).collisions, []);
});

test("a chosen status that is no longer reachable is discarded and reported", () => {
  const draft = { ...base, workflowStatus: "Booked" };
  const latest = { ...base, workflowStatus: "Cancelled" };
  const result = mergeLatest(base, draft, latest);
  assert.equal(result.draft.workflowStatus, "Cancelled");
  assert.deepEqual(result.discarded, ["workflowStatus"]);
  assert.ok(!result.kept.includes("workflowStatus"));
});

test("a still-reachable chosen status is kept", () => {
  const draft = { ...base, workflowStatus: "Cancelled" };
  const latest = { ...base, workflowStatus: "Booked" };
  const result = mergeLatest(base, draft, latest);
  assert.equal(result.draft.workflowStatus, "Cancelled");
  assert.ok(result.kept.includes("workflowStatus"));
});

test("schedule edits are discarded and reported once the server moved the shipment into transit", () => {
  const draft = { ...base, plannedDepartureAtUtc: "2026-11-06T00:00:00Z", displayName: "Mine" };
  const latest = { ...base, workflowStatus: "InTransit" };
  const result = mergeLatest(base, draft, latest);
  assert.equal(result.draft.plannedDepartureAtUtc, base.plannedDepartureAtUtc);
  assert.deepEqual(result.discarded, ["plannedDepartureAtUtc"]);
  assert.equal(result.draft.displayName, "Mine");
});

test("merging never mutates its inputs", () => {
  const draft = Object.freeze({ ...base, displayName: "Mine" });
  const latest = Object.freeze({ ...base, displayName: "Theirs" });
  const frozenBase = Object.freeze({ ...base });
  assert.doesNotThrow(() => mergeLatest(frozenBase, draft, latest));
});

test("version or record status changes are server changes", () => {
  assert.equal(serverChanged({ version: 1, recordStatus: "Active" }, { version: 1, recordStatus: "Active" }), false);
  assert.equal(serverChanged({ version: 1, recordStatus: "Active" }, { version: 2, recordStatus: "Active" }), true);
  assert.equal(serverChanged({ version: 1, recordStatus: "Active" }, { version: 1, recordStatus: "Archived" }), true);
});

test("workflow options and schedule editability follow the closed lifecycle", () => {
  assert.deepEqual(workflowOptions("Draft"), ["Draft", "Booked", "Cancelled"]);
  assert.deepEqual(workflowOptions("Completed"), ["Completed"]);
  assert.equal(scheduleEditable("Booked"), true);
  assert.equal(scheduleEditable("InTransit"), false);
});
