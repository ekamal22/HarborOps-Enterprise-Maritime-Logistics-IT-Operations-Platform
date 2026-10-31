import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { organizationError, OrganizationRequestError } from "../auth/organizationClient";
import {
  listReferences, createReference, updateReference, archiveReference,
  type ReferenceKind, type ReferenceRow, type ReferenceStatus
} from "./referenceClient";

type FormState = {
  referenceCode: string;
  name: string;
  countryCode: string;
  unLocode: string;
  imoNumber: string;
  mmsi: string;
  flagCountryCode: string;
};
const empty: FormState = {
  referenceCode: "", name: "", countryCode: "", unLocode: "",
  imoNumber: "", mmsi: "", flagCountryCode: ""
};
function formOf(row: ReferenceRow): FormState {
  return {
    referenceCode: row.referenceCode,
    name: row.name,
    countryCode: row.countryCode ?? "",
    unLocode: row.unLocode ?? "",
    imoNumber: row.imoNumber ?? "",
    mmsi: row.mmsi ?? "",
    flagCountryCode: row.flagCountryCode ?? ""
  };
}
function payload(kind: ReferenceKind, form: FormState, isNew: boolean): Record<string, unknown> {
  return {
    ...(isNew ? { referenceCode: form.referenceCode.trim() } : {}),
    name: form.name.trim(),
    ...(kind === "ports"
      ? { countryCode: form.countryCode.trim(), unLocode: form.unLocode.trim() || null }
      : { imoNumber: form.imoNumber.trim() || null, mmsi: form.mmsi.trim() || null,
          flagCountryCode: form.flagCountryCode.trim() || null })
  };
}

