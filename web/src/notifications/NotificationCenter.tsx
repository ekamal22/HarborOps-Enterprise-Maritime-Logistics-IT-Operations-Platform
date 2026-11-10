import { useCallback, useEffect, useRef, useState } from "react";
import { organizationError, OrganizationRequestError } from "../auth/organizationClient";
import { listNotifications, notificationCount, updateNotification,
  type NotificationFilter, type NotificationItem } from "./notificationClient";

export type NotificationTarget = {
  shipmentId: string;
  exceptionId: string;
  request: number;
};

function revoked(error: unknown): boolean {
  return error instanceof OrganizationRequestError &&
    (error.status === 401 || error.code === "access_denied" ||
      error.code === "organization_access_revoked" ||
      error.code === "organization_selection_required");
}

export default function NotificationCenter({ organizationId, onAccessInvalidated,
  onOpen }: {
  organizationId: string;
  onAccessInvalidated: (message: string) => void;
  onOpen: (target: NotificationTarget) => void;
}) {
  const [filter, setFilter] = useState<NotificationFilter>("All");
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [unread, setUnread] = useState<number | null>(null);
  const [unreadCapped, setUnreadCapped] = useState(false);
  const [loading, setLoading] = useState(true);
  const [paging, setPaging] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [error, setError] = useState("");
  const serial = useRef(0);
  const openSerial = useRef(0);
  const mounted = useRef(true);
  const activeAbort = useRef<AbortController | null>(null);
  const pagingAbort = useRef<AbortController | null>(null);

  const invalidate = useCallback((cause: unknown) => {
    if (revoked(cause)) onAccessInvalidated(organizationError(cause));
  }, [onAccessInvalidated]);

  const refresh = useCallback(async (quiet = false) => {
    const run = ++serial.current;
    activeAbort.current?.abort();
    // A fresh first page supersedes any in-flight pagination request.
    pagingAbort.current?.abort();
    pagingAbort.current = null;
    setPaging(false);
    const controller = new AbortController();
    activeAbort.current = controller;
    if (!quiet) setLoading(true);
    setError("");
    try {
      const [page, count] = await Promise.all([
        listNotifications(organizationId,filter,undefined,controller.signal),
        notificationCount(organizationId,controller.signal)
      ]);
      if (!mounted.current || serial.current !== run) return;
      setItems(page.items);
      setCursor(page.nextCursor);
      setUnread(count.unreadCount);
      setUnreadCapped(count.unreadCountCapped === true);
    } catch (cause) {
      if (!mounted.current || serial.current !== run ||
          controller.signal.aborted) return;
      setItems([]); setCursor(null); setUnread(null); setUnreadCapped(false);
      setError(organizationError(cause));
      invalidate(cause);
    } finally {
      if (mounted.current && serial.current === run) setLoading(false);
    }
  }, [organizationId,filter,invalidate]);

  useEffect(() => {
    mounted.current = true;
    void refresh();
    const timer = window.setInterval(() => {
      if (document.visibilityState === "visible" && !document.hidden)
        void refresh(true);
    },30_000);
    return () => {
      mounted.current = false;
      serial.current++;
      activeAbort.current?.abort();
      pagingAbort.current?.abort();
      pagingAbort.current = null;
      window.clearInterval(timer);
    };
  }, [refresh]);

  const loadMore = async () => {
    if (!cursor || loading || paging || busyId || pagingAbort.current) return;
    const run = serial.current;
    const controller = new AbortController();
    pagingAbort.current = controller;
    setPaging(true); setError("");
    try {
      const page = await listNotifications(organizationId,filter,cursor,controller.signal);
      if (!mounted.current || run !== serial.current) return;
      setItems(current => {
        const seen = new Set(current.map(item => item.id));
        return [...current,...page.items.filter(item => !seen.has(item.id))];
      });
      setCursor(page.nextCursor);
    } catch (cause) {
      if (mounted.current && run === serial.current) {
        setError(organizationError(cause)); invalidate(cause);
      }
    } finally {
      // An older aborted page must never release a newer page's lock.
      if (pagingAbort.current === controller) {
        pagingAbort.current = null;
        if (mounted.current) setPaging(false);
      }
    }
  };

  const change = async (item: NotificationItem, action: "MarkRead" | "Acknowledge") => {
    if (busyId) return;
    const run = serial.current;
    setBusyId(item.id); setError("");
    try {
      const updated = await updateNotification(organizationId,item,action);
      if (!mounted.current || run !== serial.current) return;
      setItems(current => current.map(row => row.id === updated.id ? updated : row));
      await refresh(true);
    } catch (cause) {
      if (mounted.current && run === serial.current) {
        setError(cause instanceof OrganizationRequestError && cause.status === 409
          ? "This notification changed. Refresh and try again."
          : organizationError(cause));
        invalidate(cause);
      }
    } finally {
      // refresh() intentionally advances serial.current after an applied action.
      // Do not strand the mutation lock when that self-initiated refresh completes.
      // Only clear the matching operation on the currently mounted workspace.
      if (mounted.current)
        setBusyId(current => current === item.id ? null : current);
    }
  };

  return <section className="mt-5 rounded-lg border border-slate-700 p-4"
    aria-label="In-app shipment notifications">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <h4 className="text-base font-semibold">
        Shipment notifications {unread !== null ? "(" + unread + (unreadCapped ? "+" : "") + " unread)" : ""}
      </h4>
      <button type="button" onClick={() => void refresh()}
        disabled={loading || Boolean(busyId)}
        className="rounded border border-slate-500 px-3 py-1 text-sm disabled:opacity-50">
        Refresh notifications
      </button>
    </div>
    <p className="mt-1 text-xs text-slate-400">
      Based on the last recorded exception evaluation. These alerts do not confirm vessel
      movement or calculate ETA. Your read state does not change the exception.
    </p>
    <label className="mt-3 block text-xs">Show
      <select value={filter} onChange={event =>
        setFilter(event.target.value as NotificationFilter)}
        className="ml-2 rounded border border-slate-600 bg-slate-950 px-2 py-1">
        {(["All","Unread","Read","Acknowledged"] as NotificationFilter[]).map(
          value => <option key={value} value={value}>{value}</option>)}
      </select>
    </label>
    {loading && <p role="status" className="mt-3 text-sm text-slate-300">
      Loading notifications...
    </p>}
    {error && <p role="alert" className="mt-3 text-xs text-amber-300">{error}</p>}
    {!loading && items.length === 0 && !error && <p className="mt-3 text-sm text-slate-400">
      No notifications are available for this filter. This does not prove there are no exceptions.
    </p>}
    <ul className="mt-3 space-y-2">
      {items.map(item => <li key={item.id}
        className="rounded border border-slate-700 p-3 text-sm">
        <p className="font-medium">{item.title}</p>
        <p className="text-xs text-slate-400">
          {item.eventLabel} | {item.createdAtUtc} |
          {" "}{item.acknowledgedAtUtc ? "Acknowledged" :
            item.readAtUtc ? "Read" : "Unread"}
        </p>
        <div className="mt-2 flex flex-wrap gap-2">
          <button type="button" onClick={() =>
            onOpen({ shipmentId:item.shipmentId, exceptionId:item.exceptionId,
              request:++openSerial.current })}
            className="rounded border border-slate-500 px-2 py-1 text-xs">
            Open shipment exception
          </button>
          {!item.readAtUtc && <button type="button" disabled={Boolean(busyId)}
            onClick={() => void change(item,"MarkRead")}
            className="rounded border border-cyan-500 px-2 py-1 text-xs disabled:opacity-50">
            Mark read
          </button>}
          {!item.acknowledgedAtUtc && <button type="button"
            disabled={Boolean(busyId)} onClick={() => void change(item,"Acknowledge")}
            className="rounded border border-slate-600 px-2 py-1 text-xs disabled:opacity-50">
            Acknowledge notification
          </button>}
        </div>
      </li>)}
    </ul>
    {cursor && <button type="button" onClick={() => void loadMore()}
      disabled={loading || paging || Boolean(busyId)}
      className="mt-3 rounded border border-slate-600 px-3 py-1 text-sm disabled:opacity-50">
      {paging ? "Loading..." : "Load more notifications"}
    </button>}
  </section>;
}
