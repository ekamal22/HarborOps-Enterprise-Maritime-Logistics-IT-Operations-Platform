import { useI18n } from "../i18n/useI18n";
import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from "react";
import { getJson, organizationError, OrganizationRequestError } from "../auth/organizationClient";
import { archiveShipment, changeShipment, createShipment, shipmentHistory, shipmentDetail,
  type ShipmentChange, type ShipmentRow, type ShipmentWorkflow } from "./shipmentClient";
import type { ReferenceRow } from "../references/referenceClient";
import { isDirty, mergeLatest, scheduleEditable, serverChanged, workflowOptions } from "../lib/shipmentDraft.mjs";
import ShipmentAssignmentsPanel from "../assignments/ShipmentAssignmentsPanel";
import ShipmentTrackingTimeline from "./ShipmentTrackingTimeline";
import ShipmentExceptionPanel from "./ShipmentExceptionPanel";
import type { NotificationTarget } from "../notifications/NotificationCenter";
import { searchShipments, exportShipmentsCsv, emptyAdvancedFilters,
  type ShipmentAdvancedFilters, type ShipmentSearchFilters } from "./shipmentSearchClient";

type Form = {
  referenceCode: string; externalReference: string; displayName: string;
  originPortId: string; destinationPortId: string;
  plannedDepartureAtUtc: string; plannedArrivalAtUtc: string;
  workflowStatus: ShipmentWorkflow;
};
const blank: Form = {
  referenceCode: "", externalReference: "", displayName: "",
  originPortId: "", destinationPortId: "", plannedDepartureAtUtc: "",
  plannedArrivalAtUtc: "", workflowStatus: "Draft"
};
const fromRow = (r: ShipmentRow): Form => ({
  referenceCode: r.referenceCode, externalReference: r.externalReference ?? "",
  displayName: r.displayName ?? "", originPortId: r.originPortId,
  destinationPortId: r.destinationPortId,
  plannedDepartureAtUtc: r.plannedDepartureAtUtc ?? "",
  plannedArrivalAtUtc: r.plannedArrivalAtUtc ?? "",
  workflowStatus: r.workflowStatus
});
const fieldLabel: Record<string, string> = {
  displayName: "display name", plannedDepartureAtUtc: "planned departure",
  plannedArrivalAtUtc: "planned arrival", workflowStatus: "workflow status"
};
const labels = (keys: string[]) => keys.map(key => fieldLabel[key] ?? key).join(", ");
function denied(error: unknown): boolean {
  return error instanceof OrganizationRequestError &&
    (error.status === 401 || error.code === "access_denied" ||
      error.code === "organization_selection_required" ||
      error.code === "organization_access_revoked");
}
async function loadPorts(org: string): Promise<ReferenceRow[]> {
  let cursor: string | null = null;
  const all: ReferenceRow[] = [];
  const seen = new Set<string>();
  do {
    const qs = new URLSearchParams({ status: "Active", limit: "100" });
    if (cursor) qs.set("after", cursor);
    const page = await getJson<{ ports: ReferenceRow[]; nextAfter: string | null }>(
      "/api/v1/organizations/" + encodeURIComponent(org) + "/ports?" + qs);
    const rows = page.ports ?? [];
    for (const row of rows) if (!seen.has(row.id)) { seen.add(row.id); all.push(row); }
    if (page.nextAfter && (page.nextAfter === cursor || !rows.length || all.length > 10000))
      throw new Error("Port pagination did not finish safely");
    cursor = page.nextAfter;
  } while (cursor);
  return all;
}
export default function ShipmentWorkspace({ organizationId, canManage, canManageExceptions,
  notificationTarget, onAccessInvalidated }: {
  organizationId: string; canManage: boolean; canManageExceptions: boolean;
  notificationTarget?: NotificationTarget | null;
  onAccessInvalidated: (message: string) => void;
}) {
  const { t, date, number } = useI18n();
  const [q, setQ] = useState("");
  const [status, setStatus] = useState<"Active" | "Archived" | "All">("Active");
  const [advancedDraft, setAdvancedDraft] = useState<ShipmentAdvancedFilters>(emptyAdvancedFilters);
  const [advancedApplied, setAdvancedApplied] = useState<ShipmentAdvancedFilters>(emptyAdvancedFilters);
  const [exporting, setExporting] = useState(false);
  const [exportError, setExportError] = useState("");
  const exportAbortRef = useRef<AbortController | null>(null);
  const filters: ShipmentSearchFilters = useMemo(() =>
    ({ q, recordStatus: status, ...advancedApplied }), [q, status, advancedApplied]);
  const [items, setItems] = useState<ShipmentRow[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  // `selected` is the server snapshot the open form started from; `form` is the user's draft.
  const [selected, setSelected] = useState<ShipmentRow | null>(null);
  const [focusedTarget, setFocusedTarget] = useState<NotificationTarget | null>(null);
  const [remote, setRemote] = useState<ShipmentRow | null>(null);
  const [history, setHistory] = useState<ShipmentChange[]>([]);
  const [creating, setCreating] = useState(false);
  const [form, setFormState] = useState<Form>(blank);
  const [ports, setPorts] = useState<ReferenceRow[]>([]);
  const [portError, setPortError] = useState("");
  const [portRetry, setPortRetry] = useState(0);
  const [portBusy, setPortBusy] = useState(false);
  const [conflict, setConflict] = useState("");
  const [reviewable, setReviewable] = useState(false);
  const [stale, setStale] = useState("");
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [paging, setPaging] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  // Serials: a list reset (initial load, filter change), a background refresh, paging, the open
  // selection and mutations each have their own, so none can strand another's loading flag.
  const listSerial = useRef(0);
  const backgroundSerial = useRef(0);
  const pageSerial = useRef(0);
  const selectionSerial = useRef(0);
  const mutationSerial = useRef(0);
  const formRef = useRef<Form>(blank);
  const selectedRef = useRef<ShipmentRow | null>(null);
  const creatingRef = useRef(false);
  const loadingRef = useRef(true);
  const setForm = useCallback((next: Form | ((old: Form) => Form)) => {
    const value = typeof next === "function" ? next(formRef.current) : next;
    formRef.current = value;
    setFormState(value);
  }, []);
  selectedRef.current = selected;
  creatingRef.current = creating;
  loadingRef.current = loading;
  const invalidate = useCallback((cause: unknown) => {
    if (denied(cause)) onAccessInvalidated(organizationError(cause));
  }, [onAccessInvalidated]);

  const dirty = creating
    ? isDirty(blank, form)
    : Boolean(selected) && isDirty(fromRow(selected as ShipmentRow), form);
  const dirtyRef = useRef(false);
  dirtyRef.current = dirty;
  const confirmDiscard = () =>
    !dirtyRef.current || window.confirm(t("Discard unsaved shipment changes?"));

  const closePanel = useCallback(() => {
    selectionSerial.current++;
    setSelected(null); setFocusedTarget(null); setRemote(null); setHistory([]); setCreating(false);
    setConflict(""); setReviewable(false); setForm(blank);
  }, [setForm]);

  // Initial load and filter changes. Never touches the open shipment or its draft.
  const loadList = useCallback(async () => {
    const version = ++listSerial.current;
    ++backgroundSerial.current;
    ++pageSerial.current;
    setLoading(true); setRefreshing(false); setPaging(false); setError(""); setStale("");
    setItems([]); setCursor(null);
    try {
      const page = await searchShipments(organizationId, filters);
      if (version !== listSerial.current) return;
      setItems(page.shipments); setCursor(page.nextCursor);
    } catch (cause) {
      if (version !== listSerial.current) return;
      setError(organizationError(cause)); invalidate(cause);
    } finally {
      if (version === listSerial.current) setLoading(false);
    }
  }, [organizationId, filters, invalidate]);

  // Background list refresh: keeps the rows on screen, never clears them on failure, and is
  // discarded if a list reset started in the meantime.
  const refreshList = useCallback(async () => {
    if (loadingRef.current) return;
    const reset = listSerial.current;
    const version = ++backgroundSerial.current;
    setRefreshing(true);
    try {
      const page = await searchShipments(organizationId, filters);
      if (reset !== listSerial.current || version !== backgroundSerial.current) return;
      ++pageSerial.current;
      setPaging(false);
      setItems(page.shipments); setCursor(page.nextCursor); setStale("");
    } catch (cause) {
      if (reset !== listSerial.current || version !== backgroundSerial.current) return;
      if (denied(cause)) invalidate(cause);
      else setStale("Could not refresh shipments. The list shown may be out of date.");
    } finally {
      if (version === backgroundSerial.current) setRefreshing(false);
    }
  }, [organizationId, filters, invalidate]);

  // Background check of the open shipment. A newer server version is held aside as `remote`
  // and never applied to the form on its own.
  const refreshSelected = useCallback(async () => {
    const base = selectedRef.current;
    if (!base || creatingRef.current) return;
    const generation = selectionSerial.current;
    try {
      const detail = await shipmentDetail(organizationId, base.id);
      if (generation !== selectionSerial.current || selectedRef.current?.id !== base.id) return;
      if (serverChanged(base, detail)) { setRemote(detail); setReviewable(true); }
      else setSelected(current => current?.id === detail.id ? {
        ...current, originPortLabel: detail.originPortLabel, destinationPortLabel: detail.destinationPortLabel
      } : current);
    } catch (cause) {
      if (generation !== selectionSerial.current) return;
      if (denied(cause)) invalidate(cause);
      else setStale("Could not refresh the open shipment. The values shown may be out of date.");
    }
  }, [organizationId, invalidate]);

  const backgroundRefresh = useCallback(() => {
    void refreshList();
    void refreshSelected();
  }, [refreshList, refreshSelected]);
  const backgroundRef = useRef(backgroundRefresh);
  backgroundRef.current = backgroundRefresh;

  useEffect(() => { void loadList(); }, [loadList]);
  const loadListRef = useRef(loadList);
  loadListRef.current = loadList;

  useEffect(() => {
    const visible = () => { if (document.visibilityState === "visible") backgroundRef.current(); };
    document.addEventListener("visibilitychange", visible);
    return () => {
      exportAbortRef.current?.abort();
      document.removeEventListener("visibilitychange", visible);
      listSerial.current++; backgroundSerial.current++; pageSerial.current++;
      selectionSerial.current++; mutationSerial.current++;
    };
  }, []);

  useEffect(() => {
    let active = true;
    setPorts([]); setPortError(""); setPortBusy(canManage);
    if (canManage) {
      void loadPorts(organizationId).then(rows => {
        if (active) { setPorts(rows); setPortError(""); }
      }).catch(cause => {
        if (!active) return;
        setPortError(organizationError(cause));
        invalidate(cause);
      }).finally(() => { if (active) setPortBusy(false); });
    }
    return () => { active = false; };
  }, [organizationId, canManage, invalidate, portRetry]);

  async function select(row: ShipmentRow) {
    if (!creating && selected?.id === row.id) return;
    if (!confirmDiscard()) return;
    const generation = ++selectionSerial.current;
    setSelected(row); setFocusedTarget(null); setForm(fromRow(row)); setCreating(false); setHistory([]);
    setConflict(""); setReviewable(false); setRemote(null);
    try {
      const [detail, response] = await Promise.all([
        shipmentDetail(organizationId, row.id),
        shipmentHistory(organizationId, row.id)
      ]);
      if (generation !== selectionSerial.current) return;
      // The list row can be older than the detail. Merge anything typed meanwhile instead of
      // replacing it.
      const merged = mergeLatest(fromRow(row), formRef.current, fromRow(detail));
      setSelected(detail);
      setForm(merged.draft as Form);
      setHistory(response.changes);
    } catch (cause) {
      if (generation !== selectionSerial.current) return;
      setError(organizationError(cause)); invalidate(cause);
    }
  }


  useEffect(() => {
    if (!notificationTarget) return;
    if (!confirmDiscard()) return;
    let current = true;
    const run = ++selectionSerial.current;
    setFocusedTarget(null);
    setConflict(""); setReviewable(false); setRemote(null); setError("");
    void Promise.all([
      shipmentDetail(organizationId, notificationTarget.shipmentId),
      shipmentHistory(organizationId, notificationTarget.shipmentId)
    ]).then(([detail, response]) => {
      if (!current || run !== selectionSerial.current) return;
      setSelected(detail); setForm(fromRow(detail)); setHistory(response.changes);
      setCreating(false); setFocusedTarget(notificationTarget);
    }).catch(cause => {
      if (!current || run !== selectionSerial.current) return;
      setFocusedTarget(null);
      setError("The requested shipment or exception is no longer available.");
      invalidate(cause);
    });
    return () => { current = false; };
  }, [notificationTarget, organizationId]);

  async function more() {
    if (!cursor || paging || loading || busy) return;
    const revision = listSerial.current, page = ++pageSerial.current;
    setPaging(true);
    try {
      const next = await searchShipments(organizationId, filters, cursor);
      if (revision !== listSerial.current || page !== pageSerial.current) return;
      setItems(old => [...old, ...next.shipments.filter(r => !old.some(x => x.id === r.id))]);
      setCursor(next.nextCursor);
    } catch (cause) {
      if (revision !== listSerial.current || page !== pageSerial.current) return;
      setError(organizationError(cause)); setItems([]); setCursor(null);
      invalidate(cause);
    } finally {
      if (page === pageSerial.current) setPaging(false);
    }
  }

  async function save(event: FormEvent) {
    event.preventDefault();
    if (!canManage || busy) return;
    const mutation = mutationSerial.current;
    const draft = formRef.current;
    setBusy(true); setError(""); setConflict(""); setReviewable(false);
    try {
      if (creating) {
        await createShipment(organizationId, {
          referenceCode: draft.referenceCode.trim(),
          externalReference: draft.externalReference.trim() || null,
          displayName: draft.displayName.trim() || null,
          originPortId: draft.originPortId,
          destinationPortId: draft.destinationPortId,
          plannedDepartureAtUtc: draft.plannedDepartureAtUtc.trim() || null,
          plannedArrivalAtUtc: draft.plannedArrivalAtUtc.trim() || null
        });
      } else if (selected) {
        const patch: Record<string, string | number | null> = {
          expectedVersion: selected.version
        };
        if ((draft.displayName.trim() || null) !== selected.displayName)
          patch.displayName = draft.displayName.trim() || null;
        for (const key of ["plannedDepartureAtUtc", "plannedArrivalAtUtc"] as const) {
          if ((draft[key].trim() || null) !== selected[key])
            patch[key] = draft[key].trim() || null;
        }
        if (draft.workflowStatus !== selected.workflowStatus)
          patch.workflowStatus = draft.workflowStatus;
        if (Object.keys(patch).length === 1) return;
        await changeShipment(organizationId, selected.id, patch);
      }
      if (mutation === mutationSerial.current) { closePanel(); await loadListRef.current(); }
    } catch (cause) {
      if (mutation !== mutationSerial.current) return;
      if (cause instanceof OrganizationRequestError && cause.status === 409) {
        setConflict("This shipment has changed since you opened it. Your edits are preserved. Review the current version before retrying.");
        setReviewable(true);
      } else {
        setError(organizationError(cause)); invalidate(cause);
      }
    } finally {
      if (mutation === mutationSerial.current) setBusy(false);
    }
  }

  // Load the newest server version and merge the draft into it: untouched fields follow the
  // server, edited fields keep the user's value, and collisions are reported, not resolved.
  async function reviewCurrent() {
    const base = selectedRef.current;
    if (!base || busy) return;
    const generation = ++selectionSerial.current;
    try {
      const [latest, changes] = await Promise.all([
        shipmentDetail(organizationId, base.id), shipmentHistory(organizationId, base.id)]);
      if (generation !== selectionSerial.current) return;
      const merged = mergeLatest(fromRow(base), formRef.current, fromRow(latest));
      setSelected(latest); setForm(merged.draft as Form); setHistory(changes.changes);
      setRemote(null); setReviewable(false);
      const notes = ["Current version " + latest.version + " loaded."];
      if (merged.kept.length) notes.push("Your unsaved changes to " + labels(merged.kept) + " were kept.");
      if (merged.collisions.length)
        notes.push("The server also changed " + labels(merged.collisions) + ". Your value is shown and saving will replace the server's.");
      if (merged.discarded.length)
        notes.push("Your changes to " + labels(merged.discarded) + " can no longer be applied and were not kept.");
      setConflict(notes.join(" "));
    } catch (cause) {
      if (generation !== selectionSerial.current) return;
      setError(organizationError(cause));
      invalidate(cause);
    }
  }

  async function archiveOrRestore(row: ShipmentRow) {
    if (!canManage || busy) return;
    if (row.recordStatus === "Active" && !window.confirm(
      selected?.id === row.id && dirtyRef.current
        ? t("Archive this shipment? Your unsaved changes to it will be lost.") : t("Archive this shipment?"))) return;
    const mutation = mutationSerial.current;
    setBusy(true); setError("");
    try {
      if (row.recordStatus === "Active") await archiveShipment(organizationId, row.id, row.version);
      else await changeShipment(organizationId, row.id,
        { recordStatus: "Active", expectedVersion: row.version });
      if (mutation === mutationSerial.current) {
        // Only the shipment that changed loses its panel; a draft on another row is untouched.
        if (selectedRef.current?.id === row.id) closePanel();
        await loadListRef.current();
      }
    } catch (cause) {
      if (mutation !== mutationSerial.current) return;
      setError(organizationError(cause)); invalidate(cause);
    } finally { if (mutation === mutationSerial.current) setBusy(false); }
  }


  async function downloadExport() {
    if (!canManage || exporting) return;
    const controller = new AbortController();
    exportAbortRef.current?.abort();
    exportAbortRef.current = controller;
    setExporting(true);
    setExportError("");
    try {
      await exportShipmentsCsv(organizationId, filters, controller.signal);
    } catch (cause) {
      if (controller.signal.aborted) return;
      setExportError(organizationError(cause));
      invalidate(cause);
    } finally {
      if (exportAbortRef.current === controller) {
        exportAbortRef.current = null;
        setExporting(false);
      }
    }
  }

  const advancedFields: { key: Exclude<keyof ShipmentAdvancedFilters, "workflowStatus">; label: string }[] = [
    { key: "voyageId", label: "Current voyage UUID" },
    { key: "originPortId", label: "Origin port UUID" },
    { key: "destinationPortId", label: "Destination port UUID" },
    { key: "plannedDepartureFromUtc", label: "Departure from UTC (inclusive)" },
    { key: "plannedDepartureBeforeUtc", label: "Departure before UTC (exclusive)" },
    { key: "plannedArrivalFromUtc", label: "Arrival from UTC (inclusive)" },
    { key: "plannedArrivalBeforeUtc", label: "Arrival before UTC (exclusive)" }
  ];

  const field = (key: keyof Form, label: string, locked = false) => (
    <label key={key} className="block text-sm">{t(label)}
      <input aria-label={t(label)} value={form[key]} disabled={locked || !canManage}
        onChange={e => setForm(old => ({ ...old, [key]: e.target.value }))}
        className="mt-1 block w-full rounded bg-slate-950 p-2 disabled:opacity-60" />
    </label>
  );
  const readonly = !canManage || (!creating && selected?.recordStatus === "Archived");
  const scheduleLocked = readonly || (!creating && selected !== null && !scheduleEditable(selected.workflowStatus));
  const remoteNote = remote && selected
    ? t("This shipment changed on the server (now version {remote}; you opened version {opened}). Your unsaved edits are preserved.", {remote: remote.version, opened: selected.version}) : "";
  const fieldNames = (value: string) => value.split(", ").map(part => t(part)).join(", ");
  const localizedConflict = (value: string) => t(value)
    .replace(/Current version (\\d+) loaded\\./g, (_match, version: string) =>
      t("Current version {version} loaded.", { version }))
    .replace(/Your unsaved changes to (.+?) were kept\\./g, (_match, fields: string) =>
      t("Your unsaved changes to {fields} were kept.", { fields: fieldNames(fields) }))
    .replace(/The server also changed (.+?)\\. Your value is shown and saving will replace the server's\\./g,
      (_match, fields: string) => t("The server also changed {fields}. Your value is shown and saving will replace the server's.",
        { fields: fieldNames(fields) }))
    .replace(/Your changes to (.+?) can no longer be applied and were not kept\\./g,
      (_match, fields: string) => t("Your changes to {fields} can no longer be applied and were not kept.",
        { fields: fieldNames(fields) }));
  const banner = conflict ? localizedConflict(conflict) : remoteNote;
  return <section className="mt-6 border-t border-slate-700 pt-5">
    <div className="flex items-center justify-between gap-3">
      <h4 className="font-semibold">{t("Shipments")}</h4>
      <button type="button" disabled={busy || loading || paging}
        onClick={() => { backgroundRefresh(); if (canManage) setPortRetry(x => x + 1); }}
        className="rounded border border-slate-600 px-3 py-2 text-sm">{t("Refresh shipments")}</button>
    </div>
    <div className="mt-4 flex flex-wrap items-end gap-3">
      <label className="text-sm">{t("Search shipments")}<input value={q} maxLength={80} onChange={e => setQ(e.target.value)}
          className="mt-1 block rounded bg-slate-950 p-2" />
      </label>
      <label className="text-sm">{t("Shipment record status")}<select value={status} onChange={e => setStatus(e.target.value as typeof status)}
          className="mt-1 block rounded bg-slate-950 p-2">
          {["Active", "Archived", "All"].map(x => <option key={x} value={x}>{t(x)}</option>)}
        </select>
      </label>
      {canManage && <button type="button" disabled={busy || loading || portBusy || Boolean(portError) || ports.length < 2}
        onClick={() => {
          if (creating || !confirmDiscard()) return;
          selectionSerial.current++; setCreating(true); setSelected(null); setRemote(null);
          setHistory([]); setForm(blank); setConflict(""); setReviewable(false);
        }}
        className="rounded border border-cyan-500 px-3 py-2 text-sm disabled:opacity-50">{t("Create shipment")}</button>}
    </div>

    <details className="mt-3 rounded border border-slate-700 p-3">
      <summary className="cursor-pointer text-sm">{t("Advanced shipment search")}</summary>
      <div className="mt-3 grid gap-3 md:grid-cols-2">
        <label className="text-sm">{t("Filter by workflow status")}<select value={advancedDraft.workflowStatus}
            onChange={e => setAdvancedDraft(old => ({ ...old,
              workflowStatus: e.target.value as ShipmentAdvancedFilters["workflowStatus"] }))}
            className="mt-1 block w-full rounded bg-slate-950 p-2">
            {["", "Draft", "Booked", "InTransit", "Arrived", "Completed", "Cancelled"].map(x =>
              <option key={x} value={x}>{t(x || "Any workflow")}</option>)}
          </select>
        </label>
        {advancedFields.map(item => <label key={item.key} className="text-sm">{t(item.label)}
          <input value={advancedDraft[item.key]} placeholder={item.key.endsWith("Utc") ?
            "2026-11-01T00:00:00Z" : "UUID"}
            onChange={e => setAdvancedDraft(old => ({ ...old, [item.key]: e.target.value }))}
            className="mt-1 block w-full rounded bg-slate-950 p-2" />
        </label>)}
      </div>
      <div className="mt-3 flex flex-wrap gap-2">
        <button type="button" onClick={() => setAdvancedApplied({ ...advancedDraft })}
          className="rounded border border-cyan-500 px-3 py-2 text-sm">{t("Apply advanced filters")}</button>
        <button type="button" onClick={() => {
          setAdvancedDraft({ ...emptyAdvancedFilters });
          setAdvancedApplied({ ...emptyAdvancedFilters });
        }} className="rounded border border-slate-600 px-3 py-2 text-sm">{t("Clear advanced filters")}</button>
      </div>
    </details>
    {canManage && <div className="mt-3">
      <button type="button" disabled={exporting || busy || loading}
        onClick={() => void downloadExport()}
        className="rounded border border-cyan-500 px-3 py-2 text-sm disabled:opacity-50">
        {t(exporting ? "Preparing CSV..." : "Export matching shipments as CSV")}
      </button>
      <p className="mt-1 text-xs text-slate-400">{t("Export is restricted to this organization and matching filters. Maximum 5,000 rows.")}</p>
      {exportError && <p role="alert" className="mt-2 text-sm text-rose-300">{t(exportError)}</p>}
    </div>}
    {loading && <p className="mt-3 text-sm text-slate-400">{t("Loading shipments...")}</p>}
    {refreshing && !loading && <p role="status" className="mt-3 text-xs text-slate-400">{t("Updating list...")}</p>}
    {stale && <p role="status" className="mt-3 text-xs text-amber-300">{t(stale)}</p>}
    {error && <p role="alert" className="mt-3 text-sm text-rose-300">{t(error)}</p>}
    {banner && <div role="alert" className="mt-3 text-sm text-amber-300">{banner}
      {selected && reviewable && <button type="button" onClick={() => void reviewCurrent()}
        className="ml-3 rounded border border-amber-500 px-2 py-1">{t("Load current version")}</button>}
    </div>}
    {portError && canManage && <div role="alert" className="mt-2 text-xs text-amber-300">{t("Port selector unavailable:")} {t(portError)}
      <button type="button" disabled={portBusy}
        onClick={() => setPortRetry(x => x + 1)}
        className="ml-2 rounded border border-amber-500 px-2 py-1">{t("Retry port catalog")}</button>
    </div>}
    {!loading && items.length === 0 && <p className="mt-3 text-sm text-slate-400">{t("No shipments match this filter.")}</p>}
    <ul className="mt-3 space-y-2">
      {items.map(row => <li key={row.id} className="flex flex-wrap items-center justify-between gap-2 rounded border border-slate-700 p-3 text-sm">
        <div><strong>{row.referenceCode}</strong> <span className="text-slate-400">{row.displayName || ""}</span>
          <div className="text-xs text-slate-400">{t(row.workflowStatus)} | {t(row.recordStatus)} | v{row.version}</div>
        </div>
        <div className="flex gap-2">
          <button type="button" onClick={() => void select(row)}
            className="rounded border border-slate-500 px-3 py-2">{t("View shipment")}</button>
          {canManage && <button type="button" disabled={busy}
            onClick={() => void archiveOrRestore(row)}
            className="rounded border border-slate-600 px-3 py-2">
            {t(row.recordStatus === "Active" ? "Archive shipment" : "Restore shipment")}
          </button>}
        </div>
      </li>)}
    </ul>
    {cursor && <button type="button" disabled={paging || loading || busy} onClick={() => void more()}
      className="mt-3 rounded border border-slate-600 px-3 py-2 text-sm">
      {t(paging ? "Loading next page..." : "Load more shipments")}
    </button>}
    {(creating || selected) && <div className="mt-5 rounded-lg border border-slate-600 p-4">
      <div className="flex items-center justify-between gap-2">
        <h5 className="font-medium">{creating ? t("Create shipment") : selected?.referenceCode}</h5>
        {dirty && <span role="status" className="text-xs text-amber-300">{t("Unsaved changes")}</span>}
        <button type="button" onClick={() => { if (confirmDiscard()) closePanel(); }}
          className="rounded border border-slate-600 px-2 py-1">{t("Close detail")}</button>
      </div>
      <form onSubmit={e => void save(e)} className="mt-3 grid gap-3">
        {field("referenceCode", "Shipment reference code", !creating || readonly)}
        {field("externalReference", "External reference", !creating || readonly)}
        {field("displayName", "Shipment display name", readonly)}
        {creating ? <>
          {(["originPortId", "destinationPortId"] as const).map(key =>
            <label key={key} className="text-sm">{t(key === "originPortId" ? "Origin port" : "Destination port")}
              <select aria-label={t(key === "originPortId" ? "Origin port" : "Destination port")}
                value={form[key]} required
                onChange={e => setForm(old => ({ ...old, [key]: e.target.value }))}
                className="mt-1 block w-full rounded bg-slate-950 p-2">
                <option value="">{t("Select port")}</option>
                {ports.map(port => <option key={port.id} value={port.id}>{port.referenceCode} {port.name}</option>)}
              </select>
            </label>)}
        </> : <p className="text-sm text-slate-400">{t("Route:")} {selected?.originPortLabel || selected?.originPortId} {t("to")} {selected?.destinationPortLabel || selected?.destinationPortId}</p>}
        {field("plannedDepartureAtUtc", "Planned departure UTC (ISO 8601)", scheduleLocked)}
        {field("plannedArrivalAtUtc", "Planned arrival UTC (ISO 8601)", scheduleLocked)}
        {!creating && selected && <label className="text-sm">{t("Shipment workflow")}<select aria-label={t("Shipment workflow")} value={form.workflowStatus} disabled={readonly}
            onChange={e => setForm(old => ({ ...old, workflowStatus: e.target.value as ShipmentWorkflow }))}
            className="mt-1 block w-full rounded bg-slate-950 p-2">
            {workflowOptions(selected.workflowStatus).map((x: string) => <option key={x} value={x}>{t(x)}</option>)}
          </select>
        </label>}
        {!readonly && <button type="submit" disabled={busy}
          className="rounded bg-cyan-300 px-3 py-2 text-sm font-medium text-slate-950 disabled:opacity-50">
          {t(creating ? "Save new shipment" : "Save shipment changes")}
        </button>}
      </form>
      {!creating && selected && <ShipmentAssignmentsPanel
        key={`${organizationId}:${selected.id}`}
        organizationId={organizationId} shipment={selected} canManage={canManage}
        onAccessInvalidated={onAccessInvalidated} />}
      {!creating && selected?.recordStatus === "Active" && <ShipmentTrackingTimeline
        key={`tracking:${organizationId}:${selected.id}`}
        organizationId={organizationId} shipmentId={selected.id}
        onAccessInvalidated={onAccessInvalidated} />}
      {!creating && selected && <ShipmentExceptionPanel
        key={`exceptions:${organizationId}:${selected.id}`}
        organizationId={organizationId} shipmentId={selected.id}
        isManager={canManageExceptions} onAccessInvalidated={onAccessInvalidated}
         focusExceptionId={focusedTarget?.exceptionId}
         focusRequest={focusedTarget?.request} />}
      {!creating && selected && <div className="mt-4">
        <h6 className="font-medium">{t("Manual status history")}</h6>
        {history.length === 0 ? <p className="text-xs text-slate-400">{t("No status changes loaded.")}</p> :
          <ol className="mt-2 space-y-1 text-xs text-slate-300">{history.map(x =>
            <li key={x.id}>v{number(x.version)}: {t(x.fromStatus || "Created")} {t("to")} {t(x.toStatus)} {t("at")} {date(x.changedAtUtc)} UTC</li>)}</ol>}
      </div>}
    </div>}
  </section>;
}
