import { useI18n } from "../i18n/useI18n";
import { useCallback, useEffect, useRef, useState } from "react";
import { getJson, mutate, organizationError, OrganizationRequestError } from "../auth/organizationClient";

type ExceptionRow = {
  id: string; organizationId: string; shipmentId: string; ruleCode: string;
  occurrence: number; condition: "Active" | "Inactive" | "Unknown" | "Stale";
  state: "Open" | "Acknowledged" | "Resolved"; severity: string; confidence: string;
  reasonCode: string; sourceFingerprint: string;
  referenceAtUtc: string | null; thresholdAtUtc: string | null;
  firstDetectedAtUtc: string; lastEvaluatedAtUtc: string;
  resolvedAtUtc: string | null; version: number;
  evidence: { rulePolicyVersion: string; trackingInputDigest: string | null;
    trackingInputRevision: number; correctionGeneration: number };
};
type ExceptionPage = { items: ExceptionRow[]; nextCursor: string | null };
type ExceptionHistory = {
  id: string; action: string; oldState: string | null; newState: string;
  actorKind: string; occurredAtUtc: string; reason: string | null;
};
type ExceptionDetail = {
  exception: ExceptionRow; history: ExceptionHistory[];
  historyTruncated: boolean; nextHistoryCursor: string | null;
};
type HistoryPage = { items: ExceptionHistory[]; nextCursor: string | null };

const descriptions: Record<string, string> = {
  EquipmentConflict: "Conflicting recorded equipment observations",
  ArrivalScheduleOverdue: "Planned arrival is overdue; physical arrival is unconfirmed",
  RecordedArrivalLate: "Arrival was recorded in the manual workflow after the grace period",
  DepartureSlippageRisk: "Recorded workflow departure suggests a schedule risk"
};

function revoked(cause: unknown): boolean {
  return cause instanceof OrganizationRequestError &&
    (cause.status === 401 || cause.code === "access_denied" ||
      cause.code === "organization_access_revoked" ||
      cause.code === "organization_selection_required");
}

