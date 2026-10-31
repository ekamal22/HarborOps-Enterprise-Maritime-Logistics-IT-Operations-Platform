import { getJson, mutate } from "../auth/organizationClient";

export type MaritimeKind = "voyages" | "containers";
export type MaritimeRow = {
  id: string; organizationId: string; referenceCode: string;
  recordStatus: "Active" | "Archived"; sourceKind: "UserProvided" | "Synthetic";
  version: number; createdAtUtc: string; updatedAtUtc: string;
  vesselId?: string; originPortId?: string; destinationPortId?: string;
  plannedDepartureAtUtc?: string | null; plannedArrivalAtUtc?: string | null;
  actualDepartureAtUtc?: string | null; actualArrivalAtUtc?: string | null;
  voyageStatus?: "Planned" | "Underway" | "Arrived" | "Cancelled";
  identifierKind?: "ISO6346" | "Synthetic"; containerIdentifier?: string;
  equipmentType?: string; displayName?: string | null;
};
export type MaritimePage = { nextAfter: string | null; voyages?: MaritimeRow[]; containers?: MaritimeRow[] };
const root = (organizationId: string, kind: MaritimeKind, id?: string) =>
  "/api/v1/organizations/" + encodeURIComponent(organizationId) +
  "/" + kind + (id ? "/" + encodeURIComponent(id) : "");
export async function listMaritime(org: string, kind: MaritimeKind, q: string,
  status: string, after?: string): Promise<MaritimePage> {
  const params = new URLSearchParams({ recordStatus: status, limit: "25" });
  if (q.trim()) params.set("q", q.trim());
  if (after) params.set("after", after);
  return getJson<MaritimePage>(root(org, kind) + "?" + params);
}
export async function createMaritime(org: string, kind: MaritimeKind, data: object) {
  return mutate<MaritimeRow>("POST", root(org, kind), data);
}
export async function updateMaritime(org: string, kind: MaritimeKind, id: string, data: object) {
  return mutate<MaritimeRow>("PATCH", root(org, kind, id), data);
}
export async function archiveMaritime(org: string, kind: MaritimeKind, id: string, version: number) {
  return mutate<null>("DELETE", root(org, kind, id), { expectedVersion: version });
}
