import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { useI18n } from "../i18n/useI18n";
import { organizationError, OrganizationRequestError, type OrganizationItem } from "../auth/organizationClient";
import {
  listIncidents, getIncident, createIncident, updateIncident, assignIncident,
  addAction, updateAction, grantIncidentRead,
  type Incident, type IncidentHistory, type IncidentState, type Severity
} from "./incidentClient";

type Props = {
  organizationId: string;
  role: OrganizationItem["role"];
  currentUserId: string;
  onAccessInvalidated: (message: string) => void;
};
const states: Record<IncidentState, IncidentState[]> = {
  New: ["Triage"], Triage: ["Investigating"], Investigating: ["Mitigated"],
  Mitigated: ["Monitoring"], Monitoring: ["Investigating", "Resolved"],
  Resolved: ["Closed"], Closed: []
};
const severities: Severity[] = ["SEV-1", "SEV-2", "SEV-3", "SEV-4"];
const inputClass = "block mt-1 w-full rounded border border-slate-600 bg-slate-950 px-3 py-2";
const buttonClass = "rounded border border-cyan-600 px-3 py-2 text-sm disabled:opacity-50";

export default function IncidentWorkspace({
  organizationId, role, currentUserId, onAccessInvalidated
}: Props) {
  const { t, date } = useI18n();
  const technical = role === "Support" || role === "Administrator";
  const canCreate = technical || role === "Manager";
  const canRead = technical || role === "Auditor";
  const [items,setItems] = useState<Incident[]>([]);
  const [cursor,setCursor] = useState<string | null>(null);
  const [paging,setPaging] = useState(false);
  const [selected,setSelected] = useState<IncidentHistory | null>(null);
  const [summary,setSummary] = useState<Incident | null>(null);
  const [loading,setLoading] = useState(true);
  const [busy,setBusy] = useState(false);
  const [error,setError] = useState("");
  const [title,setTitle] = useState("");
  const [service,setService] = useState("api");
  const [impact,setImpact] = useState("service_unavailable");
  const [severity,setSeverity] = useState<Severity>("SEV-3");
  const [note,setNote] = useState("");
  const [reason,setReason] = useState("incident_review");
  const [rootCause,setRootCause] = useState("undetermined");
  const [assignee,setAssignee] = useState(currentUserId);
  const [actionTitle,setActionTitle] = useState("");
  const [auditorId,setAuditorId] = useState("");
  const [observation,setObservation] = useState("service_degraded");
  const sequence=useRef(0);
  const active=useRef(true);

  const report = useCallback((cause: unknown) => {
    setError(organizationError(cause));
    if (cause instanceof OrganizationRequestError &&
      (cause.status === 401 || cause.code === "organization_access_revoked" ||
        cause.code === "organization_selection_required"))
      onAccessInvalidated(organizationError(cause));
  },[onAccessInvalidated]);

  const refresh = useCallback(async () => {
    const run=++sequence.current;
    setLoading(true);
    try {
      const result=await listIncidents(organizationId);
      if(!active.current || run!==sequence.current)return;
      setItems(result.incidents);
      setCursor(result.nextCursor);
      setError("");
    } catch(cause) {
      if(!active.current || run!==sequence.current)return;
      setItems([]);
      setCursor(null);
      report(cause);
    } finally {
      if(active.current && run===sequence.current)setLoading(false);
    }
  },[organizationId,report]);

  useEffect(()=>{
    active.current=true;
    void refresh();
    return ()=>{active.current=false;sequence.current+=1};
  },[refresh]);

  async function loadMore() {
    if(!cursor || paging || busy)return;
    setPaging(true);
    try{
      const result=await listIncidents(organizationId,cursor);
      if(!active.current)return;
      setItems(previous=>{
        const seen=new Set(previous.map(x=>x.id));
        return [...previous,...result.incidents.filter(x=>!seen.has(x.id))];
      });
      setCursor(result.nextCursor);
    }catch(cause){if(active.current)report(cause);}
    finally{if(active.current)setPaging(false);}
  }

  async function open(item: Incident) {
    setBusy(true);
    setError("");
    try {
      const result=await getIncident(organizationId,item.id,canRead);
      if(!active.current)return;
      if("events" in result){setSelected(result);setSummary(result.incident);}
      else {setSelected(null);setSummary(result);}
    }catch(cause){if(active.current)report(cause);}
    finally{if(active.current)setBusy(false);}
  }

  async function loadHistory() {
    if(!summary || !selected?.nextEventAfter || busy)return;
    const currentId=summary.id;
    setBusy(true);
    try{
      const page=await getIncident(organizationId,currentId,true,selected.nextEventAfter);
      if(!active.current || !("events" in page))return;
      setSelected(previous=>previous && previous.incident.id===currentId
        ? {...previous,events:[...previous.events,...page.events],
          nextEventAfter:page.nextEventAfter}
        : previous);
    }catch(cause){if(active.current)report(cause);}
    finally{if(active.current)setBusy(false);}
  }

  async function execute(operation: ()=>Promise<Incident>, after?: ()=>void) {
    if(!summary)return;
    setBusy(true);setError("");
    try{
      const result=await operation();
      if(!active.current)return;
      after?.();
      await refresh();
      await open(result);
    }catch(cause){
      if(!active.current)return;
      report(cause);
      if(cause instanceof OrganizationRequestError && cause.status===409) {
        // Keep all local drafts; refresh only server state.
        try{await open(summary);}catch{/* Preserve the visible conflict. */}
      }
    }finally{if(active.current)setBusy(false);}
  }

  async function create(event: FormEvent) {
    event.preventDefault();
    setBusy(true);setError("");
    try {
      const item=await createIncident(organizationId,{
        title,serviceCode:service,severity,impactCode:impact,
        detectedAtUtc:new Date().toISOString()
      });
      if(!active.current)return;
      setTitle("");
      await refresh();
      await open(item);
    }catch(cause){if(active.current)report(cause);}
    finally{if(active.current)setBusy(false);}
  }

  function currentVersion():number {return summary?.version ?? 0;}
  function advance(next: IncidentState) {
    if(!summary)return;
    void execute(()=>updateIncident(organizationId,summary.id,"transitions",{
      expectedVersion:currentVersion(),toState:next,reasonCode:reason
    }));
  }
  function reopen() {
    if(!summary)return;
    void execute(()=>updateIncident(organizationId,summary.id,"reopen",{
      expectedVersion:currentVersion(),toState:"Investigating",reasonCode:reason
    }));
  }
  async function addNote(event:FormEvent) {
    event.preventDefault();
    if(!summary)return;
    await execute(()=>updateIncident(organizationId,summary.id,"notes",{
      expectedVersion:currentVersion(),body:note
    }),()=>setNote(""));
  }
  async function assign(event:FormEvent) {
    event.preventDefault();
    if(!summary)return;
    await execute(()=>assignIncident(organizationId,summary.id,{
      expectedVersion:currentVersion(),assignedTo:assignee,reasonCode:"assigned_responder"
    }));
  }
  async function recordAction(event:FormEvent) {
    event.preventDefault();
    if(!summary)return;
    await execute(()=>addAction(organizationId,summary.id,{
      expectedVersion:currentVersion(),title:actionTitle,ownerUserId:currentUserId
    }),()=>setActionTitle(""));
  }
  async function recordObservation(event:FormEvent) {
    event.preventDefault();
    if(!summary)return;
    await execute(()=>updateIncident(organizationId,summary.id,"evidence",{
      expectedVersion:currentVersion(),observationCode:observation
    }));
  }
  async function grant(event:FormEvent) {
    event.preventDefault();setBusy(true);setError("");
    try{await grantIncidentRead(organizationId,auditorId);setAuditorId("");}
    catch(cause){report(cause);}finally{setBusy(false);}
  }

  return (
    <section className="mt-6 border-t border-slate-700 pt-5" aria-label={t("IT incidents")}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h4 className="font-semibold">{t("IT incidents")}</h4>
        <button className={buttonClass} disabled={busy||loading}
          onClick={()=>void refresh()}>{t("Refresh incidents")}</button>
      </div>
      {error && <p role="alert" className="mt-2 text-sm text-rose-300">{t(error)}</p>}
      {loading && <p className="mt-2 text-sm text-slate-400">{t("Loading incidents...")}</p>}

      {canCreate && (
        <form onSubmit={e=>void create(e)} className="mt-4 grid gap-3 rounded border border-slate-700 p-3 md:grid-cols-2">
          <h5 className="md:col-span-2 font-semibold">{t("Report an incident")}</h5>
          <label className="text-sm">{t("Incident title")}
            <input className={inputClass} required maxLength={160} value={title}
              onChange={e=>setTitle(e.target.value)}/></label>
          <label className="text-sm">{t("Service code")}
            <select className={inputClass} value={service} onChange={e=>setService(e.target.value)}>
              <option value="api">api</option><option value="database">database</option>
              <option value="notifications">notifications</option><option value="tracking_held">tracking_held</option>
            </select></label>
          <label className="text-sm">{t("Severity")}
            <select className={inputClass} value={severity}
              onChange={e=>setSeverity(e.target.value as Severity)}>
              {severities.map(s=><option key={s} value={s}>{s}</option>)}
            </select></label>
          <label className="text-sm">{t("Impact code")}
            <input className={inputClass} required maxLength={80} pattern="[a-z0-9_-]+"
              value={impact} onChange={e=>setImpact(e.target.value)}/></label>
          <button className={buttonClass+" md:col-span-2"} disabled={busy||!title.trim()}>
            {t("Create incident")}</button>
        </form>
      )}

      <div className="mt-4 grid gap-3 md:grid-cols-2">
        <div>
          <h5 className="font-semibold">{t("Incident queue")}</h5>
          {!loading&&items.length===0&&<p className="mt-2 text-sm text-slate-400">{t("No incidents found")}</p>}
          <ul className="mt-2 space-y-2">
            {items.map(item=><li key={item.id}>
              <button disabled={busy} onClick={()=>void open(item)}
                className="w-full rounded border border-slate-700 p-3 text-left text-sm disabled:opacity-50">
                <span className="font-semibold">{item.title}</span>
                <span className="block text-slate-400">{item.severity} | {t(item.state)} | {date(item.updatedAtUtc)}</span>
              </button></li>)}
          </ul>
          {cursor&&<button className={buttonClass+" mt-3"} disabled={busy||paging}
            onClick={()=>void loadMore()}>{t("Load more incidents")}</button>}
        </div>
        <div>
          {summary ? (
            <div className="rounded border border-slate-700 p-3">
              <h5 className="font-semibold">{summary.title}</h5>
              <p className="text-xs text-slate-400">{t("Incident state")}: {t(summary.state)} |
                {" "}{t("Version")}: {summary.version}</p>
              <p className="mt-2 text-sm">{t("Service code")}: {summary.serviceCode}</p>
              <p className="text-sm">{t("Impact code")}: {summary.impactCode}</p>
              {!canRead&&<p className="mt-2 text-sm text-slate-400">{t("Technical details are restricted")}</p>}
              {technical&&selected&&<div className="mt-4 space-y-4">
                <label className="block text-sm">{t("Reason code")}
                  <input className={inputClass} value={reason} maxLength={80} pattern="[a-z0-9_-]+"
                    onChange={e=>setReason(e.target.value)}/></label>
                <div className="flex flex-wrap gap-2">
                  {states[selected.incident.state].map(state=>
                    <button key={state} className={buttonClass} disabled={busy}
                      onClick={()=>advance(state)}>{t("Move to")} {t(state)}</button>)}
                  {(summary.state==="Resolved"||summary.state==="Closed")&&
                    <button className={buttonClass} disabled={busy} onClick={reopen}>{t("Reopen incident")}</button>}
                  <button className={buttonClass} disabled={busy}
                    onClick={()=>void execute(()=>updateIncident(organizationId,summary.id,"m17-snapshot",{
                      expectedVersion:currentVersion()
                    }))}>{t("Capture safe service status")}</button>
                </div>
                <form onSubmit={e=>void assign(e)}>
                  <label className="text-sm">{t("Assign responder UUID")}
                    <input className={inputClass} required value={assignee}
                      onChange={e=>setAssignee(e.target.value)}/></label>
                  <button className={buttonClass+" mt-2"} disabled={busy}>{t("Assign")}</button>
                </form>
                <form onSubmit={e=>void addNote(e)}>
                  <label className="text-sm">{t("Investigation note")}
                    <textarea className={inputClass} required maxLength={4096} value={note}
                      onChange={e=>setNote(e.target.value)}/></label>
                  <p className="mt-1 text-xs text-amber-300">{t("Do not paste credentials or customer data into notes")}</p>
                  <button className={buttonClass+" mt-2"} disabled={busy}>{t("Add note")}</button>
                </form>
                <form onSubmit={e=>void recordAction(e)}>
                  <label className="text-sm">{t("Corrective action")}
                    <input className={inputClass} required maxLength={160} value={actionTitle}
                      onChange={e=>setActionTitle(e.target.value)}/></label>
                  <button className={buttonClass+" mt-2"} disabled={busy}>{t("Record action")}</button>
                </form>
                {summary.state==="Monitoring"&&<div>
                  <label className="text-sm">{t("Root cause code")}
                    <input className={inputClass} maxLength={80} pattern="[a-z0-9_-]+"
                      value={rootCause} onChange={e=>setRootCause(e.target.value)}/></label>
                  <button className={buttonClass+" mt-2"} disabled={busy} onClick={()=>void execute(
                    ()=>updateIncident(organizationId,summary.id,"recovery",{
                      expectedVersion:currentVersion(),rootCauseCode:rootCause
                    }))}>{t("Verify recovery")}</button>
                </div>}
                <form onSubmit={e=>void recordObservation(e)}>
                  <label className="text-sm">{t("Manual observation code")}
                    <input className={inputClass} value={observation} maxLength={80} pattern="[a-z0-9_-]+"
                      onChange={e=>setObservation(e.target.value)}/></label>
                  <button className={buttonClass+" mt-2"} disabled={busy}>{t("Record observation")}</button>
                </form>
                <h6 className="font-semibold">{t("Corrective actions")}</h6>
                {selected.actions.map(a=><div key={a.id} className="rounded bg-slate-900 p-2 text-sm">
                  {a.title} | {t(a.state)}
                  {a.state==="Proposed"&&<button className={buttonClass+" ml-2"} disabled={busy}
                    onClick={()=>void execute(()=>updateAction(organizationId,summary.id,a,
                      "InProgress",currentVersion()))}>{t("Start action")}</button>}
                  {a.state==="InProgress"&&<button className={buttonClass+" ml-2"} disabled={busy}
                    onClick={()=>void execute(()=>updateAction(organizationId,summary.id,a,
                      "Verified",currentVersion()))}>{t("Verify action")}</button>}
                </div>)}
                <h6 className="font-semibold">{t("Investigation notes")}</h6>
                {selected.notes.map(n=><p key={n.id} className="whitespace-pre-wrap break-words rounded bg-slate-900 p-2 text-sm">
                  {n.body}<span className="block text-xs text-slate-400">{date(n.occurredAtUtc)}</span></p>)}
                <h6 className="font-semibold">{t("Evidence")}</h6>
                {selected.evidence.map(e=><p key={e.id} className="text-sm">
                  {t(e.kind)}: {e.observationCode} | {t(e.sourceStatus)} | {date(e.observedAtUtc)}</p>)}
                <h6 className="font-semibold">{t("Incident history")}</h6>
                <ol className="space-y-2">{selected.events.map(e=>
                  <li key={e.id} className="border-l border-slate-600 pl-2 text-sm">
                    {e.sequence}. {t(e.kind)} | {e.reasonCode}
                    <span className="block text-xs text-slate-400">{date(e.occurredAtUtc)}</span>
                  </li>)}</ol>
                {selected.nextEventAfter&&<button className={buttonClass+" mt-2"}
                  disabled={busy} onClick={()=>void loadHistory()}>{t("Load older history")}</button>}
              </div>}
              {role==="Auditor"&&selected&&<>
                <h6 className="mt-4 font-semibold">{t("Incident history")}</h6>
                <ol className="mt-2 space-y-1 text-sm">{selected.events.map(e=>
                  <li key={e.id}>{e.sequence}. {t(e.kind)} | {date(e.occurredAtUtc)}</li>)}</ol>
                {selected.nextEventAfter&&<button className={buttonClass+" mt-2"}
                  disabled={busy} onClick={()=>void loadHistory()}>{t("Load older history")}</button>}
              </>}
            </div>
          ):<p className="text-sm text-slate-400">{t("Select an incident to view its details")}</p>}
        </div>
      </div>
      {role==="Administrator"&&<form onSubmit={e=>void grant(e)} className="mt-5 rounded border border-slate-700 p-3">
        <h5 className="font-semibold">{t("Grant Auditor incident read access")}</h5>
        <label className="mt-2 block text-sm">{t("Auditor UUID")}
          <input className={inputClass} required value={auditorId}
            onChange={e=>setAuditorId(e.target.value)}/></label>
        <button className={buttonClass+" mt-2"} disabled={busy}>{t("Grant 24-hour access")}</button>
      </form>}
    </section>
  );
}
