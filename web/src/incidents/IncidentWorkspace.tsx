import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { useI18n } from "../i18n/useI18n";
import { organizationError, OrganizationRequestError, type OrganizationItem } from "../auth/organizationClient";
import {
  listIncidents, getIncident, createIncident, updateIncident, assignIncident,
  addAction, updateAction, grantIncidentRead, patchIncident, getPrefill,
  listReadGrants, revokeIncidentRead,
  type Incident, type IncidentHistory, type IncidentState, type IncidentPrefill,
  type ReadGrant, type Severity
} from "./incidentClient";

type Props = {
  organizationId: string;
  role: OrganizationItem["role"];
  currentUserId: string;
  onAccessInvalidated: (message: string) => void;
};
const states: Record<IncidentState, IncidentState[]> = {
  New: ["Triage"], Triage: ["Investigating"], Investigating: ["Mitigated"],
  Mitigated: ["Monitoring", "Investigating"], Monitoring: ["Investigating", "Resolved"],
  Resolved: ["Closed"], Closed: []
};
const severities: Severity[] = ["SEV-1", "SEV-2", "SEV-3", "SEV-4"];
const inputClass = "block mt-1 w-full rounded border border-slate-600 bg-slate-950 px-3 py-2";
const buttonClass = "rounded border border-cyan-600 px-3 py-2 text-sm disabled:opacity-50";

// Maps the stable server code to catalogued text so no English server wording reaches the page.
function incidentError(cause: unknown): string {
  if (cause instanceof OrganizationRequestError) {
    switch (cause.code) {
      case "resource_not_found": return "The incident was not found";
      case "invalid_request": return "The incident request was not valid";
      case "stale_or_invalid_cursor": return "The incident list is out of date";
      case "recovery_not_verified": return "Recovery could not be verified by the service";
      case "recovery_unsupported": return "Recovery verification is not available for this service";
      case "conflict":
        return cause.status === 409
          ? "The incident changed or the operation conflicts with its current state"
          : organizationError(cause);
      default: break;
    }
  }
  return organizationError(cause);
}

