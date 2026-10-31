export type OrganizationItem = {
  id: string;
  slug: string;
  displayName: string;
  role: "Analyst" | "Manager" | "Support" | "Administrator" | "Auditor";
  membershipVersion: number;
};

export type OrganizationProfile = {
  id: string;
  slug: string;
  displayName: string;
  status: string;
  version: number;
};

export type Membership = {
  userId: string;
  email: string;
  displayName: string;
  role: OrganizationItem["role"];
  status: "Active" | "Suspended" | "Removed";
  version: number;
};

export class OrganizationRequestError extends Error {
  constructor(public readonly status: number, public readonly code: string, message: string) {
    super(message);
    this.name = "OrganizationRequestError";
  }
}

const jsonHeaders = { "Content-Type": "application/json" };

async function checked<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const body: { code?: string; message?: string } = await response.json().catch(() => ({}));
    throw new OrganizationRequestError(response.status, body.code ?? "request_failed",
      body.message ?? "Request could not be completed");
  }
  return response.json() as Promise<T>;
}

export async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(path, { credentials: "same-origin", cache: "no-store" });
  return checked<T>(response);
}

async function csrf(): Promise<string> {
  const result = await getJson<{ token: string }>("/api/v1/auth/csrf");
  return result.token;
}

export async function mutate<T>(method: "POST" | "PUT" | "PATCH" | "DELETE", path: string,
  body?: object): Promise<T | null> {
  const token = await csrf();
  const response = await fetch(path, {
    method, credentials: "same-origin", cache: "no-store",
    headers: { ...jsonHeaders, "X-HarborOps-CSRF": token },
    ...(body ? { body: JSON.stringify(body) } : {})
  });
  if (response.status === 204) return null;
  return checked<T>(response);
}

export function organizationError(error: unknown): string {
  if (error instanceof OrganizationRequestError) {
    if (error.status === 401) return "Your session has expired. Sign in again.";
    if (error.status === 503) return "The organization service is temporarily unavailable.";
    if (error.code === "organization_access_revoked") return "Your organization access has changed. Refresh the organization list.";
    if (error.code === "organization_selection_required") return "Select an active organization first.";
    if (error.code === "conflict") return "The record changed. Refresh and try again.";
    return error.message;
  }
  return error instanceof Error ? error.message : "Organization request failed";
}
