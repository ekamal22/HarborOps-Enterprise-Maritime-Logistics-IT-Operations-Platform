import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { OrganizationRequestError, organizationError } from "../auth/organizationClient";
import { listMaritime, type MaritimeKind, type MaritimeRow } from "../maritime/maritimeClient";
import { addAssignment, closeAssignment, listAssignments,
  type Assignment, type AssignmentKind } from "./assignmentClient";
import type { ShipmentRow } from "../shipments/shipmentClient";

const kinds: AssignmentKind[] = ["container-assignments", "voyage-assignments"];
const label = (kind: AssignmentKind) => kind === "container-assignments" ? "Container" : "Voyage";
const catalog = (kind: AssignmentKind): MaritimeKind =>
  kind === "container-assignments" ? "containers" : "voyages";

function AllocationList({ org, shipment, kind, canManage, onAccessInvalidated }: {
  org: string; shipment: ShipmentRow; kind: AssignmentKind; canManage: boolean;
  onAccessInvalidated: (message: string) => void;
}) {
  const [items, setItems] = useState<Assignment[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [candidates, setCandidates] = useState<MaritimeRow[]>([]);
  const [target, setTarget] = useState("");
  const [start, setStart] = useState("");
  const [end, setEnd] = useState("");
  const [closingId, setClosingId] = useState("");
  const [closeAt, setCloseAt] = useState("");
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [catalogError, setCatalogError] = useState("");
  const [catalogRetry, setCatalogRetry] = useState(0);
  const [refreshToken, setRefreshToken] = useState(0);
  const seq = useRef(0);
  const active = useRef(true);

  const invalidated = useCallback((cause: unknown) => {
    if (cause instanceof OrganizationRequestError &&
      (cause.status === 401 || cause.status === 403 ||
        cause.code === "organization_selection_required")) {
      onAccessInvalidated(organizationError(cause));
    }
  }, [onAccessInvalidated]);

  useEffect(() => {
    active.current = true;
    const at = ++seq.current;
    setLoading(true); setError("");
    void listAssignments(org, shipment.id, kind).then(page => {
      if (!active.current || at !== seq.current) return;
      setItems(page.assignments); setCursor(page.nextAfter);
    }).catch(cause => {
      if (!active.current || at !== seq.current) return;
      setItems([]); setCursor(null);
      setError(organizationError(cause)); invalidated(cause);
    }).finally(() => { if (active.current && at === seq.current) setLoading(false); });
    return () => { active.current = false; ++seq.current; };
  }, [org, shipment.id, kind, refreshToken, invalidated]);

  useEffect(() => {
    if (!canManage || shipment.recordStatus !== "Active") return;
    let active = true;
    setCatalogError("");
    void (async () => {
      let after: string | undefined;
      const seen = new Set<string>();
      const rows: MaritimeRow[] = [];
      do {
        const page = await listMaritime(org, catalog(kind), "", "Active", after);
        for (const row of (kind === "container-assignments" ? page.containers : page.voyages) ?? []) {
          if (!seen.has(row.id) && (kind !== "voyage-assignments" ||
            row.voyageStatus === "Planned" || row.voyageStatus === "Underway") &&
            (kind !== "voyage-assignments" ||
              (row.originPortId === shipment.originPortId &&
               row.destinationPortId === shipment.destinationPortId))) {
            seen.add(row.id); rows.push(row);
          }
        }
        if (!page.nextAfter) break;
        if (page.nextAfter === after || seen.size > 10000)
          throw new Error("Catalog pagination failed");
        after = page.nextAfter;
      } while (true);
      if (active) setCandidates(rows);
    })().catch(cause => {
      if (active) { setCatalogError(organizationError(cause)); invalidated(cause); }
    });
    return () => { active = false; };
  }, [org, kind, canManage, shipment.id, shipment.originPortId,
    shipment.destinationPortId, shipment.recordStatus, catalogRetry, invalidated]);

  const refresh = () => setRefreshToken(x => x + 1);

  async function create(event: FormEvent) {
    event.preventDefault();
    if (busy || !canManage || !target || !start.trim()) return;
    setBusy(true); setError("");
    try {
      await addAssignment(org, shipment.id, kind, target, start.trim(), end.trim() || null);
      setTarget(""); setStart(""); setEnd(""); refresh();
    } catch (cause) {
      setError(organizationError(cause)); invalidated(cause);
    } finally { setBusy(false); }
  }

  async function close(event: FormEvent, row: Assignment) {
    event.preventDefault();
    if (busy || !canManage || !closeAt.trim()) return;
    setBusy(true); setError("");
    try {
      await closeAssignment(org, shipment.id, kind, row.id, row.version, closeAt.trim());
      setClosingId(""); setCloseAt(""); refresh();
    } catch (cause) {
      setError(organizationError(cause)); invalidated(cause);
    } finally { setBusy(false); }
  }

  async function more() {
    if (busy || !cursor) return;
    setBusy(true);
    const at = seq.current;
    try {
      const page = await listAssignments(org, shipment.id, kind, cursor);
      if (at !== seq.current || !active.current) return;
      setItems(old => [...old, ...page.assignments.filter(x => !old.some(y => y.id === x.id))]);
      setCursor(page.nextAfter);
    } catch (cause) {
      if (at === seq.current) { setError(organizationError(cause)); invalidated(cause); }
    } finally { setBusy(false); }
  }

  return <div className="mt-4 rounded border border-slate-700 p-3">
    <div className="flex flex-wrap items-center justify-between gap-2">
      <h6 className="font-medium">{label(kind)} assignments</h6>
      <button type="button" className="rounded border border-slate-600 px-2 py-1 text-xs"
        disabled={loading || busy} onClick={refresh}>Refresh assignments</button>
    </div>
    {loading && <p className="mt-2 text-xs text-slate-400">Loading assignments...</p>}
    {error && <p role="alert" className="mt-2 text-sm text-rose-300">{error}</p>}
    <ul className="mt-2 space-y-2">
      {items.map(row => <li key={row.id} className="rounded bg-slate-900 p-2 text-xs">
        <div className="break-all">{label(kind)}: {row.targetLabel ?? row.containerId ?? row.voyageId}</div>
        <div>From {row.startsAtUtc} to {row.endsAtUtc ?? "open"} | version {row.version}</div>
        {canManage && row.endsAtUtc === null &&
          (closingId === row.id
            ? <form className="mt-2 flex flex-wrap items-end gap-2" onSubmit={event => void close(event, row)}>
                <label>End UTC (ISO 8601)
                  <input aria-label={"End " + label(kind) + " assignment UTC"}
                    className="mt-1 block rounded bg-slate-950 p-2"
                    value={closeAt} onChange={e => setCloseAt(e.target.value)} required />
                </label>
                <button className="rounded border border-cyan-500 px-2 py-1" disabled={busy}>Confirm closure</button>
                <button type="button" className="rounded border px-2 py-1"
                  onClick={() => setClosingId("")}>Cancel</button>
              </form>
            : <button type="button" disabled={busy}
                onClick={() => { setClosingId(row.id); setCloseAt(""); }}
                className="mt-2 rounded border border-slate-500 px-2 py-1">Close assignment</button>)}
      </li>)}
    </ul>
    {items.length === 0 && !loading && <p className="mt-2 text-xs text-slate-400">No assignments recorded.</p>}
    {cursor && <button type="button" disabled={busy || loading}
      onClick={() => void more()} className="mt-2 rounded border px-2 py-1 text-xs">
      Load more assignments
    </button>}
    {canManage && shipment.recordStatus === "Active" && shipment.workflowStatus !== "Cancelled" &&
      shipment.workflowStatus !== "Completed" && <>
      <form className="mt-4 grid gap-2" onSubmit={event => void create(event)}>
        <label className="text-xs">Select {label(kind).toLowerCase()}
          <select aria-label={"Assign " + label(kind).toLowerCase()} required value={target}
            onChange={e => setTarget(e.target.value)}
            className="mt-1 block w-full rounded bg-slate-950 p-2">
            <option value="">Select {label(kind).toLowerCase()}</option>
            {candidates.map(row => <option key={row.id} value={row.id}>
              {row.referenceCode} {row.containerIdentifier ?? row.displayName ?? ""}
            </option>)}
          </select>
        </label>
        <label className="text-xs">Start UTC (ISO 8601)
          <input aria-label={"Start " + label(kind) + " assignment UTC"} required value={start}
            onChange={e => setStart(e.target.value)} className="mt-1 block w-full rounded bg-slate-950 p-2" />
        </label>
        <label className="text-xs">End UTC (optional)
          <input aria-label={"Optional end " + label(kind) + " assignment UTC"} value={end}
            onChange={e => setEnd(e.target.value)} className="mt-1 block w-full rounded bg-slate-950 p-2" />
        </label>
        <button disabled={busy || !target || !start.trim()}
          className="rounded bg-cyan-300 px-3 py-2 text-xs font-medium text-slate-950 disabled:opacity-50">
          Attach {label(kind).toLowerCase()}
        </button>
      </form>
      {catalogError && <p role="alert" className="mt-2 text-xs text-amber-300">
        Catalog unavailable: {catalogError}
        <button type="button" className="ml-2 underline" onClick={() => setCatalogRetry(x => x + 1)}>Retry catalog</button>
      </p>}
    </>}
  </div>;
}

export default function ShipmentAssignmentsPanel({ organizationId, shipment,
  canManage, onAccessInvalidated }: {
  organizationId: string; shipment: ShipmentRow; canManage: boolean;
  onAccessInvalidated: (message: string) => void;
}) {
  return <section aria-label="Shipment allocations" className="mt-4">
    <h6 className="font-semibold">Shipment allocations</h6>
    <p className="mt-1 text-xs text-slate-400">Administrative allocations only. No physical loading or tracking event is implied.</p>
    {kinds.map(kind => <AllocationList key={kind} org={organizationId} shipment={shipment}
      kind={kind} canManage={canManage} onAccessInvalidated={onAccessInvalidated} />)}
  </section>;
}
