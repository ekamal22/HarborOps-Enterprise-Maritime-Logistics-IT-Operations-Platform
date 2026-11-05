import { useCallback, useEffect, useRef, useState } from "react";
import { getJson, organizationError, OrganizationRequestError } from "../auth/organizationClient";

type EventRow = {
  eventId: string; receiptId: string; sourceEventId: string; eventCode: string;
  occurredAtUtc: string; receivedAtUtc: string; superseded: boolean;
};
type Page = { items: EventRow[]; nextCursor: string | null; eventCount: number };
type Projection = { observedTrackingState: string; lastOccurredAtUtc: string | null;
  materializationRevision: number; materializationStale: boolean; eventCount: number };

export default function ShipmentTrackingTimeline({ organizationId, shipmentId, onAccessInvalidated }: {
  organizationId: string; shipmentId: string; onAccessInvalidated: (message: string) => void;
}) {
  const path = "/api/v1/organizations/" + encodeURIComponent(organizationId) +
    "/shipments/" + encodeURIComponent(shipmentId);
  const [events, setEvents] = useState<EventRow[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [projection, setProjection] = useState<Projection | null>(null);
  const [loading, setLoading] = useState(false);
  const [paging, setPaging] = useState(false);
  const [error, setError] = useState("");
  const serial = useRef(0);
  const invalidate = useCallback((cause: unknown) => {
    if (cause instanceof OrganizationRequestError &&
        (cause.status === 401 || cause.code === "access_denied" ||
         cause.code === "organization_selection_required" ||
         cause.code === "organization_access_revoked"))
      onAccessInvalidated(organizationError(cause));
  }, [onAccessInvalidated]);
  const refresh = useCallback(async () => {
    const run = ++serial.current;
    setLoading(true); setPaging(false); setError("");
    try {
      const [projectionResult, page] = await Promise.all([
        getJson<Projection>(path + "/tracking-projection"),
        getJson<Page>(path + "/events?limit=25")
      ]);
      if (serial.current !== run) return;
      setProjection(projectionResult); setEvents(page.items); setCursor(page.nextCursor);
    } catch (cause) {
      if (serial.current !== run) return;
      setProjection(null); setEvents([]); setCursor(null);
      setError(organizationError(cause)); invalidate(cause);
    } finally { if (serial.current === run) setLoading(false); }
  }, [path, invalidate]);
  useEffect(() => { void refresh(); return () => { serial.current++; }; }, [refresh]);
  const loadMore = async () => {
    if (!cursor || paging || loading) return;
    const run = serial.current;
    setPaging(true); setError("");
    try {
      const page = await getJson<Page>(path + "/events?limit=25&cursor=" +
        encodeURIComponent(cursor));
      if (serial.current !== run) return;
      setEvents(old => {
        const seen = new Set(old.map(x => x.receiptId));
        return [...old, ...page.items.filter(x => !seen.has(x.receiptId))];
      });
      setCursor(page.nextCursor);
    } catch (cause) {
      if (serial.current !== run) return;
      setError(cause instanceof OrganizationRequestError &&
        cause.code === "stale_or_invalid_cursor"
        ? "Events changed. Refresh to restart the timeline."
        : organizationError(cause));
      invalidate(cause);
    } finally { if (serial.current === run) setPaging(false); }
  };
  return <section className="mt-4 rounded border border-slate-700 p-3">
    <div className="flex flex-wrap items-center justify-between gap-2">
      <h6 className="font-medium">Tracking observations</h6>
      <button type="button" disabled={loading || paging} onClick={() => void refresh()}
        className="rounded border border-slate-500 px-2 py-1 text-xs">
        {loading ? "Loading..." : "Refresh tracking"}
      </button>
    </div>
    <p className="mt-1 text-xs text-slate-400">
      Derived equipment observations are separate from the manual shipment workflow.
      No ETA can be inferred from this source profile.
    </p>
    {error && <p role="alert" className="mt-2 text-xs text-amber-300">{error}</p>}
    {projection && <>
      <p className="mt-2 text-sm">Observed: <strong>{projection.observedTrackingState}</strong>
        <span className="ml-2 text-xs text-slate-400">
          {projection.lastOccurredAtUtc || "No resolved event"}
        </span></p>
      <p className="text-xs text-slate-400">
        Materialization: {projection.materializationRevision || "not yet materialized"}
        {projection.materializationStale ? " (rebuild pending)" : ""}
      </p>
      <h6 className="mt-3 font-medium text-sm">Resolved event timeline</h6>
      {events.length === 0 ? <p className="mt-1 text-xs text-slate-400">
        No resolved tracking events. Reconciliation may still be pending.
      </p> : <ol className="mt-2 space-y-2 text-xs">{events.map(x =>
        <li key={x.eventId} className="rounded border border-slate-700 p-2">
          <strong>{x.eventCode}</strong>
          {x.superseded && <span className="ml-2 text-amber-300">Corrected</span>}
          <div>Occurred: {x.occurredAtUtc}</div>
          <div className="text-slate-400">Received: {x.receivedAtUtc}</div>
          <div className="text-slate-400">Source event: {x.sourceEventId}</div>
        </li>)}</ol>}
      {cursor && <button type="button" disabled={paging || loading}
        onClick={() => void loadMore()}
        className="mt-3 rounded border border-slate-600 px-3 py-2 text-xs">
        {paging ? "Loading..." : "Load more events"}
      </button>}
    </>}
  </section>;
}