export default function ShipmentExceptionPanel({ organizationId, shipmentId, isManager,
  focusExceptionId, focusRequest, onAccessInvalidated }: {
  organizationId: string; shipmentId: string; isManager: boolean;
  focusExceptionId?: string; focusRequest?: number;
  onAccessInvalidated: (message: string) => void;
}) {
  const { t, date, number } = useI18n();
  const root = "/api/v1/organizations/" + encodeURIComponent(organizationId);
  const shipmentRoot = root + "/shipments/" + encodeURIComponent(shipmentId);
  const [rows, setRows] = useState<ExceptionRow[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [detail, setDetail] = useState<ExceptionDetail | null>(null);
  const [reasons, setReasons] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [paging, setPaging] = useState(false);
  const [historyPaging, setHistoryPaging] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const serial = useRef(0);
  const detailSerial = useRef(0);

  const invalidate = useCallback((cause: unknown) => {
    if (revoked(cause)) onAccessInvalidated(organizationError(cause));
  }, [onAccessInvalidated]);

  const refresh = useCallback(async () => {
    const run = ++serial.current;
    detailSerial.current++;
    setLoading(true); setError(""); setDetail(null); setCursor(null);
    setHistoryPaging(false);
    try {
      const page = await getJson<ExceptionPage>(shipmentRoot + "/exceptions?limit=25");
      if (run !== serial.current) return;
      setRows(page.items); setCursor(page.nextCursor);
    } catch (cause) {
      if (run !== serial.current) return;
      setRows([]); setError(organizationError(cause)); invalidate(cause);
    } finally {
      if (run === serial.current) setLoading(false);
    }
  }, [shipmentRoot, invalidate]);

  useEffect(() => {
    void refresh();
    return () => { serial.current++; detailSerial.current++; };
  }, [refresh]);

  const more = async () => {
    if (!cursor || paging || loading) return;
    const run = serial.current;
    setPaging(true); setError("");
    try {
      const page = await getJson<ExceptionPage>(shipmentRoot +
        "/exceptions?limit=25&cursor=" + encodeURIComponent(cursor));
      if (run !== serial.current) return;
      setRows(current => [...current,
        ...page.items.filter(x => !current.some(y => y.id === x.id))]);
      setCursor(page.nextCursor);
    } catch (cause) {
      if (run !== serial.current) return;
      setError(organizationError(cause)); invalidate(cause);
    } finally { if (run === serial.current) setPaging(false); }
  };

  const evaluate = async () => {
    if (!isManager || busy) return;
    setBusy(true); setError(""); setMessage("");
    try {
      await mutate<{ rulesEvaluated: number }>("POST", shipmentRoot + "/exceptions/evaluate");
      setMessage("Evaluation completed against the current accepted records.");
      await refresh();
    } catch (cause) {
      setError(organizationError(cause)); invalidate(cause);
    } finally { setBusy(false); }
  };

  const showDetail = async (id: string) => {
    setError(""); setDetail(null); setHistoryPaging(false);
    const run = serial.current;
    const detailRun = ++detailSerial.current;
    try {
      const result = await getJson<ExceptionDetail>(root + "/exceptions/" +
        encodeURIComponent(id));
      if (run === serial.current && detailRun === detailSerial.current)
        setDetail(result);
    } catch (cause) {
      if (run === serial.current && detailRun === detailSerial.current) {
        setError(organizationError(cause));
        invalidate(cause);
      }
    }
  };

  useEffect(() => {
    if (focusExceptionId) void showDetail(focusExceptionId);
  }, [focusExceptionId, focusRequest]);

  const moreHistory = async () => {
    if (!detail?.nextHistoryCursor || historyPaging || loading) return;
    const run = serial.current;
    const detailRun = detailSerial.current;
    const exceptionId = detail.exception.id;
    setHistoryPaging(true); setError("");
    try {
      const page = await getJson<HistoryPage>(root + "/exceptions/" +
        encodeURIComponent(exceptionId) + "/history?limit=100&cursor=" +
        encodeURIComponent(detail.nextHistoryCursor));
      if (run !== serial.current || detailRun !== detailSerial.current) return;
      setDetail(previous => {
        if (!previous || previous.exception.id !== exceptionId) return previous;
        const ids = new Set(previous.history.map(x => x.id));
        return {
          ...previous,
          history: [...previous.history, ...page.items.filter(x => !ids.has(x.id))],
          nextHistoryCursor: page.nextCursor,
          historyTruncated: page.nextCursor !== null
        };
      });
    } catch (cause) {
      if (run === serial.current && detailRun === detailSerial.current) {
        setError(organizationError(cause)); invalidate(cause);
      }
    } finally {
      if (run === serial.current && detailRun === detailSerial.current)
        setHistoryPaging(false);
    }
  };

  const act = async (item: ExceptionRow, action: "Acknowledge" | "Resolve") => {
    const reason = reasons[item.id] ?? "";
    if (!isManager || busy || reason.trim().length < 8 || reason.trim().length > 240) return;
    setBusy(true); setError(""); setMessage("");
    try {
      await mutate<{ result: string; version: number }>(
        "PATCH", root + "/exceptions/" + encodeURIComponent(item.id),
        { action, expectedVersion: item.version, reason: reason.trim() },
        { "Idempotency-Key": crypto.randomUUID() });
      setReasons(old => {
        const next = { ...old };
        delete next[item.id];
        return next;
      });
      setMessage("Exception disposition saved.");
      await refresh();
    } catch (cause) {
      setError(cause instanceof OrganizationRequestError && cause.status === 409
        ? "The exception changed. Refresh and inspect the latest version before retrying."
        : organizationError(cause));
      invalidate(cause);
    } finally { setBusy(false); }
  };

  return <section className="mt-4 rounded border border-slate-700 p-3"
    aria-label="Shipment exceptions">
    <div className="flex flex-wrap items-center justify-between gap-2">
      <h6 className="font-medium">Shipment exceptions</h6>
      <div className="flex flex-wrap gap-2">
        <button type="button" disabled={loading || busy || paging} onClick={() => void refresh()}
          className="rounded border border-slate-500 px-2 py-1 text-xs">Refresh exceptions</button>
        {isManager && <button type="button" disabled={loading || busy}
          onClick={() => void evaluate()}
          className="rounded border border-cyan-500 px-2 py-1 text-xs disabled:opacity-50">
          Reevaluate rules
        </button>}
      </div>
    </div>
    <p className="mt-1 text-xs text-slate-400">
      These rule outcomes use manual workflow records and accepted equipment observations.
      Schedule overdue does not establish a physical delay, and no ETA is calculated.
      Results reflect the last evaluation and may be stale until reevaluated.
      Exception status is live and can change while you move between pages.
    </p>
    {loading && <p role="status" className="mt-2 text-xs">Loading exceptions...</p>}
    {message && <p role="status" className="mt-2 text-xs text-cyan-300">{message}</p>}
    {error && <p role="alert" className="mt-2 text-xs text-amber-300">{error}</p>}
    {!loading && rows.length === 0 && <p className="mt-2 text-xs text-slate-400">
      No recorded exception occurrences. This does not prove that no risk exists.
    </p>}
    <ul className="mt-3 space-y-2">{rows.map(item =>
      <li key={item.id} className="rounded border border-slate-700 p-3 text-xs">
        <strong>{descriptions[item.ruleCode] ?? item.ruleCode}</strong>
        <span className="ml-2">{item.condition} / {item.state}</span>
        <p className="mt-1 text-slate-400">Severity: {item.severity} | Evidence: {item.confidence}</p>
        <p className="text-slate-400">Reason: {item.reasonCode}</p>
        {item.ruleCode === "RecordedArrivalLate" && item.state === "Resolved" &&
          item.condition === "Active" && <p className="mt-1 text-slate-400">
            Historical recorded lateness remains in the evidence after Manager review.
            Resolution closes this occurrence, not the underlying workflow record.
          </p>}
        <p className="text-slate-400">Evaluated: {item.lastEvaluatedAtUtc}</p>
        {item.thresholdAtUtc && <p className="text-slate-400">
          Rule threshold: {item.thresholdAtUtc}
        </p>}
        <div className="mt-2 flex flex-wrap gap-2">
          <button type="button" onClick={() => void showDetail(item.id)}
            className="rounded border border-slate-500 px-2 py-1">View history</button>
          {isManager && item.state !== "Resolved" && <>
            <label className="sr-only" htmlFor={"reason-" + item.id}>Disposition reason</label>
            <input id={"reason-" + item.id} value={reasons[item.id] ?? ""} maxLength={240}
              onChange={e => setReasons(old => ({ ...old, [item.id]: e.target.value }))}
              placeholder="Disposition reason (8 to 240 characters)"
              className="min-w-60 flex-1 rounded bg-slate-950 px-2 py-1" />
            {item.state === "Open" && item.condition === "Active" &&
              <button type="button" disabled={busy || (reasons[item.id] ?? "").trim().length < 8}
                onClick={() => void act(item, "Acknowledge")}
                className="rounded border border-cyan-500 px-2 py-1 disabled:opacity-50">
                Acknowledge
              </button>}
            {item.state === "Acknowledged" && (item.condition === "Inactive" ||
              (item.ruleCode === "RecordedArrivalLate" && item.condition === "Active")) &&
              <button type="button" disabled={busy || (reasons[item.id] ?? "").trim().length < 8}
                onClick={() => void act(item, "Resolve")}
                className="rounded border border-slate-500 px-2 py-1 disabled:opacity-50">
                Resolve
              </button>}
          </>}
        </div>
      </li>)}</ul>
    {cursor && <button type="button" disabled={paging || busy || loading}
      onClick={() => void more()} className="mt-3 rounded border border-slate-500 px-2 py-1 text-xs">
      {paging ? "Loading..." : "Load more exceptions"}
    </button>}
    {detail && <div className="mt-3 rounded bg-slate-900 p-3 text-xs">
      <div className="flex items-center justify-between">
        <strong>Exception #{detail.exception.occurrence} history</strong>
        <button type="button" onClick={() => {
          detailSerial.current++;
          setDetail(null);
          setHistoryPaging(false);
        }} className="rounded border border-slate-500 px-2 py-1">{t("Close")}</button>
      </div>
      {detail.nextHistoryCursor && <p role="status" className="mt-2 text-amber-300">
        Showing {detail.history.length} history entries. Additional retained records are available.
      </p>}
      <p className="mt-2 text-slate-400">
        Rule policy: {detail.exception.evidence.rulePolicyVersion};
        input revision: {detail.exception.evidence.trackingInputRevision};
        correction generation: {detail.exception.evidence.correctionGeneration}
      </p>
      {detail.exception.evidence.trackingInputDigest && <p className="mt-1 break-all text-slate-400">
        M10 projection input digest: {detail.exception.evidence.trackingInputDigest}
      </p>}
      <ol className="mt-2 space-y-1">{detail.history.map(entry =>
        <li key={entry.id}>{entry.occurredAtUtc}: {entry.action}
          {entry.reason ? " | " + entry.reason : ""}
        </li>)}</ol>
      {detail.nextHistoryCursor && <button type="button" disabled={historyPaging || loading}
        onClick={() => void moreHistory()}
        className="mt-3 rounded border border-slate-500 px-2 py-1 disabled:opacity-50">
        {historyPaging ? "Loading history..." : "Load more history"}
      </button>}
    </div>}
  </section>;
}
