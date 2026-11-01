import { getJson, mutate } from "../auth/organizationClient";

export type ShipmentWorkflow = "Draft" | "Booked" | "InTransit" | "Arrived" | "Completed" | "Cancelled";
export type ShipmentRow = {
  id: string;
  organizationId: string;
  referenceCode: string;
  externalReference: string | null;
  displayName: string | null;
  originPortId: string;
  destinationPortId: string;
  plannedDepartureAtUtc: string | null;
  plannedArrivalAtUtc: string | null;
  workflowStatus: ShipmentWorkflow;
  recordStatus: "Active" | "Archived";
  version: number;
  originPortLabel?: string | null;
  destinationPortLabel?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
};
export type ShipmentChange = {
  id: string; version: number; fromStatus: ShipmentWorkflow | null;
  toStatus: ShipmentWorkflow; changedAtUtc: string;
};
export type ShipmentPage = { shipments: ShipmentRow[]; nextAfter: string | null };
const root = (org: string, id?: string) =>
  "/api/v1/organizations/" + encodeURIComponent(org) +
  "/shipments" + (id ? "/" + encodeURIComponent(id) : "");

export async function listShipments(org: string, q: string, recordStatus: string,
  after?: string): Promise<ShipmentPage> {
  const query = new URLSearchParams({ recordStatus, limit: "25" });
  if (q.trim()) query.set("q", q.trim());
  if (after) query.set("after", after);
  return getJson<ShipmentPage>(root(org) + "?" + query);
}
export async function shipmentDetail(org: string, id: string) {
  return getJson<ShipmentRow>(root(org, id));
}
export async function createShipment(org: string, body: object) {
  return mutate<ShipmentRow>("POST", root(org), body);
}
export async function changeShipment(org: string, id: string, body: object) {
  return mutate<ShipmentRow>("PATCH", root(org, id), body);
}
export async function archiveShipment(org: string, id: string, version: number) {
  return mutate<null>("DELETE", root(org, id), { expectedVersion: version });
}
export async function shipmentHistory(org: string, id: string) {
  return getJson<{ changes: ShipmentChange[] }>(root(org, id) + "/status-changes");
}
