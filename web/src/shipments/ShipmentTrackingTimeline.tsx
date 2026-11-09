import { useI18n } from "../i18n/useI18n";
import { useCallback, useEffect, useRef, useState } from "react";
import { getJson, organizationError, OrganizationRequestError } from "../auth/organizationClient";

type EventRow = {
  eventId: string; receiptId: string; sourceEventId: string; eventCode: string;
  occurredAtUtc: string; receivedAtUtc: string; superseded: boolean;
  containerId: string;
};
type Page = { items: EventRow[]; nextCursor: string | null; eventCount: number };
type Projection = { observedTrackingState: string; lastOccurredAtUtc: string | null;
  materializationRevision: number; materializationStale: boolean; eventCount: number;
  snapshotToken: string; containerCount: number; containerObservations: {
    containerId: string; observedState: string; occurredAtUtc: string;
  }[]; containerObservationsTruncated: boolean };

export default function ShipmentTrackingTimeline({ organizationId, shipmentId, onAccessInvalidated }: {
  organizationId: string; shipmentId: string; onAccessInvalidated: (message: string) => void;
}) {
  const { t, date, number } = useI18n();
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
      const projectionResult = await getJson<Projection>(path + "/tracking-projection");
      if (serial.current !== run) return;
      const page = await getJson<Page>(path + "/events?limit=25&snapshot=" +
        encodeURIComponent(projectionResult.snapshotToken));
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
      <h6 className="font-medium">{t("Tracking observations")}</h6>
      <button type="button" disabled={loading || paging} onClick={() => void refresh()}
        className="rounded border border-slate-500 px-2 py-1 text-xs">
        {t(loading ? "Loading..." : "Refresh tracking")}
      </button>
    </div>
    <p className="mt-1 text-xs text-slate-400">{t("Derived equipment observations are separate from the manual shipment workflow. No ETA can be inferred from this source profile.")}</p>
    {error && <p role="alert" className="mt-2 text-xs text-amber-300">{t(error)}</p>}
    {projection && <>
      <p className="mt-2 text-sm">{t("Observed:")} <strong>{t(projection.observedTrackingState)}</strong>
        <span className="ml-2 text-xs text-slate-400">
          {projection.lastOccurredAtUtc ? date(projection.lastOccurredAtUtc) + " UTC" : t("No resolved event")}
        </span></p>
      <p className="text-xs text-slate-400">{t("Materialization:")} {projection.materializationRevision ? number(projection.materializationRevision) : t("not yet materialized")}
        {projection.materializationStale ? t(" (rebuild pending)") : ""}
      </p>
      {projection.containerObservations.length > 0 && <div className="mt-2 text-xs">
        <strong>{t("Container observations (")}{number(projection.containerCount)})</strong>
        <ul className="mt-1 space-y-1">{projection.containerObservations.map(item =>
          <li key={item.containerId}>{item.containerId}: {t(item.observedState)}</li>)}</ul>
        {projection.containerObservationsTruncated &&
          <span>{t("Additional container observations not shown.")}</span>}
      </div>}
      <h6 className="mt-3 font-medium text-sm">{t("Resolved event timeline")}</h6>
      {events.length === 0 ? <p className="mt-1 text-xs text-slate-400">{t("No resolved tracking events. Reconciliation may still be pending.")}</p> : <ol className="mt-2 space-y-2 text-xs">{events.map(x =>
        <li key={x.eventId} className="rounded border border-slate-700 p-2">
          <strong>{t(x.eventCode)}</strong>
          {x.superseded && <span className="ml-2 text-amber-300">{t("Corrected")}</span>}
          <div>{t("Container:")} {x.containerId}</div>
          <div>{t("Occurred:")} {date(x.occurredAtUtc)} UTC</div>
          <div className="text-slate-400">{t("Received:")} {date(x.receivedAtUtc)} UTC</div>
          <div className="text-slate-400">{t("Source event:")} {x.sourceEventId}</div>
        </li>)}</ol>}
      {cursor && <button type="button" disabled={paging || loading}
        onClick={() => void loadMore()}
        className="mt-3 rounded border border-slate-600 px-3 py-2 text-xs">
        {t(paging ? "Loading..." : "Load more events")}
      </button>}
    </>}
  </section>;
}
