import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from "react";
import { organizationError, OrganizationRequestError } from "../auth/organizationClient";
import { catalog, overview, type DrillSelection, type KpiCount,
  type KpiDefinition, type KpiFilterValues, type KpiReport } from "./analyticsClient";
import KpiDrillPanel from "./KpiDrillPanel";

const initial: KpiFilterValues = {
  fromDate: "", beforeDate: "", bucket: "day",
  originPortId: "", destinationPortId: "", voyageId: "", recordSource: ""
};
const statuses = ["Draft", "Booked", "InTransit", "Arrived", "Completed", "Cancelled"];
const qualityLabels: Record<string, string> = {
  Q01: "Missing evaluation", Q02: "Stale checkpoint",
  Q03: "Unknown evaluation", Q04: "Missing or stale equipment projection"
};
const fmt = (date: string) => new Date(date).toLocaleString("en-GB",
  { year: "numeric", month: "short", day: "2-digit", hour: "2-digit",
    minute: "2-digit", timeZone: "UTC" }) + " UTC";

function count(rows: KpiCount[], code: string, dimension?: string): number {
  return rows.filter(row => row.metricCode === code &&
    (dimension === undefined || row.dimension === dimension))
    .reduce((sum, row) => sum + row.count, 0);
}
function fraction(n: number, d: number) {
  if (d === 0) return "Not enough data";
  // Numerators are exact integers; round half up at two decimal points.
  return (Math.floor((n * 100 / d + Number.EPSILON) * 100 + 0.5) / 100).toFixed(2) + "%";
}

type Props = {
  organizationId: string;
  onAccessInvalidated: (message: string) => void;
};

