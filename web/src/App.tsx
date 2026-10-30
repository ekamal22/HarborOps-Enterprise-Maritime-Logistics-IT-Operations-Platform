import { useCallback, useEffect, useRef, useState } from "react";
import { parseBaselineStatus } from "./lib/baseline.mjs";
import type { BaselineStatus } from "./lib/baseline.mjs";
import AuthPanel from "./auth/AuthPanel";

type ViewState =
  | { status: "loading" }
  | { status: "ready"; data: BaselineStatus }
  | { status: "error"; message: string };

const apiOrigin = "";
const REQUEST_TIMEOUT_MS = 10_000;

export default function App() {
  const [view, setView] = useState<ViewState>({ status: "loading" });

  const request = useRef<AbortController | null>(null);

  const refresh = useCallback(async () => {
    request.current?.abort();
    const controller = new AbortController();
    request.current = controller;
    let timedOut = false;
    const timer = window.setTimeout(() => {
      timedOut = true;
      controller.abort();
    }, REQUEST_TIMEOUT_MS);
    setView({ status: "loading" });

    try {
      const response = await fetch(`${apiOrigin}/api/v1/system/baseline`, {
        signal: controller.signal,
        headers: { Accept: "application/json" }
      });
      if (!response.ok) throw new Error(`Service returned HTTP ${response.status}`);
      const data = parseBaselineStatus(await response.json());
      if (!controller.signal.aborted) setView({ status: "ready", data });
    } catch (error) {
      if (timedOut && request.current === controller) {
        setView({ status: "error", message: "The service did not respond in time" });
      } else if (!controller.signal.aborted) {
        setView({ status: "error", message: error instanceof Error ? error.message : "Request failed" });
      }
    } finally {
      window.clearTimeout(timer);
      if (request.current === controller) request.current = null;
    }
  }, []);

  useEffect(() => {
    void refresh();
    return () => request.current?.abort();
  }, [refresh]);

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <header className="border-b border-slate-800/80">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-5">
          <div className="flex items-center gap-3">
            <span className="grid h-10 w-10 place-items-center rounded-xl bg-cyan-400/15 text-cyan-300" aria-hidden="true">◈</span>
            <div><p className="text-lg font-bold tracking-tight">HarborOps</p><p className="text-xs text-slate-400">Enterprise maritime operations</p></div>
          </div>
          <span className="rounded-full border border-slate-700 px-3 py-1 text-xs text-slate-300">M0 foundation</span>
        </div>
      </header>
      <main className="mx-auto max-w-5xl px-6 py-16">
        <p className="mb-3 text-xs font-semibold uppercase tracking-[.2em] text-cyan-300">Platform readiness</p>
        <h1 className="max-w-2xl text-4xl font-bold tracking-tight sm:text-5xl">Foundation status from the live API.</h1>
        <p className="mt-5 max-w-2xl text-slate-400">This page queries a PostgreSQL-backed API to check the engineering foundation. Shipment operations, event processing, analytics, and incident management remain planned for later milestones.</p>
        <section className="mt-12 rounded-2xl border border-slate-800 bg-slate-900/80 p-7" >
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div><h2 className="text-xl font-semibold">System baseline</h2><p className="mt-1 text-sm text-slate-400">Read-only status from /api/v1/system/baseline</p></div>
            <button onClick={() => void refresh()} className="rounded-lg bg-cyan-300 px-4 py-2 text-sm font-semibold text-slate-950 transition hover:bg-cyan-200 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-cyan-200">Refresh status</button>
          </div>
          {view.status === "loading" && <p className="mt-8 text-slate-300" role="status">Checking database-backed status...</p>}
          {view.status === "error" && <div role="alert" className="mt-8 rounded-lg border border-rose-800 bg-rose-950/40 p-4"><p className="font-semibold text-rose-200">Service unavailable</p><p className="mt-1 text-sm text-rose-300">{view.message}</p></div>}
          {view.status === "ready" && <p className="sr-only" role="status">System baseline status loaded</p>}
          {view.status === "ready" && <dl className="mt-8 grid gap-6 sm:grid-cols-2">
            <div><dt className="text-xs uppercase tracking-widest text-slate-400">Service</dt><dd className="mt-2 text-lg font-semibold">{view.data.service}</dd></div>
            <div><dt className="text-xs uppercase tracking-widest text-slate-400">Data provenance</dt><dd className="mt-2 text-lg font-semibold text-cyan-300">Synthetic demonstration</dd></div>
            <div><dt className="text-xs uppercase tracking-widest text-slate-400">Dataset version</dt><dd className="mt-2 font-mono text-sm">{view.data.datasetVersion}</dd></div>
            <div><dt className="text-xs uppercase tracking-widest text-slate-400">Seed reference (UTC)</dt><dd className="mt-2 font-mono text-sm">{new Date(view.data.seededAtUtc).toISOString()}</dd></div>
          </dl>}
        </section>
        <AuthPanel />
        <p className="mt-8 text-xs text-slate-400">Demo data only. No live vessel information or shipping integrations.</p>
      </main>
    </div>
  );
}
