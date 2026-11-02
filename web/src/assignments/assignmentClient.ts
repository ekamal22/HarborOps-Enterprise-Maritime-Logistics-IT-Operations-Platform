import { getJson, mutate } from "../auth/organizationClient";

export type AssignmentKind = "container-assignments" | "voyage-assignments";
export type Assignment = {
  id: string; organizationId: string; shipmentId: string;
  containerId?: string; voyageId?: string; targetLabel?: string | null;
  startsAtUtc: string; endsAtUtc: string | null;
  version: number; createdAtUtc: string; updatedAtUtc: string;
};
export type AssignmentPage = { assignments: Assignment[]; nextAfter: string | null };
const root = (org: string, shipment: string, kind: AssignmentKind, id?: string) =>
  "/api/v1/organizations/" + encodeURIComponent(org) +
  "/shipments/" + encodeURIComponent(shipment) + "/" + kind +
  (id ? "/" + encodeURIComponent(id) : "");

export function listAssignments(org: string, shipment: string, kind: AssignmentKind,
  after?: string) {
  const qs = new URLSearchParams({ limit: "25" });
  if (after) qs.set("after", after);
  return getJson<AssignmentPage>(root(org, shipment, kind) + "?" + qs);
}
export function addAssignment(org: string, shipment: string, kind: AssignmentKind,
  target: string, startsAtUtc: string, endsAtUtc: string | null) {
  const property = kind === "container-assignments" ? "containerId" : "voyageId";
  return mutate<Assignment>("POST", root(org, shipment, kind),
    { [property]: target, startsAtUtc, endsAtUtc });
}
export function closeAssignment(org: string, shipment: string, kind: AssignmentKind,
  id: string, expectedVersion: number, endsAtUtc: string) {
  return mutate<Assignment>("PATCH", root(org, shipment, kind, id),
    { expectedVersion, endsAtUtc });
}