// A failure that proves the command was not applied lets the next attempt use a new key.
// A network error, a 5xx or an unknown outcome keeps the key, so a retry of the same command
// is recognised by the server instead of being applied twice.
function definitive(cause: unknown): boolean {
  return cause instanceof OrganizationRequestError && cause.status >= 400 && cause.status < 500 &&
    cause.status !== 408 && cause.status !== 429;
}

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
  const [conflict,setConflict] = useState(false);
  const [title,setTitle] = useState("");
  const [service,setService] = useState("api");
  const [impact,setImpact] = useState("service_unavailable");
  const [reportedImpact,setReportedImpact] = useState("service_degraded");
  const [newSeverity,setNewSeverity] = useState<Severity>("SEV-3");
  const [severity,setSeverity] = useState<Severity>("SEV-3");
  const [note,setNote] = useState("");
  const [reason,setReason] = useState("incident_review");
  const [rootCause,setRootCause] = useState("undetermined");
  const [assignee,setAssignee] = useState(currentUserId);
  const [actionTitle,setActionTitle] = useState("");
  const [auditorId,setAuditorId] = useState("");
  const [grants,setGrants] = useState<ReadGrant[]>([]);
  const [observation,setObservation] = useState("service_degraded");
  const [prefill,setPrefill] = useState<IncidentPrefill | null>(null);
  const sequence=useRef(0);
  const active=useRef(true);
  const pendingCreate=useRef<{ fingerprint: string; key: string; detectedAtUtc: string } | null>(null);
  const operationKeys=useRef(new Map<string,string>());

  const report = useCallback((cause: unknown) => {
    setError(incidentError(cause));
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

  const refreshGrants = useCallback(async () => {
    if(role!=="Administrator")return;
    try{
      const result=await listReadGrants(organizationId);
      if(active.current)setGrants(result);
    }catch(cause){if(active.current)report(cause);}
  },[organizationId,role,report]);

  useEffect(()=>{
    active.current=true;
    void refresh();
    void refreshGrants();
    return ()=>{active.current=false;sequence.current+=1};
  },[refresh,refreshGrants]);

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
    }catch(cause){
      if(!active.current)return;
      report(cause);
      // A cursor is only valid for a short time; restart the list instead of leaving a dead button.
      if(cause instanceof OrganizationRequestError && cause.code==="stale_or_invalid_cursor")
        void refresh();
    }
    finally{if(active.current)setPaging(false);}
  }

  async function open(item: Incident) {
    setBusy(true);
    setError("");
    setConflict(false);
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

  // Runs one command. The operation key is stable for an identical command on an identical
  // incident version, so retrying after a lost response replays instead of repeating.
  async function execute(name: string, payload: unknown,
    operation: (key:string)=>Promise<Incident>, after?: ()=>void) {
    if(!summary)return;
    const fingerprint=JSON.stringify([name,summary.id,summary.version,payload]);
    const key=operationKeys.current.get(fingerprint) ?? crypto.randomUUID();
    operationKeys.current.set(fingerprint,key);
    setBusy(true);setError("");setConflict(false);
    try{
      const result=await operation(key);
      if(!active.current)return;
      operationKeys.current.delete(fingerprint);
      after?.();
      await refresh();
      await open(result);
    }catch(cause){
      if(!active.current)return;
      if(definitive(cause))operationKeys.current.delete(fingerprint);
      report(cause);
      if(cause instanceof OrganizationRequestError && cause.status===409) {
        // Keep the user's draft and the visible conflict while refreshing server state.
        try {
          const latest=await getIncident(organizationId,summary.id,canRead);
          if(active.current) {
            if("events" in latest){setSelected(latest);setSummary(latest.incident);}
            else {setSelected(null);setSummary(latest);}
            setConflict(true);
          }
        }catch { /* The conflict remains visible until the next explicit refresh. */ }
      }
    }finally{if(active.current)setBusy(false);}
  }

  async function usePrefill() {
    setBusy(true);setError("");
    try{
      const result=await getPrefill(organizationId);
      if(!active.current)return;
      setPrefill(result);
      setService(result.serviceCode);
      setTitle(current=>current.trim()?current:result.suggestedTitle);
    }catch(cause){if(active.current)report(cause);}
    finally{if(active.current)setBusy(false);}
  }

  async function create(event: FormEvent) {
    event.preventDefault();
    setBusy(true);setError("");
    try {
      const token=prefill && service===prefill.serviceCode ? prefill.prefillToken : undefined;
      const fingerprint=JSON.stringify({title,service,severity,impact,token});
      if(!pendingCreate.current || pendingCreate.current.fingerprint!==fingerprint) {
        pendingCreate.current={
          fingerprint,
          key:crypto.randomUUID(),
          detectedAtUtc:new Date().toISOString()
        };
      }
      const pending=pendingCreate.current;
      const item=await createIncident(organizationId,{
        title,serviceCode:service,severity,impactCode:impact,
        detectedAtUtc:pending.detectedAtUtc,
        ...(token ? { prefillToken: token } : {})
      },pending.key);
      if(!active.current)return;
      pendingCreate.current=null;
      setTitle("");
      setPrefill(null);
      await refresh();
      await open(item);
    }catch(cause){
      if(!active.current)return;
      // A definitive refusal proves nothing was stored, so the next attempt may use a new key.
      if(definitive(cause))pendingCreate.current=null;
      report(cause);
    }
    finally{if(active.current)setBusy(false);}
  }

  function currentVersion():number {return summary?.version ?? 0;}
  function advance(next: IncidentState) {
    if(!summary)return;
    const body={expectedVersion:currentVersion(),toState:next,reasonCode:reason};
    void execute("transition",body,key=>updateIncident(organizationId,summary.id,"transitions",body,key));
  }
  function reopen() {
    if(!summary)return;
    const body={expectedVersion:currentVersion(),toState:"Investigating",reasonCode:reason};
    void execute("reopen",body,key=>updateIncident(organizationId,summary.id,"reopen",body,key));
  }
  async function addNote(event:FormEvent) {
    event.preventDefault();
    if(!summary)return;
    const body={expectedVersion:currentVersion(),body:note};
    await execute("note",body,key=>updateIncident(organizationId,summary.id,"notes",body,key),
      ()=>setNote(""));
  }
  async function assign(event:FormEvent) {
    event.preventDefault();
    if(!summary)return;
    const body={expectedVersion:currentVersion(),assignedTo:assignee,reasonCode:"assigned_responder"};
    await execute("assign",body,key=>assignIncident(organizationId,summary.id,body,key));
  }
  async function recordAction(event:FormEvent) {
    event.preventDefault();
    if(!summary)return;
    const body={expectedVersion:currentVersion(),title:actionTitle,ownerUserId:currentUserId};
    await execute("action",body,key=>addAction(organizationId,summary.id,body,key),
      ()=>setActionTitle(""));
  }
  async function recordObservation(event:FormEvent) {
    event.preventDefault();
    if(!summary)return;
    const body={expectedVersion:currentVersion(),observationCode:observation};
    await execute("evidence",body,key=>updateIncident(organizationId,summary.id,"evidence",body,key));
  }
  async function updateImpact(event:FormEvent) {
    event.preventDefault();
    if(!summary)return;
    const body={expectedVersion:currentVersion(),impactCode:reportedImpact,reasonCode:"updated_impact"};
    await execute("impact",body,key=>patchIncident(organizationId,summary.id,"impact",body,key));
  }
  async function updateSeverity(event:FormEvent) {
    event.preventDefault();
    if(!summary)return;
    const body={expectedVersion:currentVersion(),severity:newSeverity,reasonCode:"reviewed_severity"};
    await execute("severity",body,key=>patchIncident(organizationId,summary.id,"severity",body,key));
  }
  async function grant(event:FormEvent) {
    event.preventDefault();setBusy(true);setError("");
    try{await grantIncidentRead(organizationId,auditorId);setAuditorId("");await refreshGrants();}
    catch(cause){report(cause);}finally{setBusy(false);}
  }
  async function revoke(item: ReadGrant) {
    setBusy(true);setError("");
    try{await revokeIncidentRead(organizationId,item.userId,item.version);await refreshGrants();}
    catch(cause){report(cause);await refreshGrants();}finally{setBusy(false);}
  }

  return (
    <section className="mt-6 border-t border-slate-700 pt-5" aria-label={t("IT incidents")}
      data-testid="incident-workspace">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h4 className="font-semibold">{t("IT incidents")}</h4>
        <button className={buttonClass} disabled={busy||loading}
          onClick={()=>void refresh()}>{t("Refresh incidents")}</button>
      </div>
      {error && <p role="alert" data-testid="incident-error" className="mt-2 text-sm text-rose-300">{t(error)}</p>}
      {conflict && <p role="status" data-testid="incident-conflict" className="mt-2 text-sm text-amber-300">
        {t("The incident changed. Review the latest state before retrying.")}</p>}
      {loading && <p className="mt-2 text-sm text-slate-400">{t("Loading incidents...")}</p>}

      {canCreate && (
        <form onSubmit={e=>void create(e)} data-testid="incident-create"
          className="mt-4 grid gap-3 rounded border border-slate-700 p-3 md:grid-cols-2">
          <h5 className="md:col-span-2 font-semibold">{t("Report an incident")}</h5>
          <div className="md:col-span-2 text-sm">
            <button type="button" className={buttonClass} disabled={busy}
              onClick={()=>void usePrefill()}>{t("Use current service status")}</button>
            {prefill&&<p className="mt-2" data-testid="incident-prefill">
              {t("Current service status")}: {t(prefill.status)} | {t("Observed")} {date(prefill.observedAtUtc)}
              {!prefill.statusIsCurrent&&<span className="block text-amber-300">{t("Service status is not current")}</span>}
              <span className="block text-xs text-slate-400">{t("Nothing is created until you confirm")}</span>
            </p>}
          </div>
          <label className="text-sm">{t("Incident title")}
            <input className={inputClass} required maxLength={160} value={title}
              onChange={e=>setTitle(e.target.value)}/></label>
          <label className="text-sm">{t("Service code")}
            <select className={inputClass} value={service} onChange={e=>setService(e.target.value)}>
              <option value="api">{t("API service")}</option><option value="database">{t("Database service")}</option>
              <option value="notifications">{t("Notification service")}</option>
              <option value="tracking_held">{t("Held tracking service")}</option>
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
          <ul className="mt-2 space-y-2" data-testid="incident-list">
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
            <div className="rounded border border-slate-700 p-3" data-testid="incident-detail">
              <h5 className="font-semibold">{summary.title}</h5>
              <p className="text-xs text-slate-400" data-testid="incident-state">{t("Incident state")}: {t(summary.state)} |
                {" "}{t("Version")}: {summary.version}</p>
              <p className="mt-2 text-sm">{t("Service code")}: {summary.serviceCode}</p>
              <p className="text-sm">{t("Impact code")}: {summary.impactCode}</p>
              {(role==="Manager"||technical)&&summary.state!=="Closed"&&
                <form onSubmit={e=>void updateImpact(e)} className="mt-3">
                  <label className="block text-sm">{t("Update impact")}
                    <input className={inputClass} required maxLength={80} pattern="[a-z0-9_-]+"
                      value={reportedImpact} onChange={e=>setReportedImpact(e.target.value)}/>
                  </label>
                  <button className={buttonClass+" mt-2"} disabled={busy}>{t("Record updated impact")}</button>
                </form>}
              {!canRead&&<p className="mt-2 text-sm text-slate-400">{t("Technical details are restricted")}</p>}
              {technical&&selected&&<div className="mt-4 space-y-4">
                {summary.state!=="Closed"&&<form onSubmit={e=>void updateSeverity(e)}>
                  <label className="block text-sm">{t("Reassess severity")}
                    <select className={inputClass} value={newSeverity}
                      onChange={e=>setNewSeverity(e.target.value as Severity)}>
                      {severities.map(value=><option key={value}>{value}</option>)}
                    </select>
                  </label>
                  <button className={buttonClass+" mt-2"} disabled={busy}>{t("Record severity change")}</button>
                </form>}
                <label className="block text-sm">{t("Reason code")}
                  <input className={inputClass} value={reason} maxLength={80} pattern="[a-z0-9_-]+"
                    onChange={e=>setReason(e.target.value)}/></label>
                <div className="flex flex-wrap gap-2">
                  {states[selected.incident.state].map(state=>
                    <button key={state} className={buttonClass} disabled={busy}
                      data-testid={"move-"+state}
                      onClick={()=>advance(state)}>{t("Move to")} {t(state)}</button>)}
                  {(summary.state==="Resolved"||summary.state==="Closed")&&
                    <button className={buttonClass} disabled={busy} data-testid="reopen"
                      onClick={reopen}>{t("Reopen incident")}</button>}
                  <button className={buttonClass} disabled={busy}
                    onClick={()=>{const body={expectedVersion:currentVersion()};
                      void execute("m17-snapshot",body,key=>updateIncident(organizationId,summary.id,"m17-snapshot",body,key));}}
                    >{t("Capture safe service status")}</button>
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
                  <button className={buttonClass+" mt-2"} disabled={busy} data-testid="verify-recovery"
                    onClick={()=>{const body={expectedVersion:currentVersion(),rootCauseCode:rootCause};
                      void execute("recovery",body,key=>updateIncident(organizationId,summary.id,"recovery",body,key));}}
                    >{t("Verify recovery")}</button>
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
                    onClick={()=>{const body={expectedVersion:currentVersion(),action:a.id,version:a.version,to:"InProgress"};
                      void execute("action-progress",body,key=>updateAction(organizationId,summary.id,a,
                        "InProgress",currentVersion(),key));}}>{t("Start action")}</button>}
                  {a.state==="InProgress"&&<button className={buttonClass+" ml-2"} disabled={busy}
                    onClick={()=>{const body={expectedVersion:currentVersion(),action:a.id,version:a.version,to:"Verified"};
                      void execute("action-verify",body,key=>updateAction(organizationId,summary.id,a,
                        "Verified",currentVersion(),key));}}>{t("Verify action")}</button>}
                </div>)}
                <h6 className="font-semibold">{t("Investigation notes")}</h6>
                {selected.notes.map(n=><p key={n.id} className="whitespace-pre-wrap break-words rounded bg-slate-900 p-2 text-sm">
                  {n.body}<span className="block text-xs text-slate-400">{date(n.occurredAtUtc)}</span></p>)}
                <h6 className="font-semibold">{t("Evidence")}</h6>
                {selected.evidence.map(e=><p key={e.id} className="text-sm">
                  {t(e.kind)}: {e.observationCode} | {t(e.sourceStatus)} | {date(e.observedAtUtc)}
                  {e.correlationDigest&&<span className="block text-xs text-slate-400">{t("Evidence digest")}: {e.correlationDigest.slice(0,12)}</span>}
                </p>)}
                <h6 className="font-semibold">{t("Incident history")}</h6>
                <ol className="space-y-2" data-testid="incident-history">{selected.events.map(e=>
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
      {role==="Administrator"&&<form onSubmit={e=>void grant(e)} className="mt-5 rounded border border-slate-700 p-3"
        data-testid="incident-grants">
        <h5 className="font-semibold">{t("Grant Auditor incident read access")}</h5>
        <label className="mt-2 block text-sm">{t("Auditor UUID")}
          <input className={inputClass} required value={auditorId}
            onChange={e=>setAuditorId(e.target.value)}/></label>
        <button className={buttonClass+" mt-2"} disabled={busy}>{t("Grant 24-hour access")}</button>
        <h6 className="mt-4 font-semibold">{t("Auditor read grants")}</h6>
        {grants.length===0&&<p className="text-sm text-slate-400">{t("No Auditor read grants")}</p>}
        <ul className="mt-2 space-y-2">{grants.map(item=><li key={item.userId} className="text-sm">
          <span className="break-all">{item.userId}</span> | {item.effective?t("Active"):t("Inactive")} |
          {" "}{t("Expires")} {date(item.expiresAtUtc)}
          {item.status==="Active"&&<button type="button" className={buttonClass+" ml-2"} disabled={busy}
            onClick={()=>void revoke(item)}>{t("Revoke access")}</button>}
        </li>)}</ul>
      </form>}
    </section>
  );
}
