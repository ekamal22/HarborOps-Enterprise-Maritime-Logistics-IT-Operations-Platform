import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import ReferenceDataWorkspace from "../references/ReferenceDataWorkspace";
import MaritimeWorkspace from "../maritime/MaritimeWorkspace";
import ShipmentWorkspace from "../shipments/ShipmentWorkspace";
import NotificationCenter, { type NotificationTarget } from "../notifications/NotificationCenter";
import {
  getJson, mutate, organizationError, OrganizationRequestError,
  type Membership, type OrganizationItem, type OrganizationProfile
} from "./organizationClient";

const roles: OrganizationItem["role"][] = ["Analyst", "Manager", "Support", "Administrator", "Auditor"];
type SessionSelection = { selected: OrganizationItem | null };
type MembershipList = { memberships: Membership[]; nextAfter: string | null };

function MembershipRow({ member, currentUserId, locked, onUpdate, onRemove }: {
  member: Membership; currentUserId: string; locked: boolean;
  onUpdate: (member: Membership, role: OrganizationItem["role"], status: Membership["status"]) => Promise<void>;
  onRemove: (member: Membership) => Promise<void>;
}) {
  const [role, setRole] = useState(member.role);
  const [status, setStatus] = useState(member.status);
  useEffect(() => { setRole(member.role); setStatus(member.status); }, [member.role, member.status, member.version]);
  const own = member.userId === currentUserId;
  return (
    <li className="rounded-lg border border-slate-700 p-3">
      <div className="text-sm font-medium">{member.displayName} <span className="text-slate-400">{member.email}</span></div>
      <p className="mt-1 break-all text-xs text-slate-500">{member.userId}</p>
      <div className="mt-3 flex flex-wrap items-center gap-2">
        <select aria-label={"Role for " + member.displayName} value={role} disabled={locked || own}
          onChange={event => setRole(event.target.value as OrganizationItem["role"])}
          className="rounded bg-slate-950 p-2 text-sm">
          {roles.map(option => <option key={option} value={option}>{option}</option>)}
        </select>
        <select aria-label={"Membership state for " + member.displayName} value={status}
          disabled={locked || own} onChange={event => setStatus(event.target.value as Membership["status"])}
          className="rounded bg-slate-950 p-2 text-sm">
          {["Active", "Suspended", "Removed"].map(option =>
            <option key={option} value={option}>{option}</option>)}
        </select>
        <button disabled={locked || own || (role === member.role && status === member.status)}
          onClick={() => void onUpdate(member, role, status)}
          className="rounded border border-cyan-500 px-3 py-2 text-xs disabled:opacity-50">Save membership</button>
        <button disabled={locked || own || member.status === "Removed"} onClick={() => void onRemove(member)}
          className="rounded border border-rose-700 px-3 py-2 text-xs text-rose-200 disabled:opacity-50">
          Remove
        </button>
      </div>
      {own && <p className="mt-2 text-xs text-slate-400">Your own membership cannot be edited here.</p>}
    </li>
  );
}

