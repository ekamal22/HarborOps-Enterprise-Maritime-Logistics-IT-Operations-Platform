// Pure rules for keeping a user's unsaved shipment edits separate from server data.
// "base" is the server snapshot the edit started from, "draft" is what the user has in the form,
// and "latest" is a newer server snapshot. All three use the same string-valued form shape.

export const EDITABLE = ["displayName", "plannedDepartureAtUtc", "plannedArrivalAtUtc", "workflowStatus"];
const SCHEDULE = ["plannedDepartureAtUtc", "plannedArrivalAtUtc"];

export const TRANSITIONS = {
  Draft: ["Booked", "Cancelled"],
  Booked: ["InTransit", "Cancelled"],
  InTransit: ["Arrived", "Cancelled"],
  Arrived: ["Completed"],
  Completed: [],
  Cancelled: []
};

const same = (a, b) => (a ?? "") === (b ?? "");

export function scheduleEditable(status) {
  return status === "Draft" || status === "Booked";
}

export function workflowOptions(status) {
  return [status, ...(TRANSITIONS[status] ?? [])];
}

export function editedFields(base, draft) {
  return EDITABLE.filter(key => !same(draft[key], base[key]));
}

export function isDirty(base, draft) {
  return editedFields(base, draft).length > 0;
}

// Combine a newer server snapshot with the user's edits without losing either:
// - a field the user did not touch follows the server;
// - a field the user edited keeps the user's value;
// - an edited field the server also changed to a different value is reported as a collision;
// - an edit that can no longer be saved (illegal status, locked schedule) is reported, not applied.
export function mergeLatest(base, draft, latest) {
  const merged = { ...latest };
  const kept = [];
  const collisions = [];
  const discarded = [];
  for (const key of EDITABLE) {
    if (same(draft[key], base[key])) continue;
    merged[key] = draft[key];
    kept.push(key);
    if (!same(latest[key], base[key]) && !same(latest[key], draft[key])) collisions.push(key);
  }
  if (!workflowOptions(latest.workflowStatus).includes(merged.workflowStatus)) {
    merged.workflowStatus = latest.workflowStatus;
    kept.splice(kept.indexOf("workflowStatus"), 1);
    discarded.push("workflowStatus");
  }
  if (!scheduleEditable(latest.workflowStatus)) {
    for (const key of SCHEDULE) {
      if (!kept.includes(key)) continue;
      merged[key] = latest[key];
      kept.splice(kept.indexOf(key), 1);
      discarded.push(key);
    }
  }
  return { draft: merged, kept, collisions, discarded };
}

export function serverChanged(base, latest) {
  return latest.version !== base.version || latest.recordStatus !== base.recordStatus;
}
