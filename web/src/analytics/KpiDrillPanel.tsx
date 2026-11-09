import { useI18n } from "../i18n/useI18n";
import { useEffect, useState } from "react";
import { organizationError, OrganizationRequestError, getJson } from "../auth/organizationClient";
import { shipmentDetail, type ShipmentRow } from "../shipments/shipmentClient";
import { members, type DrillSelection, type KpiMember, type KpiReport } from "./analyticsClient";

type Props = {
  organizationId: string; report: KpiReport; selection: DrillSelection; isAuditor: boolean;
  onAccessInvalidated: (message: string) => void; onClose: () => void;
};

export default function KpiDrillPanel({
  organizationId, report, selection, isAuditor, onAccessInvalidated, onClose
}: Props) {
  const { t, date, number, locale } = useI18n();
  const [rows, setRows] = useState<KpiMember[]>([]);
  const [after, setAfter] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [pageLoading, setPageLoading] = useState(false);
  const [error, setError] = useState("");
  const [detail, setDetail] = useState<ShipmentRow | null>(null);
  const [exceptionState, setExceptionState] = useState("");
  const [detailError, setDetailError] = useState("");
  const [detailBusy, setDetailBusy] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    setRows([]); setAfter(null); setError(""); setDetail(null);
    setExceptionState(""); setLoading(true);
    void members(organizationId, report, selection, null, controller.signal)
      .then(page => {
        if (!controller.signal.aborted) { setRows(page.items); setAfter(page.nextAfter); }
      }).catch(cause => {
        if (controller.signal.aborted) return;
        setError(organizationError(cause));
        if (cause instanceof OrganizationRequestError &&
          (cause.status === 401 || cause.status === 403 ||
            cause.code === "organization_selection_required"))
          onAccessInvalidated(organizationError(cause));
      }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [organizationId, report, selection, onAccessInvalidated]);

  async function next() {
    if (!after || pageLoading) return;
    setPageLoading(true); setError("");
    try {
      const page = await members(organizationId, report, selection, after);
      setRows(existing => [...existing, ...page.items]);
      setAfter(page.nextAfter);
    } catch (cause) {
      setError(organizationError(cause));
      if (cause instanceof OrganizationRequestError &&
        (cause.status === 401 || cause.status === 403))
        onAccessInvalidated(organizationError(cause));
    } finally { setPageLoading(false); }
  }

  async function open(row: KpiMember) {
    setDetailBusy(true); setDetailError(""); setDetail(null); setExceptionState("");
    try {
      const latest = await shipmentDetail(organizationId, row.shipmentId);
      setDetail(latest);
      if (row.exceptionId && !isAuditor) {
        const item = await getJson<{ exception: {
          lifecycleState: string; conditionState: string; ruleCode: string;
        } }>("/api/v1/organizations/" + encodeURIComponent(organizationId) +
          "/exceptions/" + encodeURIComponent(row.exceptionId));
        setExceptionState(item.exception.ruleCode + ": " +
          item.exception.conditionState + ", " + item.exception.lifecycleState);
      }
    } catch (cause) {
      setDetailError(organizationError(cause));
      if (cause instanceof OrganizationRequestError &&
        (cause.status === 401 || cause.status === 403))
        onAccessInvalidated(organizationError(cause));
    } finally { setDetailBusy(false); }
  }

  return <section aria-label={t("KPI report members")} className="mt-5 rounded-xl border border-cyan-900 bg-slate-950 p-4">
    <div className="flex items-center justify-between gap-2">
      <div>
        <h4 className="text-base font-semibold">{selection.metricCode === "K02" ? t("Workflow: ") + t(selection.dimension) : selection.metricCode === "K12" ? t(selection.dimension) + t(" openings") : t(selection.label)} {t("members")}</h4>
        <p className="mt-1 text-xs text-slate-400">{t("Frozen report membership, evaluated")} {date(report.evaluatedAtUtc, {timeZone: "UTC"})} UTC</p>
      </div>
      <button onClick={onClose} className="rounded border border-slate-600 px-3 py-2 text-sm">{t("Close")}</button>
    </div>
    {loading && <p role="status" className="mt-3 text-sm">{t("Loading report members...")}</p>}
    {error && <p role="alert" className="mt-3 text-sm text-rose-300">{t(error)}</p>}
    {!loading && rows.length === 0 && !error &&
      <p className="mt-3 text-sm text-slate-400">{t("No members in this reporting scope.")}</p>}
    {rows.length > 0 && <div className="mt-3 overflow-x-auto">
      <table className="w-full text-left text-sm">
        <thead><tr className="border-b border-slate-700 text-slate-400">
          <th className="py-2">{t("Shipment")}</th><th className="py-2">{t("Type")}</th>
          <th className="py-2">{t("Action")}</th>
        </tr></thead>
        <tbody>{rows.map(row => <tr className="border-b border-slate-800" key={row.memberKey}>
          <td className="py-2 font-mono text-cyan-300">{row.referenceCode}</td>
          <td className="py-2 text-slate-400">{t(row.exceptionId ? "Exception occurrence" : "Shipment")}</td>
          <td className="py-2"><button onClick={() => void open(row)} disabled={detailBusy}
            className="rounded border border-slate-600 px-2 py-1 text-xs disabled:opacity-50">{t("View current detail")}</button></td>
        </tr>)}</tbody>
      </table>
    </div>}
    {after && <button onClick={() => void next()} disabled={pageLoading}
      className="mt-3 rounded border border-cyan-600 px-3 py-2 text-sm disabled:opacity-50">
      {t(pageLoading ? "Loading..." : "Load more")}
    </button>}
    {detailBusy && <p className="mt-3 text-sm" role="status">{t("Checking live shipment access...")}</p>}
    {detailError && <p className="mt-3 text-sm text-rose-300" role="alert">{t(detailError)}</p>}
    {detail && <div className="mt-4 rounded border border-slate-700 p-3 text-sm">
      <p className="font-semibold">{detail.referenceCode} | {t(detail.workflowStatus)}</p>
      <p className="mt-1 text-xs text-slate-400">{t("Live detail may have changed since the report snapshot.")}</p>
      <p className="mt-2">{t("Record:")} {t(detail.recordStatus)} | {t("Source:")} {detail.displayName ?? t("Not provided")}</p>
      <p className="mt-1 text-xs text-slate-400">{t("Planned arrival:")} {detail.plannedArrivalAtUtc ? date(detail.plannedArrivalAtUtc) + " UTC" : t("Unknown")}</p>
      {exceptionState && <p className="mt-2 text-amber-200">{exceptionState.split(/(: |, )/).map(part => t(part)).join("")}</p>}
    </div>}
  </section>;
}