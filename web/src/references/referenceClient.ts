import { getJson, mutate } from "../auth/organizationClient";

export type ReferenceKind = "ports" | "vessels";
export type ReferenceStatus = "Active" | "Archived";
export type ReferenceRow = {
  id: string;
  organizationId: string;
  referenceCode: string;
  name: string;
  status: ReferenceStatus;
  sourceKind: "UserProvided" | "Synthetic";
  version: number;
  createdAtUtc: string;
  updatedAtUtc: string;
  countryCode?: string;
  unLocode?: string | null;
  imoNumber?: string | null;
  mmsi?: string | null;
  flagCountryCode?: string | null;
};
export type ReferencePage = { nextAfter: string | null; ports?: ReferenceRow[]; vessels?: ReferenceRow[] };

export function referencesPath(organizationId: string, kind: ReferenceKind, id?: string): string {
  return "/api/v1/organizations/" + encodeURIComponent(organizationId) +
    "/" + kind + (id ? "/" + encodeURIComponent(id) : "");
}

export function listReferences(organizationId: string, kind: ReferenceKind,
  q: string, status: string, after?: string): Promise<ReferencePage> {
  const params = new URLSearchParams({ status, limit: "25" });
  if (q.trim()) params.set("q", q.trim());
  if (after) params.set("after", after);
  return getJson<ReferencePage>(referencesPath(organizationId, kind) + "?" + params.toString());
}

export function createReference(organizationId: string, kind: ReferenceKind,
  payload: Record<string, unknown>): Promise<ReferenceRow | null> {
  return mutate<ReferenceRow>("POST", referencesPath(organizationId, kind), payload);
}
export function updateReference(organizationId: string, kind: ReferenceKind, id: string,
  payload: Record<string, unknown>): Promise<ReferenceRow | null> {
  return mutate<ReferenceRow>("PATCH", referencesPath(organizationId, kind, id), payload);
}
export function archiveReference(organizationId: string, kind: ReferenceKind, id: string,
  expectedVersion: number): Promise<null> {
  return mutate<null>("DELETE", referencesPath(organizationId, kind, id),
    { expectedVersion }) as Promise<null>;
}
