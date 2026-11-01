import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { getJson, organizationError, OrganizationRequestError } from "../auth/organizationClient";
import { archiveShipment, changeShipment, createShipment, listShipments, shipmentHistory, shipmentDetail,
  type ShipmentChange, type ShipmentRow, type ShipmentWorkflow } from "./shipmentClient";
import type { ReferenceRow } from "../references/referenceClient";

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
const allowed: Record<ShipmentWorkflow, ShipmentWorkflow[]> = {
  Draft: ["Booked", "Cancelled"],
  Booked: ["InTransit", "Cancelled"],
  InTransit: ["Arrived", "Cancelled"],
  Arrived: ["Completed"],
  Completed: [], Cancelled: []
};
const fromRow = (r: ShipmentRow): Form => ({
  referenceCode: r.referenceCode, externalReference: r.externalReference ?? "",
  displayName: r.displayName ?? "", originPortId: r.originPortId,
  destinationPortId: r.destinationPortId,
  plannedDepartureAtUtc: r.plannedDepartureAtUtc ?? "",
  plannedArrivalAtUtc: r.plannedArrivalAtUtc ?? "",
  workflowStatus: r.workflowStatus
});
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
export default function ShipmentWorkspace({ organizationId, canManage, onAccessInvalidated }: {
  organizationId: string; canManage: boolean; onAccessInvalidated: (message: string) => void;
}) {
  const [q, setQ] = useState("");
  const [status, setStatus] = useState<"Active" | "Archived" | "All">("Active");
  const [items, setItems] = useState<ShipmentRow[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [selected, setSelected] = useState<ShipmentRow | null>(null);
  const [history, setHistory] = useState<ShipmentChange[]>([]);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<Form>(blank);
  const [ports, setPorts] = useState<ReferenceRow[]>([]);
  const [portError, setPortError] = useState("");
  const [portRetry, setPortRetry] = useState(0);
  const [portBusy, setPortBusy] = useState(false);
  const [conflict, setConflict] = useState("");
  const [loading, setLoading] = useState(true);
  const [paging, setPaging] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const requestSerial = useRef(0);
  const pageSerial = useRef(0);
  const selectionSerial = useRef(0);
  const mutationSerial = useRef(0);
  const invalidate = useCallback((cause: unknown) => {
    if (denied(cause)) onAccessInvalidated(organizationError(cause));
  }, [onAccessInvalidated]);

  const reload = useCallback(async () => {
    const version = ++requestSerial.current;
    ++pageSerial.current;
    ++selectionSerial.current;
    setLoading(true); setPaging(false); setError("");
    setItems([]); setCursor(null); setSelected(null); setHistory([]); setCreating(false);
    try {
      const page = await listShipments(organizationId, q, status);
      if (version !== requestSerial.current) return;
      setItems(page.shipments); setCursor(page.nextAfter);
    } catch (cause) {
      if (version !== requestSerial.current) return;
      setError(organizationError(cause)); invalidate(cause);
    } finally {
      if (version === requestSerial.current) setLoading(false);
    }
  }, [organizationId, q, status, invalidate]);

  useEffect(() => {
    void reload();
    const visible = () => { if (document.visibilityState === "visible") void reload(); };
    document.addEventListener("visibilitychange", visible);
    return () => {
      requestSerial.current++; pageSerial.current++; selectionSerial.current++;
      mutationSerial.current++; document.removeEventListener("visibilitychange", visible);
    };
  }, [reload]);

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
    const generation = ++selectionSerial.current;
    setSelected(row); setForm(fromRow(row)); setCreating(false); setHistory([]); setConflict("");
    try {
      const response = await shipmentHistory(organizationId, row.id);
      if (generation === selectionSerial.current) setHistory(response.changes);
    } catch (cause) {
      if (generation !== selectionSerial.current) return;
      setError(organizationError(cause)); invalidate(cause);
    }
  }

  async function more() {
    if (!cursor || paging || loading || busy) return;
    const revision = requestSerial.current, page = ++pageSerial.current;
    setPaging(true);
    try {
      const next = await listShipments(organizationId, q, status, cursor);
      if (revision !== requestSerial.current || page !== pageSerial.current) return;
      setItems(old => [...old, ...next.shipments.filter(r => !old.some(x => x.id === r.id))]);
      setCursor(next.nextAfter);
    } catch (cause) {
      if (revision !== requestSerial.current || page !== pageSerial.current) return;
      setError(organizationError(cause)); setItems([]); setCursor(null);
      invalidate(cause);
    } finally {
      if (page === pageSerial.current) setPaging(false);
    }
  }

  async function save(event: FormEvent) {
    event.preventDefault();
    if (!canManage || busy) return;
    const revision = requestSerial.current, mutation = mutationSerial.current;
    setBusy(true); setError(""); setConflict("");
    try {
      if (creating) {
        await createShipment(organizationId, {
          referenceCode: form.referenceCode.trim(),
          externalReference: form.externalReference.trim() || null,
          displayName: form.displayName.trim() || null,
          originPortId: form.originPortId,
          destinationPortId: form.destinationPortId,
          plannedDepartureAtUtc: form.plannedDepartureAtUtc.trim() || null,
          plannedArrivalAtUtc: form.plannedArrivalAtUtc.trim() || null
        });
      } else if (selected) {
        const patch: Record<string, string | number | null> = {
          expectedVersion: selected.version
        };
        if ((form.displayName.trim() || null) !== selected.displayName)
          patch.displayName = form.displayName.trim() || null;
        for (const key of ["plannedDepartureAtUtc", "plannedArrivalAtUtc"] as const) {
          if ((form[key].trim() || null) !== selected[key])
            patch[key] = form[key].trim() || null;
        }
        if (form.workflowStatus !== selected.workflowStatus)
          patch.workflowStatus = form.workflowStatus;
        if (Object.keys(patch).length === 1) return;
        await changeShipment(organizationId, selected.id, patch);
      }
      if (revision === requestSerial.current) await reload();
    } catch (cause) {
      if (revision !== requestSerial.current) return;
      if (cause instanceof OrganizationRequestError && cause.status === 409) {
        setConflict("This shipment has changed since you opened it. Your edits are preserved. Review the current version before retrying.");
      } else {
        setError(organizationError(cause)); invalidate(cause);
      }
    } finally {
      if (mutation === mutationSerial.current) setBusy(false);
    }
  }
  async function reviewCurrent() {
    if (!selected || busy) return;
    const generation = ++selectionSerial.current;
    try {
      const current = await shipmentDetail(organizationId, selected.id);
      if (generation !== selectionSerial.current) return;
      setSelected(current);
      setConflict("Current shipment version loaded. Review your preserved edits before saving.");
      const changes = await shipmentHistory(organizationId, current.id);
      if (generation === selectionSerial.current) setHistory(changes.changes);
    } catch (cause) {
      if (generation !== selectionSerial.current) return;
      setError(organizationError(cause));
      invalidate(cause);
    }
  }
  async function archiveOrRestore(row: ShipmentRow) {
    if (!canManage || busy) return;
    if (row.recordStatus === "Active" && !window.confirm("Archive this shipment?")) return;
    const revision = requestSerial.current, mutation = mutationSerial.current;
    setBusy(true); setError("");
    try {
      if (row.recordStatus === "Active") await archiveShipment(organizationId, row.id, row.version);
      else await changeShipment(organizationId, row.id,
        { recordStatus: "Active", expectedVersion: row.version });
      if (revision === requestSerial.current) await reload();
    } catch (cause) {
      if (revision !== requestSerial.current) return;
      setError(organizationError(cause)); invalidate(cause);
    } finally { if (mutation === mutationSerial.current) setBusy(false); }
  }
  const field = (key: keyof Form, label: string, locked = false) => (
    <label key={key} className="block text-sm">{label}
      <input aria-label={label} value={form[key]} disabled={locked || !canManage}
        onChange={e => setForm(old => ({ ...old, [key]: e.target.value }))}
        className="mt-1 block w-full rounded bg-slate-950 p-2 disabled:opacity-60" />
    </label>
  );
  const readonly = !canManage || (!creating && selected?.recordStatus === "Archived");
  return <section className="mt-6 border-t border-slate-700 pt-5">
    <div className="flex items-center justify-between gap-3">
      <h4 className="font-semibold">Shipments</h4>
      <button type="button" disabled={busy || loading || paging}
        onClick={() => { void reload(); if (canManage) setPortRetry(x => x + 1); }}
        className="rounded border border-slate-600 px-3 py-2 text-sm">
        Refresh shipments
      </button>
    </div>
    <div className="mt-4 flex flex-wrap items-end gap-3">
      <label className="text-sm">Search shipments
        <input value={q} maxLength={80} onChange={e => setQ(e.target.value)}
          className="mt-1 block rounded bg-slate-950 p-2" />
      </label>
      <label className="text-sm">Shipment record status
        <select value={status} onChange={e => setStatus(e.target.value as typeof status)}
          className="mt-1 block rounded bg-slate-950 p-2">
          {["Active", "Archived", "All"].map(x => <option key={x}>{x}</option>)}
        </select>
      </label>
      {canManage && <button type="button" disabled={busy || loading || portBusy || Boolean(portError) || ports.length < 2}
        onClick={() => { selectionSerial.current++; setCreating(true); setSelected(null); setHistory([]); setForm(blank); setConflict(""); }}
        className="rounded border border-cyan-500 px-3 py-2 text-sm disabled:opacity-50">Create shipment</button>}
    </div>
    {loading && <p className="mt-3 text-sm text-slate-400">Loading shipments...</p>}
    {error && <p role="alert" className="mt-3 text-sm text-rose-300">{error}</p>}
    {conflict && <div role="alert" className="mt-3 text-sm text-amber-300">{conflict}
      {selected && <button type="button" onClick={() => void reviewCurrent()}
        className="ml-3 rounded border border-amber-500 px-2 py-1">Load current version</button>}
    </div>}
    {portError && canManage && <div role="alert" className="mt-2 text-xs text-amber-300">
      Port selector unavailable: {portError}
      <button type="button" disabled={portBusy}
        onClick={() => setPortRetry(x => x + 1)}
        className="ml-2 rounded border border-amber-500 px-2 py-1">Retry port catalog</button>
    </div>}
    {!loading && items.length === 0 && <p className="mt-3 text-sm text-slate-400">No shipments match this filter.</p>}
    <ul className="mt-3 space-y-2">
      {items.map(row => <li key={row.id} className="flex flex-wrap items-center justify-between gap-2 rounded border border-slate-700 p-3 text-sm">
        <div><strong>{row.referenceCode}</strong> <span className="text-slate-400">{row.displayName || ""}</span>
          <div className="text-xs text-slate-400">{row.workflowStatus} | {row.recordStatus} | v{row.version}</div>
        </div>
        <div className="flex gap-2">
          <button type="button" onClick={() => void select(row)}
            className="rounded border border-slate-500 px-3 py-2">View shipment</button>
          {canManage && <button type="button" disabled={busy}
            onClick={() => void archiveOrRestore(row)}
            className="rounded border border-slate-600 px-3 py-2">
            {row.recordStatus === "Active" ? "Archive shipment" : "Restore shipment"}
          </button>}
        </div>
      </li>)}
    </ul>
    {cursor && <button type="button" disabled={paging || loading || busy} onClick={() => void more()}
      className="mt-3 rounded border border-slate-600 px-3 py-2 text-sm">
      {paging ? "Loading next page..." : "Load more shipments"}
    </button>}
    {(creating || selected) && <div className="mt-5 rounded-lg border border-slate-600 p-4">
      <div className="flex items-center justify-between gap-2">
        <h5 className="font-medium">{creating ? "Create shipment" : selected?.referenceCode}</h5>
        <button type="button" onClick={() => { selectionSerial.current++; setCreating(false); setSelected(null); setHistory([]); setConflict(""); }}
          className="rounded border border-slate-600 px-2 py-1">Close detail</button>
      </div>
      <form onSubmit={e => void save(e)} className="mt-3 grid gap-3">
        {field("referenceCode", "Shipment reference code", !creating || readonly)}
        {field("externalReference", "External reference", !creating || readonly)}
        {field("displayName", "Shipment display name", readonly)}
        {creating ? <>
          {(["originPortId", "destinationPortId"] as const).map(key =>
            <label key={key} className="text-sm">{key === "originPortId" ? "Origin port" : "Destination port"}
              <select aria-label={key === "originPortId" ? "Origin port" : "Destination port"}
                value={form[key]} required
                onChange={e => setForm(old => ({ ...old, [key]: e.target.value }))}
                className="mt-1 block w-full rounded bg-slate-950 p-2">
                <option value="">Select port</option>
                {ports.map(port => <option key={port.id} value={port.id}>{port.referenceCode} {port.name}</option>)}
              </select>
            </label>)}
        </> : <p className="text-sm text-slate-400">Route: {selected?.originPortLabel || selected?.originPortId} to {selected?.destinationPortLabel || selected?.destinationPortId}</p>}
        {field("plannedDepartureAtUtc", "Planned departure UTC (ISO 8601)",
          readonly || (!creating && selected?.workflowStatus !== "Draft" && selected?.workflowStatus !== "Booked"))}
        {field("plannedArrivalAtUtc", "Planned arrival UTC (ISO 8601)",
          readonly || (!creating && selected?.workflowStatus !== "Draft" && selected?.workflowStatus !== "Booked"))}
        {!creating && selected && <label className="text-sm">Shipment workflow
          <select aria-label="Shipment workflow" value={form.workflowStatus} disabled={readonly}
            onChange={e => setForm(old => ({ ...old, workflowStatus: e.target.value as ShipmentWorkflow }))}
            className="mt-1 block w-full rounded bg-slate-950 p-2">
            {[selected.workflowStatus, ...allowed[selected.workflowStatus]].map(x => <option key={x}>{x}</option>)}
          </select>
        </label>}
        {!readonly && <button type="submit" disabled={busy}
          className="rounded bg-cyan-300 px-3 py-2 text-sm font-medium text-slate-950 disabled:opacity-50">
          {creating ? "Save new shipment" : "Save shipment changes"}
        </button>}
      </form>
      {!creating && selected && <div className="mt-4">
        <h6 className="font-medium">Manual status history</h6>
        {history.length === 0 ? <p className="text-xs text-slate-400">No status changes loaded.</p> :
          <ol className="mt-2 space-y-1 text-xs text-slate-300">{history.map(x =>
            <li key={x.id}>v{x.version}: {x.fromStatus || "Created"} to {x.toStatus} at {x.changedAtUtc}</li>)}</ol>}
      </div>}
    </div>}
  </section>;
}
