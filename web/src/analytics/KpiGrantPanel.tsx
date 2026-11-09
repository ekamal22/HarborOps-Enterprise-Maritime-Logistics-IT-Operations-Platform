import { useI18n } from "../i18n/useI18n";
import { useEffect, useState, type FormEvent } from "react";
import { organizationError, OrganizationRequestError, type Membership } from "../auth/organizationClient";
import { auditorGrants, setAuditorGrant, type KpiGrant } from "./analyticsClient";

export default function KpiGrantPanel({ organizationId, members, onAccessInvalidated }: {
  organizationId: string; members: Membership[];
  onAccessInvalidated: (message: string) => void;
}) {
  const { t, date, number, locale } = useI18n();
  const [grants, setGrants] = useState<KpiGrant[]>([]);
  const [target, setTarget] = useState("");
  const [enabled, setEnabled] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  useEffect(() => {
    const controller = new AbortController();
    void auditorGrants(organizationId, controller.signal)
      .then(r => { if (!controller.signal.aborted) setGrants(r.grants); })
      .catch(cause => { if (!controller.signal.aborted) setError(organizationError(cause)); });
    return () => controller.abort();
  }, [organizationId]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(target.trim())) {
      setError("Choose an Auditor account"); return;
    }
    setBusy(true); setError(""); setNotice("");
    try {
      const result = await setAuditorGrant(organizationId, target.trim(), enabled);
      if (!result) throw new Error("Grant operation did not return a result");
      setGrants(previous => [...previous.filter(g => g.userId !== target.trim()),
        { userId: target.trim(), active: enabled, version: result.version }]
        .sort((a, b) => a.userId.localeCompare(b.userId)));
      setNotice(enabled ? "Read-only grant enabled." : "Read-only grant revoked.");
    } catch (cause) {
      setError(organizationError(cause));
      if (cause instanceof OrganizationRequestError &&
        (cause.status === 401 || cause.status === 403))
        onAccessInvalidated(organizationError(cause));
    } finally { setBusy(false); }
  }
  return <section className="mt-5 rounded border border-slate-700 p-4" aria-label={t("KPI Auditor grants")}>
    <h4 className="font-semibold">{t("KPI read-only Auditor grants")}</h4>
    <p className="mt-1 text-xs text-slate-400">{t("Only active Auditors with an explicit grant can view operational reports. This does not grant exports, edits or notification access.")}</p>
    <form onSubmit={e => void submit(e)} className="mt-3 flex flex-wrap items-end gap-3 text-sm">
      <label>{t("Auditor account")}<select value={target} onChange={e => setTarget(e.target.value)}
          className="mt-1 block max-w-sm rounded border border-slate-600 bg-slate-950 p-2">
          <option value="">{t("Select Auditor")}</option>
          {members.filter(m => m.role === "Auditor" && m.status === "Active").map(m =>
            <option key={m.userId} value={m.userId}>{m.displayName} ({m.email})</option>)}
        </select>
      </label>
      <label>{t("Or enter Auditor UUID")}<input type="text" value={target} placeholder={t("Account UUID")}
          onChange={e => setTarget(e.target.value)}
          className="mt-1 block max-w-sm rounded border border-slate-600 bg-slate-950 p-2" />
      </label>
      <label>{t("Access")}<select value={enabled ? "enable" : "disable"}
          onChange={e => setEnabled(e.target.value === "enable")}
          className="mt-1 block rounded border border-slate-600 bg-slate-950 p-2">
          <option value="enable">{t("Grant read-only")}</option>
          <option value="disable">{t("Revoke")}</option>
        </select>
      </label>
      <button disabled={busy || !target}
        className="rounded border border-cyan-500 px-3 py-2 disabled:opacity-50">
        {busy ? "Saving..." : "Save grant"}
      </button>
    </form>
    {error && <p role="alert" className="mt-3 text-sm text-rose-300">{error}</p>}
    {notice && <p role="status" className="mt-3 text-sm text-cyan-300">{notice}</p>}
    <ul className="mt-3 space-y-2 text-xs">
      {grants.map(g => <li key={g.userId} className="break-all text-slate-400">
        {members.find(m => m.userId === g.userId)?.displayName ?? g.userId}:
        <strong className={g.active ? "text-cyan-300" : "text-slate-500"}>
          {g.active ? " Enabled" : " Revoked"}
        </strong>
        <span> (version {g.version})</span>
      </li>)}
    </ul>
  </section>;
}