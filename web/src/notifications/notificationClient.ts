import { mutate, OrganizationRequestError } from "../auth/organizationClient";

export type NotificationItem = {
  id: string;
  shipmentId: string;
  exceptionId: string;
  eventType: "ExceptionOpened" | "ExceptionResolved" | "ExceptionAutoResolved";
  ruleCode: string;
  title: string;
  eventLabel: string;
  createdAtUtc: string;
  readAtUtc: string | null;
  acknowledgedAtUtc: string | null;
  state: "Unread" | "Read" | "Acknowledged";
  version: number;
};

export type NotificationFilter = "All" | "Unread" | "Read" | "Acknowledged";
export type NotificationPage = {
  items: NotificationItem[];
  nextCursor: string | null;
  snapshotAtUtc: string;
};

const base = (org: string) =>
  "/api/v1/organizations/" + encodeURIComponent(org) + "/notifications";

async function read<T>(url: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(url, {
    credentials: "same-origin", cache: "no-store", signal
  });
  if (!response.ok) {
    const body: { code?: string; message?: string } =
      await response.json().catch(() => ({}));
    throw new OrganizationRequestError(response.status,
      body.code ?? "request_failed",
      body.message ?? "Notification request could not be completed");
  }
  return response.json() as Promise<T>;
}

export const listNotifications = (org: string, filter: NotificationFilter,
  cursor?: string, signal?: AbortSignal) => {
  const query = new URLSearchParams({ state: filter, limit: "25" });
  if (cursor) query.set("cursor",cursor);
  return read<NotificationPage>(base(org) + "?" + query, signal);
};

export const notificationCount = (org: string, signal?: AbortSignal) =>
  read<{ unreadCount: number; unreadCountCapped?: boolean; asOfUtc: string }>(
    base(org) + "/unread-count", signal);

export const notificationDetail = (org: string, id: string, signal?: AbortSignal) =>
  read<NotificationItem>(base(org) + "/" + encodeURIComponent(id),signal);

export async function updateNotification(org: string, item: NotificationItem,
  action: "MarkRead" | "Acknowledge"): Promise<NotificationItem> {
  const idempotencyKey = crypto.randomUUID();
  const updated = await mutate<NotificationItem>("PATCH",
    base(org) + "/" + encodeURIComponent(item.id),
    { action, expectedVersion: item.version },
    { "Idempotency-Key": idempotencyKey });
  if (!updated) throw new Error("Notification update returned no item");
  return updated;
}
