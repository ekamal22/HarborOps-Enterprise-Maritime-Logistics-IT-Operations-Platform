import { getJson, mutate } from "../auth/organizationClient";

export type IncidentState = "New" | "Triage" | "Investigating" | "Mitigated" |
  "Monitoring" | "Resolved" | "Closed";
export type Severity = "SEV-1" | "SEV-2" | "SEV-3" | "SEV-4";
export type Incident = {
  id: string; title: string; serviceCode: string; severity: Severity;
  state: IncidentState; impactCode: string; version: number;
  updatedAtUtc: string; assignedTo: string | null;
};
export type IncidentEvent = {
  id: string; sequence: number; kind: string; fromState: string | null;
  toState: string | null; reasonCode: string; actorUserId: string;
  occurredAtUtc: string; relatedId: string | null;
};
export type IncidentNote = { id: string; body: string; actorUserId: string; occurredAtUtc: string };
export type IncidentAction = { id: string; title: string; state: string; ownerUserId: string; version: number };
export type IncidentEvidence = { id: string; kind: string; observationCode: string;
  sourceStatus: string; observedAtUtc: string; capturedAtUtc: string };
export type IncidentHistory = { incident: Incident; events: IncidentEvent[];
  notes: IncidentNote[]; actions: IncidentAction[]; evidence: IncidentEvidence[]; nextEventAfter: number | null };
const base = (organizationId: string) =>
  "/api/v1/organizations/" + encodeURIComponent(organizationId) + "/incidents";
const key = () => ({ "X-HarborOps-Operation-Id": crypto.randomUUID() });

export async function listIncidents(org: string, cursor?: string): Promise<{ incidents: Incident[]; nextCursor: string | null }> {
  return getJson<{ incidents: Incident[]; nextCursor: string | null }>(base(org) +
    "?limit=25" + (cursor ? "&cursor=" + encodeURIComponent(cursor) : ""));
}
export async function getIncident(org: string, id: string, full = true, afterSeq?: number): Promise<IncidentHistory | Incident> {
  return full ? getJson<IncidentHistory>(base(org) + "/" + encodeURIComponent(id) + "/history" +
      (afterSeq ? "?afterSeq=" + encodeURIComponent(String(afterSeq)) : "")) :
    getJson<Incident>(base(org) + "/" + encodeURIComponent(id) + "/summary");
}
export async function createIncident(org: string, data: {
  title: string; serviceCode: string; severity: Severity; impactCode: string;
  detectedAtUtc: string;
}): Promise<Incident> {
  return (await mutate<Incident>("POST", base(org), data, key()))!;
}
export async function updateIncident(org: string, id: string, name: string,
  body: object): Promise<Incident> {
  return (await mutate<Incident>("POST", base(org) + "/" + encodeURIComponent(id) +
    "/" + name, body, key()))!;
}
export async function assignIncident(org: string, id: string,
  body: { expectedVersion: number; assignedTo: string; reasonCode: string }): Promise<Incident> {
  return (await mutate<Incident>("PATCH", base(org) + "/" + encodeURIComponent(id) +
    "/assignment", body, key()))!;
}
export async function addAction(org: string, id: string,
  body: { expectedVersion: number; title: string; ownerUserId: string }): Promise<Incident> {
  return (await mutate<Incident>("POST", base(org) + "/" + encodeURIComponent(id) +
    "/actions", body, key()))!;
}
export async function updateAction(org: string, id: string, action: IncidentAction,
  state: string, expectedVersion: number): Promise<Incident> {
  return (await mutate<Incident>("PATCH", base(org) + "/" + encodeURIComponent(id) +
    "/actions/" + encodeURIComponent(action.id), {
      expectedVersion, actionId: action.id, expectedActionVersion: action.version,
      toState: state, reasonCode: "action_progress"
    }, key()))!;
}
export async function grantIncidentRead(org: string, recipientId: string): Promise<void> {
  const expiresAtUtc = new Date(Date.now() + 24 * 3600 * 1000).toISOString();
  await mutate("PUT",base(org) + "/read-grants/" + encodeURIComponent(recipientId),{ expiresAtUtc });
}
