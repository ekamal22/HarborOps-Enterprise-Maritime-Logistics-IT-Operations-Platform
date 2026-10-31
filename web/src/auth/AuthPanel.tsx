import { useCallback, useEffect, useState, type FormEvent } from "react";
import OrganizationWorkspace from "./OrganizationWorkspace";

type User = { id: string; email: string; displayName: string };
type SessionState = { user: User; session: { expiresAtUtc: string } };
type ServerError = { code?: string; message?: string };

async function csrf(): Promise<string> {
  const response = await fetch("/api/v1/auth/csrf", { credentials: "same-origin", cache: "no-store" });
  if (!response.ok) throw new Error("Unable to obtain a secure session token");
  const payload: { token: string } = await response.json();
  return payload.token;
}

async function post(path: string, payload: object): Promise<Response> {
  const token = await csrf();
  return fetch("/api/v1/auth/" + path, {
    method: "POST", credentials: "same-origin", cache: "no-store",
    headers: { "Content-Type": "application/json", "X-HarborOps-CSRF": token },
    body: JSON.stringify(payload)
  });
}

async function failure(response: Response): Promise<string> {
  const body = await response.json().catch(() => ({})) as ServerError;
  if (response.status === 429) return "Too many requests. Try again later.";
  if (response.status === 503) return "Authentication service is temporarily unavailable.";
  return body.message || "The request could not be completed.";
}

export default function AuthPanel() {
  const [current, setCurrent] = useState<SessionState | null>(null);
  const [checking, setChecking] = useState(true);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const refresh = useCallback(async () => {
    try {
      const response = await fetch("/api/v1/auth/session", { credentials: "same-origin", cache: "no-store" });
      if (response.status === 401) { setCurrent(null); return; }
      if (!response.ok) throw new Error(await failure(response));
      setCurrent(await response.json() as SessionState);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Session check failed");
    } finally { setChecking(false); }
  }, []);

  useEffect(() => { void refresh(); }, [refresh]);

  async function signIn(event: FormEvent) {
    event.preventDefault();
    setBusy(true); setError("");
    try {
      const response = await post("login", { email, password });
      if (!response.ok) throw new Error(await failure(response));
      setCurrent(await response.json() as SessionState);
      setPassword("");
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Sign-in failed");
    } finally { setBusy(false); }
  }

  async function operation(action: "logout" | "logout-all" | "account/disable-self") {
    if (action === "account/disable-self" &&
        !window.confirm("Disable this account and revoke every session?")) return;
    setBusy(true); setError("");
    try {
      const response = await post(action, action === "account/disable-self"
        ? { password: confirmation } : {});
      if (!response.ok) throw new Error(await failure(response));
      setCurrent(null); setConfirmation(""); setPassword("");
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Session operation failed");
    } finally { setBusy(false); }
  }

  return (
    <section className="mt-8 rounded-2xl border border-slate-800 bg-slate-900/80 p-7">
      <h2 className="text-xl font-semibold">Secure access</h2>
      {checking ? <p className="mt-4 text-sm text-slate-300">Checking session...</p>
        : current ? <>
          <p className="mt-4 text-slate-300">Signed in as <strong>{current.user.displayName}</strong></p>
          <p className="mt-1 text-sm text-slate-400">{current.user.email}</p>
          <p className="mt-2 text-xs text-slate-400">Session expires {new Date(current.session.expiresAtUtc).toLocaleString()}</p>
          <div className="mt-5 flex flex-wrap gap-3">
            <button disabled={busy} onClick={() => void operation("logout")} className="rounded-lg bg-cyan-300 px-4 py-2 font-semibold text-slate-950 disabled:opacity-50">Log out</button>
            <button disabled={busy} onClick={() => void operation("logout-all")} className="rounded-lg border border-slate-600 px-4 py-2 disabled:opacity-50">Log out all devices</button>
          </div>
          <OrganizationWorkspace currentUserId={current.user.id} />
          <div className="mt-6 border-t border-slate-700 pt-5">
            <label htmlFor="confirmation" className="block text-sm text-slate-300">Password confirmation to disable account</label>
            <input id="confirmation" type="password" autoComplete="current-password" value={confirmation}
              onChange={e => setConfirmation(e.target.value)} className="mt-2 block w-full max-w-sm rounded-lg bg-slate-950 px-3 py-2 text-slate-100" />
            <button disabled={busy || !confirmation} onClick={() => void operation("account/disable-self")}
              className="mt-3 rounded-lg border border-rose-500 px-4 py-2 text-rose-200 disabled:opacity-50">Disable my account</button>
          </div>
        </> : <form onSubmit={e => void signIn(e)} className="mt-5 flex max-w-sm flex-col gap-4">
          <label className="flex flex-col gap-2 text-sm">Email
            <input required type="email" autoComplete="username" value={email}
              onChange={e => setEmail(e.target.value)} className="rounded-lg bg-slate-950 px-3 py-2 text-slate-100" /></label>
          <label className="flex flex-col gap-2 text-sm">Password
            <input required type="password" autoComplete="current-password" value={password}
              onChange={e => setPassword(e.target.value)} className="rounded-lg bg-slate-950 px-3 py-2 text-slate-100" /></label>
          <button type="submit" disabled={busy} className="rounded-lg bg-cyan-300 px-4 py-2 font-semibold text-slate-950 disabled:opacity-50">{busy ? "Signing in..." : "Sign in"}</button>
          <p className="text-xs text-slate-400">Accounts are provisioned locally. Public registration is unavailable.</p>
        </form>}
      {error && <p role="alert" className="mt-4 text-sm text-rose-300">{error}</p>}
    </section>
  );
}