export default function ReferenceDataWorkspace({ organizationId, canManage, onAccessInvalidated }: {
  organizationId: string;
  canManage: boolean;
  onAccessInvalidated: (message: string) => void;
}) {
  const [kind, setKind] = useState<ReferenceKind>("ports");
  const [q, setQ] = useState("");
  const [status, setStatus] = useState<ReferenceStatus | "All">("Active");
  const [items, setItems] = useState<ReferenceRow[]>([]);
  const [nextAfter, setNextAfter] = useState<string | null>(null);
  const [selected, setSelected] = useState<ReferenceRow | null>(null);
  const [form, setForm] = useState<FormState>(empty);
  const [creating, setCreating] = useState(false);
  const [busy, setBusy] = useState(false);
  const [paging, setPaging] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const generation = useRef(0);
  const mutationGeneration = useRef(0);
  const pagingGeneration = useRef(0);
  const invalidateAccess = useCallback((cause: unknown) => {
    if (cause instanceof OrganizationRequestError &&
        (cause.status === 401 || cause.code === "organization_access_revoked" ||
         cause.code === "organization_selection_required" || cause.code === "access_denied"))
      onAccessInvalidated(organizationError(cause));
  }, [onAccessInvalidated]);
  useEffect(() => () => { mutationGeneration.current += 1; }, [organizationId, kind]);

  const reload = useCallback(async () => {
    const serial = ++generation.current;
    ++pagingGeneration.current;
    setPaging(false);
    setItems([]); setSelected(null); setCreating(false); setNextAfter(null);
    setLoading(true); setError("");
    try {
      const page = await listReferences(organizationId, kind, q, status);
      if (serial !== generation.current) return;
      setItems(page[kind] ?? []);
      setNextAfter(page.nextAfter);
    } catch (cause) {
      if (serial !== generation.current) return;
      setError(organizationError(cause));
      setItems([]);
      invalidateAccess(cause);
    } finally {
      if (serial === generation.current) setLoading(false);
    }
  }, [organizationId, kind, q, status, invalidateAccess]);

  useEffect(() => {
    void reload();
    const onVisibility = () => {
      if (document.visibilityState === "visible") void reload();
    };
    document.addEventListener("visibilitychange", onVisibility);
    return () => {
      generation.current += 1;
      document.removeEventListener("visibilitychange", onVisibility);
    };
  }, [reload]);

  const loadMore = async () => {
    if (!nextAfter || busy || paging || loading) return;
    const serial = generation.current;
    const pageSerial = ++pagingGeneration.current;
    setPaging(true); setError("");
    try {
      const page = await listReferences(organizationId, kind, q, status, nextAfter);
      if (serial !== generation.current) return;
      setItems(rows => [...rows, ...(page[kind] ?? [])]);
      setNextAfter(page.nextAfter);
    } catch (cause) {
      if (serial === generation.current) {
        setError(organizationError(cause));
        setItems([]); setNextAfter(null); setSelected(null);
        invalidateAccess(cause);
      }
    } finally { if (pageSerial === pagingGeneration.current) setPaging(false); }
  };

  const write = async (event: FormEvent) => {
    event.preventDefault();
    if (!canManage || busy) return;
    const serial = generation.current;
    const mutationSerial = mutationGeneration.current;
    setBusy(true); setError("");
    try {
      if (creating) {
        await createReference(organizationId, kind, payload(kind, form, true));
      } else if (selected) {
        await updateReference(organizationId, kind, selected.id,
          { ...payload(kind, form, false), expectedVersion: selected.version });
      } else return;
      if (serial === generation.current) await reload();
    } catch (cause) {
      if (serial === generation.current) {
        setError(organizationError(cause));
        setSelected(null);
        setItems([]); setNextAfter(null);
        invalidateAccess(cause);
      }
    } finally { if (mutationSerial === mutationGeneration.current) setBusy(false); }
  };

  const changeStatus = async (row: ReferenceRow) => {
    if (!canManage || busy) return;
    if (row.status === "Active" && !window.confirm("Archive this reference?")) return;
    const serial = generation.current;
    const mutationSerial = mutationGeneration.current;
    setBusy(true); setError("");
    try {
      if (row.status === "Active")
        await archiveReference(organizationId, kind, row.id, row.version);
      else
        await updateReference(organizationId, kind, row.id,
          { status: "Active", expectedVersion: row.version });
      if (serial === generation.current) await reload();
    } catch (cause) {
      if (serial === generation.current) {
        setError(organizationError(cause));
        setSelected(null); setItems([]); setNextAfter(null);
        invalidateAccess(cause);
      }
    } finally { if (mutationSerial === mutationGeneration.current) setBusy(false); }
  };

  return (
    <section className="mt-6 border-t border-slate-700 pt-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h4 className="font-semibold">Reference data</h4>
        <button className="rounded border border-slate-600 px-3 py-2 text-sm disabled:opacity-50"
          disabled={loading || busy} onClick={() => void reload()}>Refresh references</button>
      </div>
      <div className="mt-3 flex flex-wrap gap-2" role="group" aria-label="Reference type">
        {(["ports", "vessels"] as const).map(value =>
          <button key={value} type="button" disabled={busy || paging} onClick={() => {
            generation.current += 1; mutationGeneration.current += 1; setBusy(false);
            setKind(value); setQ(""); setStatus("Active");
            setSelected(null); setCreating(false); setItems([]);
          }} className={"rounded px-3 py-2 text-sm " +
            (kind === value ? "bg-cyan-300 text-slate-950" : "border border-slate-600")}>
            {value === "ports" ? "Ports" : "Vessels"}
          </button>)}
      </div>
      <div className="mt-4 flex flex-wrap items-end gap-3">
        <label className="text-sm">Search {kind}
          <input value={q} maxLength={80} disabled={busy || paging} onChange={event => setQ(event.target.value)}
            className="mt-1 block rounded bg-slate-950 px-3 py-2"
            placeholder="Name, reference, identifier" />
        </label>
        <label className="text-sm">Reference status
          <select value={status} disabled={busy || paging} onChange={event => setStatus(event.target.value as ReferenceStatus | "All")}
            className="mt-1 block rounded bg-slate-950 px-3 py-2">
            <option>Active</option><option>Archived</option><option>All</option>
          </select>
        </label>
        {canManage && <button type="button" disabled={busy || paging || loading}
          onClick={() => { setCreating(true); setSelected(null); setForm(empty); setError(""); }}
          className="rounded bg-cyan-300 px-3 py-2 text-sm font-semibold text-slate-950">
          Add {kind === "ports" ? "port" : "vessel"}
        </button>}
      </div>
      {loading && <p className="mt-3 text-sm text-slate-400">Loading references...</p>}
      {!loading && !items.length && !error &&
        <p className="mt-3 text-sm text-slate-400">No matching {kind}.</p>}
      <ul className="mt-3 space-y-2">
        {items.map(item =>
          <li key={item.id} className="flex flex-wrap items-center justify-between gap-2 rounded border border-slate-700 p-3">
            <div>
              <div className="text-sm font-medium">{item.name}</div>
              <p className="text-xs text-slate-400">
                {item.referenceCode} · {item.status}
                {kind === "ports" ? " · " + (item.unLocode || item.countryCode) :
                  " · " + (item.imoNumber || item.mmsi || "No external identifier")}
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <button type="button" className="rounded border border-slate-600 px-3 py-1 text-sm"
                onClick={() => { setSelected(item); setCreating(false); setForm(formOf(item)); }}>
                {canManage ? "View or edit" : "Details"}
              </button>
              {canManage && <button type="button" disabled={busy || paging}
                className="rounded border border-slate-600 px-3 py-1 text-sm disabled:opacity-50"
                onClick={() => void changeStatus(item)}>
                {item.status === "Active" ? "Archive" : "Restore"}
              </button>}
            </div>
          </li>)}
      </ul>
      {nextAfter && <button type="button" disabled={busy || paging || loading}
        className="mt-3 rounded border border-slate-600 px-3 py-2 text-sm disabled:opacity-50"
        onClick={() => void loadMore()}>Load more references</button>}
      {(creating || selected) &&
        <div className="mt-5 rounded border border-slate-600 p-4">
          <h5 className="font-semibold">
            {creating ? "Add " + (kind === "ports" ? "port" : "vessel") : "Reference details"}
          </h5>
          <form onSubmit={event => void write(event)} className="mt-3 grid gap-3 sm:grid-cols-2">
            <label className="text-sm">Reference code
              <input required value={form.referenceCode} maxLength={32} disabled={!creating}
                onChange={e => setForm(value => ({ ...value, referenceCode: e.target.value }))}
                className="mt-1 block w-full rounded bg-slate-950 p-2 disabled:opacity-60" />
            </label>
            <label className="text-sm">Name
              <input required maxLength={160} value={form.name} disabled={!canManage}
                onChange={e => setForm(value => ({ ...value, name: e.target.value }))}
                className="mt-1 block w-full rounded bg-slate-950 p-2 disabled:opacity-60" />
            </label>
            {kind === "ports" ? <>
              <label className="text-sm">Country code
                <input required value={form.countryCode} maxLength={2} disabled={!canManage}
                  onChange={e => setForm(value => ({ ...value, countryCode: e.target.value }))}
                  className="mt-1 block w-full rounded bg-slate-950 p-2 disabled:opacity-60" />
              </label>
              <label className="text-sm">UN/LOCODE (optional)
                <input value={form.unLocode} maxLength={6} disabled={!canManage}
                  onChange={e => setForm(value => ({ ...value, unLocode: e.target.value }))}
                  className="mt-1 block w-full rounded bg-slate-950 p-2 disabled:opacity-60" />
              </label>
            </> : <>
              <label className="text-sm">IMO number (optional)
                <input value={form.imoNumber} maxLength={11} disabled={!canManage}
                  onChange={e => setForm(value => ({ ...value, imoNumber: e.target.value }))}
                  className="mt-1 block w-full rounded bg-slate-950 p-2 disabled:opacity-60" />
              </label>
              <label className="text-sm">MMSI (optional)
                <input value={form.mmsi} maxLength={9} disabled={!canManage}
                  onChange={e => setForm(value => ({ ...value, mmsi: e.target.value }))}
                  className="mt-1 block w-full rounded bg-slate-950 p-2 disabled:opacity-60" />
              </label>
              <label className="text-sm">Flag country (optional)
                <input value={form.flagCountryCode} maxLength={2} disabled={!canManage}
                  onChange={e => setForm(value => ({ ...value, flagCountryCode: e.target.value }))}
                  className="mt-1 block w-full rounded bg-slate-950 p-2 disabled:opacity-60" />
              </label>
            </>}
            <div className="sm:col-span-2 flex flex-wrap gap-2">
              {canManage && <button type="submit" disabled={busy || paging}
                className="rounded bg-cyan-300 px-3 py-2 text-sm font-semibold text-slate-950 disabled:opacity-50">
                {creating ? "Create reference" : "Save reference"}
              </button>}
              <button type="button" className="rounded border border-slate-600 px-3 py-2 text-sm"
                onClick={() => { setSelected(null); setCreating(false); }}>Close details</button>
            </div>
          </form>
        </div>}
      {error && <p role="alert" className="mt-3 text-sm text-rose-300">{error}</p>}
    </section>
  );
}
