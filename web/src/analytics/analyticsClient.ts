import { OrganizationRequestError, mutate } from "../auth/organizationClient";

export type KpiDefinition = {
  code: string; label: string; sourceClass: string;
  denominatorCode: string | null; description: string;
};
export type KpiCount = {
  metricCode: string; dimension: string; bucketStartUtc: string | null; count: number;
};
export type KpiReport = {
  policyVersion: string; reportId: string; reportToken: string;
  fromUtc: string; beforeUtc: string; evaluatedAtUtc: string;
  expiresAtUtc: string; memberCount: number;
  bucket: "day" | "week"; originPortId: string | null;
  destinationPortId: string | null; voyageId: string | null; sourceKind: string | null;
  counts: KpiCount[];
};
export type KpiMember = {
  memberKey: string; shipmentId: string; exceptionId: string | null; referenceCode: string;
};
export type KpiMemberPage = { items: KpiMember[]; nextAfter: string | null; reportId: string };
export type KpiGrant = { userId: string; active: boolean; version: number };
export type KpiFilterValues = {
  fromDate: string; beforeDate: string; bucket: "day" | "week";
  originPortId: string; destinationPortId: string; voyageId: string;
  recordSource: "" | "UserProvided" | "Synthetic";
};
export type DrillSelection = {
  metricCode: string; dimension: string; bucketStartUtc: string | null; label: string;
};

const root = (organization: string) =>
  "/api/v1/organizations/" + encodeURIComponent(organization) + "/analytics";

async function json<T>(path: string, signal?: AbortSignal,
  headers?: Record<string, string>): Promise<T> {
  const response = await fetch(path, {
    method: "GET", credentials: "same-origin", cache: "no-store",
    headers: { Accept: "application/json", ...headers }, signal
  });
  if (!response.ok) {
    const body: { code?: string; message?: string } =
      await response.json().catch(() => ({}));
    throw new OrganizationRequestError(response.status,
      body.code ?? "request_failed", body.message ?? "Reporting request failed");
  }
  return response.json() as Promise<T>;
}

export function catalog(org: string, signal?: AbortSignal) {
  return json<{ policyVersion: string; definitions: KpiDefinition[] }>(
    root(org) + "/catalog", signal);
}

export function overview(org: string, filter: KpiFilterValues, signal?: AbortSignal) {
  const query = new URLSearchParams({ bucket: filter.bucket });
  if (filter.fromDate || filter.beforeDate) {
    if (!filter.fromDate || !filter.beforeDate)
      throw new Error("Specify both UTC start and end dates");
    if (filter.beforeDate <= filter.fromDate)
      throw new Error("The UTC end date must be after the start date");
    const days = (Date.parse(filter.beforeDate + "T00:00:00Z") -
      Date.parse(filter.fromDate + "T00:00:00Z")) / 86_400_000;
    if (!Number.isFinite(days) || days > 366)
      throw new Error("Reporting windows must not exceed 366 days");
    query.set("fromUtc", filter.fromDate + "T00:00:00Z");
    query.set("beforeUtc", filter.beforeDate + "T00:00:00Z");
  } else query.set("defaultWindow", "last7CompleteDays");
  for (const key of ["originPortId", "destinationPortId", "voyageId"] as const) {
    if (filter[key].trim()) {
      if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(filter[key].trim()))
        throw new Error("Invalid " + key + " UUID");
      query.set(key, filter[key].trim());
    }
  }
  if (filter.recordSource) query.set("recordSource", filter.recordSource);
  return json<KpiReport>(root(org) + "/overview?" + query, signal);
}

export function members(org: string, report: KpiReport, selection: DrillSelection,
  after: string | null, signal?: AbortSignal) {
  const query = new URLSearchParams({
    metricCode: selection.metricCode, dimension: selection.dimension, limit: "50"
  });
  if (selection.bucketStartUtc) query.set("bucketStartUtc", selection.bucketStartUtc);
  if (after) query.set("after", after);
  return json<KpiMemberPage>(root(org) + "/reports/" +
    encodeURIComponent(report.reportId) + "/members?" + query, signal,
    { "X-HarborOps-Report": report.reportToken });
}

export function auditorGrants(org: string, signal?: AbortSignal) {
  return json<{ grants: KpiGrant[] }>(root(org) + "/grants", signal);
}

export function setAuditorGrant(org: string, target: string, enabled: boolean) {
  return mutate<{ targetUserId: string; enabled: boolean; version: number }>(
    "PUT", root(org) + "/grants/" + encodeURIComponent(target), { enabled });
}