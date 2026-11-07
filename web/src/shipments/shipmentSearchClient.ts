import { getJson, OrganizationRequestError } from "../auth/organizationClient";
import type { ShipmentRow, ShipmentWorkflow } from "./shipmentClient";

export type ShipmentAdvancedFilters = {
  workflowStatus: ShipmentWorkflow | "";
  voyageId: string;
  originPortId: string;
  destinationPortId: string;
  plannedDepartureFromUtc: string;
  plannedDepartureBeforeUtc: string;
  plannedArrivalFromUtc: string;
  plannedArrivalBeforeUtc: string;
};
export const emptyAdvancedFilters: ShipmentAdvancedFilters = {
  workflowStatus: "", voyageId: "", originPortId: "", destinationPortId: "",
  plannedDepartureFromUtc: "", plannedDepartureBeforeUtc: "",
  plannedArrivalFromUtc: "", plannedArrivalBeforeUtc: ""
};

export type ShipmentSearchFilters = ShipmentAdvancedFilters & {
  q: string;
  recordStatus: "Active" | "Archived" | "All";
};
export type ShipmentSearchPage = {
  shipments: ShipmentRow[];
  nextCursor: string | null;
  asOfUtc: string;
};

function queryString(filters: ShipmentSearchFilters): URLSearchParams {
  const params = new URLSearchParams();
  for (const [name, raw] of Object.entries(filters)) {
    const value = raw.trim();
    if (value) params.set(name, value);
  }
  return params;
}

const root = (org: string) =>
  "/api/v1/organizations/" + encodeURIComponent(org);

export async function searchShipments(org: string, filters: ShipmentSearchFilters,
  cursor?: string): Promise<ShipmentSearchPage> {
  const params = queryString(filters);
  params.set("limit", "25");
  if (cursor) params.set("cursor", cursor);
  return getJson<ShipmentSearchPage>(root(org) + "/shipments/search?" + params);
}

export async function exportShipmentsCsv(org: string, filters: ShipmentSearchFilters,
  signal?: AbortSignal): Promise<void> {
  const response = await fetch(root(org) + "/exports/shipments?" + queryString(filters), {
    credentials: "same-origin", cache: "no-store", signal
  });
  if (!response.ok) {
    const body: { code?: string; message?: string } =
      await response.json().catch(() => ({}));
    throw new OrganizationRequestError(response.status, body.code ?? "export_failed",
      body.message ?? "Export could not be completed");
  }
  if (!(response.headers.get("content-type") ?? "").toLowerCase().startsWith("text/csv")) {
    throw new Error("Export response was not CSV");
  }
  const blob = await response.blob();
  if (signal?.aborted) return;
  const disposition = response.headers.get("content-disposition") ?? "";
  const name = /filename="?([a-zA-Z0-9._-]+)"?/i.exec(disposition)?.[1] ?? "shipments.csv";
  const url = URL.createObjectURL(blob);
  try {
    const link = document.createElement("a");
    link.href = url;
    link.download = name;
    document.body.appendChild(link);
    link.click();
    link.remove();
  } finally {
    window.setTimeout(() => URL.revokeObjectURL(url), 2000);
  }
}