export default function OperationalDashboard({ organizationId, onAccessInvalidated, isAuditor }: Props & { isAuditor: boolean }) {
  const [draft, setDraft] = useState<KpiFilterValues>(initial);
  const [applied, setApplied] = useState<KpiFilterValues>(initial);
  const [definitions, setDefinitions] = useState<KpiDefinition[]>([]);
  const [report, setReport] = useState<KpiReport | null>(null);
  const [selection, setSelection] = useState<DrillSelection | null>(null);
  const [allowed, setAllowed] = useState<boolean | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const abort = useRef<AbortController | null>(null);
  const serial = useRef(0);

  const denied = useCallback((cause: unknown) => {
    if (cause instanceof OrganizationRequestError &&
      (cause.status === 401 || cause.status === 403 ||
        cause.code === "organization_selection_required"))
      onAccessInvalidated(organizationError(cause));
  }, [onAccessInvalidated]);

  useEffect(() => {
    const controller = new AbortController();
    void catalog(organizationId, controller.signal).then(result => {
      if (!controller.signal.aborted) { setDefinitions(result.definitions); setAllowed(true); }
    }).catch(cause => {
      if (controller.signal.aborted) return;
      if (cause instanceof OrganizationRequestError && cause.code === "access_denied") {
        setAllowed(false);
      } else {
        setError(organizationError(cause));
        denied(cause);
      }
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [organizationId, denied]);

  const refresh = useCallback(async (filters: KpiFilterValues) => {
    abort.current?.abort();
    const controller = new AbortController();
    abort.current = controller;
    const id = ++serial.current;
    setBusy(true); setError(""); setSelection(null); setReport(null);
    try {
      const result = await overview(organizationId, filters, controller.signal);
      if (!controller.signal.aborted && id === serial.current) setReport(result);
    } catch (cause) {
      if (controller.signal.aborted || id !== serial.current) return;
      setError(organizationError(cause));
      denied(cause);
    } finally {
      if (id === serial.current) setBusy(false);
      if (abort.current === controller) abort.current = null;
    }
  }, [organizationId, denied]);

  useEffect(() => {
    if (allowed !== true) return;
    void refresh(applied);
    return () => { abort.current?.abort(); serial.current++; };
  }, [allowed, applied, refresh]);

  function apply(event: FormEvent) {
    event.preventDefault();
    setApplied({ ...draft });
  }
  const figures = useMemo(() => {
    const rows = report?.counts ?? [];
    return Object.fromEntries([...definitions.map(d => d.code), "D01", "D05", "D07", "D08", "D09", "D10", "D13",
      ...Object.keys(qualityLabels)].map(code => [code, count(rows, code)]));
  }, [report, definitions]);
  const denominator = (code: string) => figures[code] ?? 0;
  const partial = report !== null && (denominator("K14") > 0 ||
    denominator("K13") < denominator("D13"));
  const exceptionTrend = useMemo(() =>
    (report?.counts ?? []).filter(c => c.metricCode === "K12").sort((a, b) =>
      (a.bucketStartUtc ?? "").localeCompare(b.bucketStartUtc ?? "") ||
      a.dimension.localeCompare(b.dimension)), [report]);

  function select(metricCode: string, label: string, dimension = "", bucketStartUtc: string | null = null) {
    setSelection({ metricCode, dimension, bucketStartUtc, label });
  }

  return <section aria-label="Operational KPI dashboard" className="mt-6 rounded-xl border border-slate-700 bg-slate-900/80 p-4 sm:p-6">
    <div className="flex flex-wrap items-center justify-between gap-2">
      <div><h3 className="text-xl font-semibold">Operational KPIs</h3>
        <p className="mt-1 text-xs text-slate-400">Versioned source-reconciled reporting | m13-kpi-v1</p></div>
      <button type="button" onClick={() => void refresh(applied)} disabled={busy || allowed !== true}
        className="rounded-lg border border-cyan-500 px-3 py-2 text-sm disabled:opacity-50">Refresh report</button>
    </div>
    {loading && <p className="mt-3 text-sm" role="status">Checking dashboard permissions...</p>}
    {allowed === false && <p className="mt-3 rounded border border-amber-700 p-3 text-sm text-amber-200">
      Dashboard access requires an active explicit read-only grant for an Auditor.
    </p>}
    {allowed === true && <>
      <form onSubmit={apply} className="mt-5 grid grid-cols-1 gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
        <label>From (UTC, inclusive)
          <input type="date" value={draft.fromDate} onChange={e => setDraft(p => ({ ...p, fromDate: e.target.value }))}
            className="mt-1 block w-full rounded border border-slate-600 bg-slate-950 p-2" />
        </label>
        <label>Before (UTC, exclusive)
          <input type="date" value={draft.beforeDate} onChange={e => setDraft(p => ({ ...p, beforeDate: e.target.value }))}
            className="mt-1 block w-full rounded border border-slate-600 bg-slate-950 p-2" />
        </label>
        <label>Trend bucket (UTC)
          <select value={draft.bucket} onChange={e => setDraft(p => ({ ...p, bucket: e.target.value as "day" | "week" }))}
            className="mt-1 block w-full rounded border border-slate-600 bg-slate-950 p-2">
            <option value="day">Daily</option><option value="week">Weekly</option>
          </select>
        </label>
        <label>Record source
          <select value={draft.recordSource} onChange={e => setDraft(p => ({ ...p, recordSource: e.target.value as KpiFilterValues["recordSource"] }))}
            className="mt-1 block w-full rounded border border-slate-600 bg-slate-950 p-2">
            <option value="">All sources</option><option value="UserProvided">User provided</option>
            <option value="Synthetic">Synthetic</option>
          </select>
        </label>
        {(["originPortId", "destinationPortId", "voyageId"] as const).map(key =>
          <label key={key}>{key === "voyageId" ? "Voyage ID" : key === "originPortId" ? "Origin port ID" : "Destination port ID"}
            <input type="text" value={draft[key]} placeholder="Optional UUID"
              onChange={e => setDraft(p => ({ ...p, [key]: e.target.value }))}
              className="mt-1 block w-full rounded border border-slate-600 bg-slate-950 p-2" />
          </label>)}
        <div className="flex items-end gap-2">
          <button disabled={busy} className="rounded bg-cyan-300 px-4 py-2 font-semibold text-slate-950 disabled:opacity-50">Apply filters</button>
          <button type="button" onClick={() => { setDraft(initial); setApplied({ ...initial }); }}
            className="rounded border border-slate-600 px-3 py-2">Reset</button>
        </div>
      </form>
      <p className="mt-2 text-xs text-slate-400">Leave both dates blank for the previous seven complete UTC days. Current risk values are evaluated when the report is built, not reconstructed for the date range.</p>
      {busy && <p className="mt-4 text-sm" role="status">Building an isolated report snapshot...</p>}
      {error && <p className="mt-4 rounded border border-rose-700 p-3 text-sm text-rose-200" role="alert">{error}</p>}
      {report && <>
        <div className="mt-5 border-y border-slate-700 py-3 text-xs text-slate-400">
          <p>Period: {fmt(report.fromUtc)} to {fmt(report.beforeUtc)} (end exclusive)</p>
          <p>Current-state evaluation: {fmt(report.evaluatedAtUtc)} | Members: {report.memberCount.toLocaleString()}</p>
          <p>Snapshot expires: {fmt(report.expiresAtUtc)}. Refresh creates a new report and replaces old drill membership.</p>
        </div>
        <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {definitions.filter(d => d.code !== "K02" && d.code !== "K12").map(d => {
            const n = denominator(d.code); const den = d.denominatorCode ? denominator(d.denominatorCode) : null;
            const hasEvaluatedRisk = !["K07", "K08", "K09", "K10"].includes(d.code) ||
              den !== 0;
            const value = !hasEvaluatedRisk && denominator("D13") > 0
              ? "Not assessed"
              : den !== null && ["K05", "K06", "K13"].includes(d.code)
                ? fraction(n, den) : n.toLocaleString();
            return <button type="button" key={d.code} onClick={() => select(d.code, d.label)}
              className="rounded-lg border border-slate-700 bg-slate-950/80 p-4 text-left hover:border-cyan-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-cyan-300">
              <span className="text-xs uppercase tracking-wide text-slate-400">{d.label}</span>
              <span className="mt-2 block text-2xl font-semibold text-cyan-200">{value}</span>
              {den !== null && <span className="mt-1 block text-xs text-slate-400">{n.toLocaleString()} / {den.toLocaleString()} {["K07","K08","K09","K10"].includes(d.code) ? "evaluated" : ""}</span>}
              {partial && ["K07", "K08", "K09", "K10", "K11"].includes(d.code) &&
                <span className="mt-1 block text-xs text-amber-300">Partial assessment. Zero is not confirmed risk-free.</span>}
              <span className="mt-2 block text-xs text-slate-500">{d.description}</span>
            </button>;
          })}
        </div>
        <section className="mt-6">
          <h4 className="font-semibold">Current workflow distribution</h4>
          <p className="mt-1 text-xs text-slate-400">Unarchived shipments only. Total {denominator("D01").toLocaleString()}.</p>
          <div className="mt-3 grid gap-2 sm:grid-cols-3">
            {statuses.map(status => {
              const n = count(report.counts, "K02", status);
              return <button key={status} onClick={() => select("K02", "Workflow: " + status, status)}
                className="rounded border border-slate-700 bg-slate-950 p-3 text-left text-sm hover:border-cyan-500">
                <span>{status}</span><span className="float-right font-semibold">{n}</span>
                {/* An SVG bar: its width is an attribute, so the page's style-src 'self'
                    policy (no inline styles) never blocks it in any engine. */}
                <svg className="mt-2 block h-1.5 w-full rounded" viewBox="0 0 100 1"
                  preserveAspectRatio="none" aria-hidden="true" focusable="false">
                  <rect width="100" height="1" className="fill-slate-800" />
                  <rect width={denominator("D01") ? Math.min(100, Math.max(0, n / denominator("D01") * 100)).toFixed(2) : "0"}
                    height="1" className="fill-cyan-400" />
                </svg>
              </button>;
            })}
          </div>
        </section>
        <section className="mt-6">
          <h4 className="font-semibold">Assessment quality</h4>
          <p className="mt-2 text-sm text-slate-400">
            {denominator("K14")} distinct eligible shipments have missing, stale or unknown evidence.
            {partial ? " Risk figures are not complete." : " Current assessment coverage is complete for eligible shipments."}
          </p>
          <dl className="mt-3 grid gap-2 sm:grid-cols-2">
            {Object.entries(qualityLabels).map(([code, label]) =>
              <div key={code} className="rounded border border-slate-700 p-3 text-sm">
                <dt className="text-slate-400">{label}</dt><dd className="mt-1 font-semibold">{denominator(code)}</dd>
              </div>)}
          </dl>
          <p className="mt-2 text-xs text-slate-500">An individual shipment may have more than one evidence-quality reason.</p>
          <p className="mt-2 text-xs text-slate-400">Completed workflows excluded from arrival percentage: {Math.max(0, denominator("K03") - denominator("D05"))} (missing authoritative plan or recorded Arrived event).</p>
        </section>
        <section className="mt-6">
          <h4 className="font-semibold">Exception occurrence openings</h4>
          <p className="mt-1 text-xs text-slate-400">Recorded openings by UTC {report.bucket}, not reconstructed historical open status.</p>
          {exceptionTrend.length === 0 && <p className="mt-3 text-sm text-slate-400">No recorded openings in this window.</p>}
          <div className="mt-3 grid gap-2 sm:grid-cols-2">
            {exceptionTrend.map(row => <button
              key={row.dimension + row.bucketStartUtc} type="button"
              onClick={() => select("K12", row.dimension + " openings", row.dimension, row.bucketStartUtc)}
              className="rounded border border-slate-700 bg-slate-950 p-3 text-left text-sm hover:border-cyan-500">
              <span className="block text-slate-400">{row.bucketStartUtc ? fmt(row.bucketStartUtc) : "Unknown bucket"}</span>
              <span className="mt-1 block">{row.dimension}: <strong>{row.count}</strong></span>
            </button>)}
          </div>
        </section>
        {selection && <KpiDrillPanel key={report.reportId + selection.metricCode +
          selection.dimension + (selection.bucketStartUtc ?? "")}
          organizationId={organizationId} report={report} selection={selection}
          isAuditor={isAuditor}
          onAccessInvalidated={onAccessInvalidated}
          onClose={() => setSelection(null)} />}
      </>}
    </>}
  </section>;
}