export default function OrganizationWorkspace({ currentUserId }: { currentUserId: string }) {
  const [organizations, setOrganizations] = useState<OrganizationItem[]>([]);
  const [selection, setSelection] = useState<OrganizationItem | null>(null);
  const [notificationTarget, setNotificationTarget] = useState<NotificationTarget | null>(null);
  const [profile, setProfile] = useState<OrganizationProfile | null>(null);
  const [members, setMembers] = useState<Membership[]>([]);
  const [nextAfter, setNextAfter] = useState<string | null>(null);
  const [profileName, setProfileName] = useState("");
  const [newUserId, setNewUserId] = useState("");
  const [newRole, setNewRole] = useState<OrganizationItem["role"]>("Analyst");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const sequence = useRef(0);
  const reloads = useRef(0);
  const selectionRef = useRef<OrganizationItem | null>(null);
  const profileRef = useRef<OrganizationProfile | null>(null);
  const profileNameRef = useRef("");
  selectionRef.current = selection;
  profileRef.current = profile;
  profileNameRef.current = profileName;

  const clearProtected = useCallback(() => {
    setProfile(null); setMembers([]); setNextAfter(null); setSelection(null);
    setNotificationTarget(null);
    setProfileName("");
  }, []);

  // A quiet refresh (tab becomes visible again) revalidates access without unmounting the open
  // workspaces, so unsaved work survives. It still fails closed: if the selected organization or
  // role is gone or changed, or access is rejected, protected state is cleared exactly as before.
  // Only a transient service outage leaves the current view in place.
  const refresh = useCallback(async (notice = "", quiet = false) => {
    const serial = ++sequence.current;
    reloads.current += 1;
    const transient = (cause: unknown) =>
      quiet && cause instanceof OrganizationRequestError && cause.status === 503;
    if (!quiet) {
      setLoading(true);
      setError(notice);
      clearProtected();
    }
    try {
      const list = await getJson<{ organizations: OrganizationItem[] }>("/api/v1/me/organizations");
      if (serial !== sequence.current) return;
      setOrganizations(list.organizations);
      let context: SessionSelection;
      try {
        context = await getJson<SessionSelection>("/api/v1/me/organization-context");
      } catch (cause) {
        if (serial === sequence.current) {
          if (quiet && !transient(cause)) clearProtected();
          setError(organizationError(cause));
        }
        return;
      }
      if (serial !== sequence.current) return;
      const selected = context.selected &&
        list.organizations.find(item => item.id === context.selected?.id);
      if (!selected) { if (quiet) clearProtected(); return; }
      const org = await getJson<OrganizationProfile>("/api/v1/organizations/" + selected.id);
      const roster = selected.role === "Administrator"
        ? await getJson<MembershipList>("/api/v1/organizations/" + selected.id + "/memberships?limit=25")
        : null;
      if (serial !== sequence.current) return;
      const unchanged = quiet && selectionRef.current?.id === selected.id &&
        selectionRef.current.role === selected.role;
      const nameEdited = unchanged && profileRef.current !== null &&
        profileNameRef.current !== profileRef.current.displayName;
      setSelection(selected);
      setProfile(org);
      if (!nameEdited) setProfileName(org.displayName);
      setMembers(roster?.memberships ?? []);
      setNextAfter(roster?.nextAfter ?? null);
      if (quiet) setError("");
    } catch (cause) {
      if (serial !== sequence.current) return;
      if (!transient(cause)) clearProtected();
      setError(organizationError(cause));
    } finally {
      reloads.current -= 1;
      if (serial === sequence.current) setLoading(false);
    }
  }, [clearProtected]);

  const lastInvalidation = useRef(0);
  const onReferenceAccessInvalidated = useCallback((message: string) => {
    const now = Date.now();
    const repeated = now - lastInvalidation.current < 10_000;
    lastInvalidation.current = now;
    // Several workspaces can report the same revocation at once. The first report already
    // started the authoritative reload; a second report must not cancel it and strand an
    // empty organization list.
    if (repeated && reloads.current > 0) return;
    sequence.current += 1;
    clearProtected();
    setOrganizations([]);
    // Reload the authoritative list once. If access is rejected again after that reload has
    // finished, stop and leave the notice so a persistent denial cannot trigger a request loop.
    if (repeated) { setLoading(false); setError(message); return; }
    void refresh(message);
  }, [clearProtected, refresh]);

  useEffect(() => {
    void refresh();
    function onVisible() {
      if (document.visibilityState === "visible") void refresh("", true);
    }
    document.addEventListener("visibilitychange", onVisible);
    return () => { sequence.current += 1; document.removeEventListener("visibilitychange", onVisible); };
  }, [refresh]);

  async function act(operation: () => Promise<unknown>) {
    setBusy(true); setError("");
    try {
      await operation();
      await refresh();
    } catch (cause) {
      clearProtected();
      setError(organizationError(cause));
    } finally { setBusy(false); }
  }

  async function choose(id: string) {
    sequence.current += 1;
    clearProtected();
    await act(() => mutate("PUT", "/api/v1/auth/session/organization", { organizationId: id }));
  }

  async function clearSelection() {
    sequence.current += 1;
    clearProtected();
    await act(() => mutate("DELETE", "/api/v1/auth/session/organization"));
  }

  async function updateProfile(event: FormEvent) {
    event.preventDefault();
    if (!selection || !profile) return;
    await act(() => mutate("PATCH", "/api/v1/organizations/" + selection.id,
      { displayName: profileName, expectedVersion: profile.version }));
  }

  async function addMember(event: FormEvent) {
    event.preventDefault();
    if (!selection) return;
    await act(async () => {
      await mutate("PUT", "/api/v1/organizations/" + selection.id + "/memberships/" + newUserId,
        { role: newRole, status: "Active", expectedVersion: null });
      setNewUserId("");
    });
  }

  async function updateMember(member: Membership, role: OrganizationItem["role"],
    status: Membership["status"]) {
    if (!selection) return;
    await act(() => mutate("PUT", "/api/v1/organizations/" + selection.id + "/memberships/" + member.userId,
      { role, status, expectedVersion: member.version }));
  }

  async function removeMember(member: Membership) {
    if (!selection || !window.confirm("Remove this member from the selected organization?")) return;
    await act(() => mutate("DELETE",
      "/api/v1/organizations/" + selection.id + "/memberships/" + member.userId,
      { expectedVersion: member.version }));
  }

  async function loadMore() {
    if (!selection || !nextAfter) return;
    const id = selection.id;
    const next = nextAfter;
    const atStart = sequence.current;
    setBusy(true); setError("");
    try {
      const page = await getJson<MembershipList>(
        "/api/v1/organizations/" + id + "/memberships?limit=25&after=" + encodeURIComponent(next));
      if (sequence.current !== atStart) return;
      setMembers(existing => [...existing, ...page.memberships]);
      setNextAfter(page.nextAfter);
    } catch (cause) { clearProtected(); setError(organizationError(cause)); }
    finally { setBusy(false); }
  }

  return (
    <div className="mt-7 border-t border-slate-700 pt-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h3 className="text-lg font-semibold">Organization workspace</h3>
        <button onClick={() => void refresh()} disabled={busy || loading}
          className="rounded border border-slate-600 px-3 py-2 text-sm disabled:opacity-50">Refresh access</button>
      </div>
      {loading && <p className="mt-3 text-sm text-slate-400">Loading organization access...</p>}
      {!loading && organizations.length === 0 &&
        <p className="mt-4 text-sm text-slate-400">No active organization membership. An administrator must grant access before organization operations are available.</p>}
      {!loading && organizations.length > 0 && (
        <div className="mt-4">
          <label htmlFor="current-organization" className="block text-sm text-slate-300">Active organization</label>
          <select id="current-organization" value={selection?.id ?? ""} disabled={busy}
            onChange={e => e.target.value ? void choose(e.target.value) : void clearSelection()}
            className="mt-2 w-full max-w-md rounded-lg border border-slate-700 bg-slate-950 px-3 py-2">
            <option value="">Select an organization</option>
            {organizations.map(org =>
              <option key={org.id} value={org.id}>{org.displayName} ({org.role})</option>)}
          </select>
        </div>
      )}
      {selection && profile && !loading && (
        <div className="mt-5 rounded-lg border border-slate-700 p-4">
          <p className="font-semibold">{profile.displayName}</p>
          <p className="mt-1 text-xs text-slate-400">Slug: {profile.slug} | Role: {selection.role}</p>
          {(selection.role === "Analyst" || selection.role === "Manager") &&
            <ReferenceDataWorkspace key={selection.id + ":" + selection.role}
              organizationId={selection.id} canManage={selection.role === "Manager"}
              onAccessInvalidated={onReferenceAccessInvalidated} />}
          {(selection.role === "Analyst" || selection.role === "Manager") &&
            <MaritimeWorkspace key={"maritime:" + selection.id + ":" + selection.role}
              organizationId={selection.id} canManage={selection.role === "Manager"}
              onAccessInvalidated={onReferenceAccessInvalidated} />}
          {(selection.role === "Analyst" || selection.role === "Manager" || selection.role === "Auditor") &&
            <NotificationCenter key={"notifications:" + selection.id + ":" + selection.role}
              organizationId={selection.id} onOpen={setNotificationTarget}
              onAccessInvalidated={onReferenceAccessInvalidated} />}
          {(selection.role === "Analyst" || selection.role === "Manager" || selection.role === "Auditor") &&
            <ShipmentWorkspace key={"shipment:" + selection.id + ":" + selection.role}
              organizationId={selection.id} canManage={selection.role !== "Auditor"}
               canManageExceptions={selection.role === "Manager"}
              notificationTarget={notificationTarget}
              onAccessInvalidated={onReferenceAccessInvalidated} />}
          {selection.role === "Administrator" && (
            <div className="mt-5 border-t border-slate-700 pt-5">
              <form onSubmit={e => void updateProfile(e)} className="flex flex-wrap items-end gap-2">
                <label className="text-sm">Organization display name
                  <input value={profileName} maxLength={160} required
                    onChange={e => setProfileName(e.target.value)}
                    className="mt-2 block rounded-lg bg-slate-950 px-3 py-2" />
                </label>
                <button disabled={busy || profileName.trim() === profile.displayName}
                  className="rounded border border-cyan-600 px-3 py-2 text-sm disabled:opacity-50">Update profile</button>
              </form>
              <h4 className="mt-6 font-semibold">Memberships</h4>
              <form onSubmit={e => void addMember(e)} className="mt-3 flex flex-wrap items-end gap-2">
                <label className="text-sm">Existing account ID
                  <input required value={newUserId} onChange={e => setNewUserId(e.target.value)}
                    placeholder="Account UUID" className="mt-2 block rounded-lg bg-slate-950 px-3 py-2" />
                </label>
                <label className="text-sm">Role
                  <select value={newRole} onChange={e => setNewRole(e.target.value as OrganizationItem["role"])}
                    className="mt-2 block rounded-lg bg-slate-950 px-3 py-2">
                    {roles.map(role => <option key={role}>{role}</option>)}
                  </select>
                </label>
                <button disabled={busy} className="rounded bg-cyan-300 px-3 py-2 text-sm font-semibold text-slate-950 disabled:opacity-50">
                  Add member
                </button>
              </form>
              <ul className="mt-5 space-y-3">
                {members.map(member => <MembershipRow key={member.userId} member={member}
                  currentUserId={currentUserId} locked={busy}
                  onUpdate={updateMember} onRemove={removeMember} />)}
              </ul>
              {nextAfter && <button disabled={busy} onClick={() => void loadMore()}
                className="mt-3 rounded border border-slate-600 px-3 py-2 text-sm disabled:opacity-50">
                Load more members
              </button>}
            </div>
          )}
        </div>
      )}
      {error && <p role="alert" className="mt-4 text-sm text-rose-300">{error}</p>}
    </div>
  );
}
