import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { getJson, organizationError, OrganizationRequestError } from "../auth/organizationClient";
import { archiveMaritime, createMaritime, listMaritime, updateMaritime,
  type MaritimeKind, type MaritimeRow } from "./maritimeClient";
import type { ReferenceRow } from "../references/referenceClient";

type Form = {
  referenceCode: string; vesselId: string; originPortId: string; destinationPortId: string;
  plannedDepartureAtUtc: string; plannedArrivalAtUtc: string;
  actualDepartureAtUtc: string; actualArrivalAtUtc: string; voyageStatus: string;
  identifierKind: "ISO6346" | "Synthetic"; containerIdentifier: string;
  equipmentType: string; displayName: string;
};
const empty: Form = {
  referenceCode: "", vesselId: "", originPortId: "", destinationPortId: "",
  plannedDepartureAtUtc: "", plannedArrivalAtUtc: "", actualDepartureAtUtc: "",
  actualArrivalAtUtc: "", voyageStatus: "Planned", identifierKind: "Synthetic",
  containerIdentifier: "", equipmentType: "Dry20", displayName: ""
};
const formats = ["Dry20", "Dry40", "HighCube40", "Reefer20", "Reefer40", "Other"] as const;
const progress = ["Planned", "Underway", "Arrived", "Cancelled"] as const;
const valueOf = (row: MaritimeRow): Form => ({
  ...empty, referenceCode: row.referenceCode, vesselId: row.vesselId ?? "",
  originPortId: row.originPortId ?? "", destinationPortId: row.destinationPortId ?? "",
  plannedDepartureAtUtc: row.plannedDepartureAtUtc ?? "",
  plannedArrivalAtUtc: row.plannedArrivalAtUtc ?? "",
  actualDepartureAtUtc: row.actualDepartureAtUtc ?? "",
  actualArrivalAtUtc: row.actualArrivalAtUtc ?? "",
  voyageStatus: row.voyageStatus ?? "Planned",
  identifierKind: row.identifierKind ?? "Synthetic",
  containerIdentifier: row.containerIdentifier ?? "",
  equipmentType: row.equipmentType ?? "Dry20",
  displayName: row.displayName ?? ""
});
function payload(kind: MaritimeKind, form: Form, isNew: boolean, prior?: MaritimeRow): Record<string, unknown> {
  if (kind === "containers")
    return isNew ? {
      referenceCode: form.referenceCode.trim(), identifierKind: form.identifierKind,
      containerIdentifier: form.containerIdentifier.trim(),
      equipmentType: form.equipmentType, displayName: form.displayName.trim() || null
    } : { displayName: form.displayName.trim() || null };
  if (isNew) return {
    referenceCode: form.referenceCode.trim(), vesselId: form.vesselId,
    originPortId: form.originPortId, destinationPortId: form.destinationPortId,
    plannedDepartureAtUtc: form.plannedDepartureAtUtc.trim() || null,
    plannedArrivalAtUtc: form.plannedArrivalAtUtc.trim() || null
  };
  const changes: Record<string, unknown> = {};
  for (const k of ["plannedDepartureAtUtc", "plannedArrivalAtUtc",
    "actualDepartureAtUtc", "actualArrivalAtUtc", "voyageStatus"] as const) {
    const before = prior?.[k] ?? (k === "voyageStatus" ? "Planned" : "");
    if (form[k] !== before) changes[k] = k === "voyageStatus" ? form[k] : form[k].trim() || null;
  }
  return changes;
}
function classify(error: unknown): boolean {
  return error instanceof OrganizationRequestError &&
    (error.status === 401 || error.code === "organization_access_revoked" ||
     error.code === "organization_selection_required" || error.code === "access_denied");
}
export default function MaritimeWorkspace({ organizationId, canManage, onAccessInvalidated }: {
  organizationId: string; canManage: boolean; onAccessInvalidated: (message: string) => void;
}) {
  const [kind, setKind] = useState<MaritimeKind>("voyages");
  const [status, setStatus] = useState<"Active" | "Archived" | "All">("Active");
  const [q, setQ] = useState("");
  const [items, setItems] = useState<MaritimeRow[]>([]);
  const [nextAfter, setNextAfter] = useState<string | null>(null);
  const [selected, setSelected] = useState<MaritimeRow | null>(null);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<Form>(empty);
  const [ports, setPorts] = useState<ReferenceRow[]>([]);
  const [vessels, setVessels] = useState<ReferenceRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [paging, setPaging] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const generation = useRef(0);
  const pageToken = useRef(0);
  const mutationToken = useRef(0);
  const invalidate = useCallback((cause: unknown) => {
    if (classify(cause)) onAccessInvalidated(organizationError(cause));
  }, [onAccessInvalidated]);
  useEffect(() => () => { mutationToken.current++; }, [organizationId]);

  const reload = useCallback(async () => {
    const serial = ++generation.current;
    ++pageToken.current;
    setPaging(false); setLoading(true); setError("");
    setItems([]); setNextAfter(null); setSelected(null); setCreating(false);
    setPorts([]); setVessels([]);
    try {
      const request = listMaritime(organizationId, kind, q, status);
      if (kind === "voyages") {
        const base = "/api/v1/organizations/" + encodeURIComponent(organizationId);
        const [portPage, vesselPage] = await Promise.all([
          getJson<{ ports: ReferenceRow[] }>(base + "/ports?limit=100&status=Active"),
          getJson<{ vessels: ReferenceRow[] }>(base + "/vessels?limit=100&status=Active")
        ]);
        if (serial !== generation.current) return;
        setPorts(portPage.ports); setVessels(vesselPage.vessels);
      }
      const page = await request;
      if (serial !== generation.current) return;
      setItems(page[kind] ?? []); setNextAfter(page.nextAfter);
    } catch (cause) {
      if (serial !== generation.current) return;
      setItems([]); setPorts([]); setVessels([]); setNextAfter(null);
      setError(organizationError(cause)); invalidate(cause);
    } finally { if (serial === generation.current) setLoading(false); }
  }, [organizationId, kind, q, status, invalidate]);
  useEffect(() => {
    void reload();
    const visible = () => { if (document.visibilityState === "visible") void reload(); };
    document.addEventListener("visibilitychange", visible);
    return () => { generation.current++; pageToken.current++; document.removeEventListener("visibilitychange", visible); };
  }, [reload]);

  async function more() {
    if (!nextAfter || busy || paging || loading) return;
    const serial = generation.current, token = ++pageToken.current;
    setPaging(true);
    try {
      const page = await listMaritime(organizationId, kind, q, status, nextAfter);
      if (serial !== generation.current) return;
      setItems(old => [...old, ...(page[kind] ?? [])]);
      setNextAfter(page.nextAfter);
    } catch (cause) {
      if (serial !== generation.current) return;
      setItems([]); setNextAfter(null); setSelected(null);
      setError(organizationError(cause)); invalidate(cause);
    } finally { if (token === pageToken.current) setPaging(false); }
  }
  async function save(event: FormEvent) {
    event.preventDefault();
    if (!canManage || busy) return;
    const serial = generation.current, token = mutationToken.current;
    setBusy(true); setError("");
    try {
      if (creating) await createMaritime(organizationId, kind, payload(kind, form, true));
      else if (selected) {
        const data = payload(kind, form, false, selected);
        if (!Object.keys(data).length) return;
        await updateMaritime(organizationId, kind, selected.id,
          { ...data, expectedVersion: selected.version });
      } else return;
      if (serial === generation.current) await reload();
    } catch (cause) {
      if (serial !== generation.current) return;
      setItems([]); setSelected(null); setError(organizationError(cause)); invalidate(cause);
    } finally { if (token === mutationToken.current) setBusy(false); }
  }
  async function changeStatus(row: MaritimeRow) {
    if (!canManage || busy) return;
    if (row.recordStatus === "Active" && !window.confirm("Archive this record?")) return;
    const serial = generation.current, token = mutationToken.current;
    setBusy(true); setError("");
    try {
      if (row.recordStatus === "Active") await archiveMaritime(organizationId, kind, row.id, row.version);
      else await updateMaritime(organizationId, kind, row.id,
        { recordStatus: "Active", expectedVersion: row.version });
      if (serial === generation.current) await reload();
    } catch (cause) {
      if (serial !== generation.current) return;
      setItems([]); setSelected(null); setError(organizationError(cause)); invalidate(cause);
    } finally { if (token === mutationToken.current) setBusy(false); }
  }
  const input = (name: keyof Form, label: string, disabled = false) => (
    <label className="text-sm" key={name}>{label}
      <input aria-label={label} value={form[name]} disabled={disabled || !canManage}
        onChange={e => setForm(old => ({ ...old, [name]: e.target.value }))}
        className="mt-1 block w-full rounded bg-slate-950 p-2 disabled:opacity-60" />
    </label>
  );
  return <section className="mt-6 border-t border-slate-700 pt-5">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <h4 className="font-semibold">Voyages and containers</h4>
      <button type="button" disabled={busy || loading || paging} onClick={() => void reload()}
        className="rounded border border-slate-600 px-3 py-2 text-sm">Refresh maritime records</button>
    </div>
    <div className="mt-3 flex flex-wrap gap-2">
      {(["voyages", "containers"] as const).map(value =>
        <button key={value} type="button" onClick={() => {
          generation.current++; mutationToken.current++; setBusy(false); setKind(value);
          setQ(""); setStatus("Active"); setItems([]); setSelected(null); setCreating(false);
        }} className={"rounded px-3 py-2 text-sm " +
          (kind === value ? "bg-cyan-300 text-slate-950" : "border border-slate-600")}>
          {value === "voyages" ? "Voyages" : "Containers"}
        </button>)}
    </div>
    <div className="mt-4 flex flex-wrap items-end gap-3">
      <label className="text-sm">Search {kind}
        <input value={q} maxLength={80} disabled={busy || paging}
          onChange={e => setQ(e.target.value)} className="mt-1 block rounded bg-slate-950 px-3 py-2" />
      </label>
      <label className="text-sm">Record status
        <select value={status} disabled={busy || paging} onChange={e => setStatus(e.target.value as typeof status)}
          className="mt-1 block rounded bg-slate-950 px-3 py-2">
          {["Active", "Archived", "All"].map(v => <option key={v}>{v}</option>)}
        </select>
      </label>
      {canManage && <button type="button" disabled={busy || paging || loading}
        onClick={() => { setCreating(true); setSelected(null); setForm(empty); setError(""); }}
        className="rounded bg-cyan-300 px-3 py-2 text-sm font-semibold text-slate-950">
        Add {kind === "voyages" ? "voyage" : "container"}
      </button>}
    </div>
    {loading && <p className="mt-3 text-sm text-slate-400">Loading {kind}...</p>}
    {error && <p role="alert" className="mt-3 text-sm text-rose-300">{error}</p>}
    {!loading && !error && !items.length && <p className="mt-3 text-sm text-slate-400">No matching {kind}.</p>}
    <ul className="mt-4 space-y-2">
      {items.map(row => <li key={row.id} className="flex flex-wrap items-center justify-between gap-3 rounded border border-slate-700 p-3">
        <div>
          <div className="font-medium">{row.referenceCode}</div>
          <div className="text-xs text-slate-400">
            {kind === "voyages"
              ? (row.voyageStatus ?? "Unknown") + " · " + (row.plannedDepartureAtUtc ?? "Unknown departure") +
                " / " + (row.plannedArrivalAtUtc ?? "Unknown arrival")
              : (row.containerIdentifier ?? "") + " · " + (row.equipmentType ?? "") +
                (row.identifierKind === "Synthetic" ? " · Synthetic" : " · Format validated")}
            {" · " + row.recordStatus}
          </div>
        </div>
        <div className="flex gap-2">
          <button type="button" onClick={() => { setSelected(row); setCreating(false); setForm(valueOf(row)); }}
            className="rounded border border-slate-600 px-3 py-1 text-sm">
            {canManage ? "View or edit" : "Details"}
          </button>
          {canManage && <button type="button" disabled={busy || paging}
            onClick={() => void changeStatus(row)}
            className="rounded border border-slate-600 px-3 py-1 text-sm">
            {row.recordStatus === "Active" ? "Archive" : "Restore"}
          </button>}
        </div>
      </li>)}
    </ul>
    {nextAfter && <button type="button" disabled={busy || paging || loading}
      onClick={() => void more()} className="mt-3 rounded border border-slate-600 px-3 py-2 text-sm">
      Load more maritime records</button>}
    {(selected || creating) && <form onSubmit={e => void save(e)} className="mt-5 grid gap-3 rounded border border-slate-600 p-4 sm:grid-cols-2">
      <h5 className="col-span-full font-semibold">{creating ? "Add record" : "Record details"}</h5>
      {input("referenceCode", "Reference code", !creating)}
      {kind === "voyages" ? <>
        {(["vesselId", "originPortId", "destinationPortId"] as const).map((field) => {
          const source = field === "vesselId" ? vessels : ports;
          return <label key={field} className="text-sm">{field}
            <select aria-label={field} value={form[field]} disabled={!canManage || !creating}
              onChange={e => setForm(old => ({ ...old, [field]: e.target.value }))}
              className="mt-1 block w-full rounded bg-slate-950 p-2 disabled:opacity-60">
              <option value="">Select reference</option>
              {source.map(ref => <option key={ref.id} value={ref.id}>{ref.referenceCode}: {ref.name}</option>)}
            </select>
          </label>;
        })}
        {input("plannedDepartureAtUtc", "Planned departure UTC (ISO 8601)", !creating && form.voyageStatus !== "Planned")}
        {input("plannedArrivalAtUtc", "Planned arrival UTC (ISO 8601)", !creating && form.voyageStatus !== "Planned")}
        {!creating && <>
          <label className="text-sm">Voyage progress
            <select aria-label="Voyage progress" value={form.voyageStatus} disabled={!canManage}
              onChange={e => setForm(old => ({ ...old, voyageStatus: e.target.value }))}
              className="mt-1 block w-full rounded bg-slate-950 p-2 disabled:opacity-60">
              {progress.map(v => <option key={v}>{v}</option>)}
            </select>
          </label>
          {input("actualDepartureAtUtc", "Actual departure UTC (ISO 8601)", Boolean(selected?.actualDepartureAtUtc))}
          {input("actualArrivalAtUtc", "Actual arrival UTC (ISO 8601)", Boolean(selected?.actualArrivalAtUtc))}
        </>}
      </> : <>
        <label className="text-sm">Identifier kind
          <select aria-label="Identifier kind" value={form.identifierKind} disabled={!creating || !canManage}
            onChange={e => setForm(old => ({ ...old, identifierKind: e.target.value as Form["identifierKind"] }))}
            className="mt-1 block w-full rounded bg-slate-950 p-2 disabled:opacity-60">
            <option value="ISO6346">ISO 6346 format</option><option value="Synthetic">Synthetic</option>
          </select>
        </label>
        {input("containerIdentifier", "Container identifier", !creating)}
        <label className="text-sm">Equipment type
          <select value={form.equipmentType} aria-label="Equipment type" disabled={!creating || !canManage}
            onChange={e => setForm(old => ({ ...old, equipmentType: e.target.value }))}
            className="mt-1 block w-full rounded bg-slate-950 p-2 disabled:opacity-60">
            {formats.map(v => <option key={v}>{v}</option>)}
          </select>
        </label>
        {input("displayName", "Display name")}
        {form.identifierKind === "Synthetic" && <p className="text-xs text-amber-200">Synthetic container reference. No registration is implied.</p>}
      </>}
      <div className="col-span-full flex gap-2">
        {canManage && (creating || selected?.recordStatus === "Active") &&
          <button type="submit" disabled={busy || loading} className="rounded bg-cyan-300 px-4 py-2 text-sm text-slate-950">
            {creating ? "Create record" : "Save changes"}
          </button>}
        <button type="button" onClick={() => { setSelected(null); setCreating(false); }}
          className="rounded border border-slate-600 px-4 py-2 text-sm">Close</button>
      </div>
    </form>}
  </section>;
}
