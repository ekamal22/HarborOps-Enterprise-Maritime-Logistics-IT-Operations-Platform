import { useCallback, useEffect, useState } from "react";
import { normalizeApiOrigin } from "./lib/config.mjs";

type BaselineStatus = {
  service: string;
  datasetVersion: string;
  seededAtUtc: string;
  source: string;
};
type ViewState =
  | { status: "loading" }
  | { status: "ready"; data: BaselineStatus }
  | { status: "error"; message: string };

const apiOrigin = normalizeApiOrigin(import.meta.env.VITE_API_BASE_URL);

export default function App() {
  const [view, setView] = useState<ViewState>({ status: "loading" });

  const refresh = useCallback(async (signal?: AbortSignal) => {
    setView({ status: "loading" });
    try {
      const response = await fetch(`${apiOrigin}/api/v1/system/baseline`, {
        signal,
        headers: { Accept: "application/json" }
      });
      if (!response.ok) throw new Error(`Service returned HTTP ${response.status}`);
      const data: BaselineStatus = await response.json();
      if (!data.datasetVersion || data.source !== "synthetic")
        throw new Error("Unexpected baseline response");
      setView({ status: "ready", data });
    } catch (error) {
      if (signal?.aborted) return;
      setView({ status: "error", message: error instanceof Error ? error.message : "Request failed" });
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    void refresh(controller.signal);
    return () => controller.abort();
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
        <h1 className="max-w-2xl text-4xl font-bold tracking-tight sm:text-5xl">Connected from interface to database.</h1>
        <p className="mt-5 max-w-2xl text-slate-400">This foundation verifies a real API response backed by PostgreSQL. Shipment operations, event processing, analytics, and incident management are planned for subsequent milestones.</p>
        <section className="mt-12 rounded-2xl border border-slate-800 bg-slate-900/80 p-7" aria-live="polite">
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div><h2 className="text-xl font-semibold">System baseline</h2><p className="mt-1 text-sm text-slate-400">Read-only status from /api/v1/system/baseline</p></div>
            <button onClick={() => void refresh()} className="rounded-lg bg-cyan-300 px-4 py-2 text-sm font-semibold text-slate-950 transition hover:bg-cyan-200 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-cyan-200">Refresh status</button>
          </div>
          {view.status === "loading" && <p className="mt-8 text-slate-300" role="status">Checking database-backed status...</p>}
          {view.status === "error" && <div className="mt-8 rounded-lg border border-rose-800 bg-rose-950/40 p-4"><p className="font-semibold text-rose-200">Service unavailable</p><p className="mt-1 text-sm text-rose-300">{view.message}</p></div>}
          {view.status === "ready" && <dl className="mt-8 grid gap-6 sm:grid-cols-2">
            <div><dt className="text-xs uppercase tracking-widest text-slate-400">Service</dt><dd className="mt-2 text-lg font-semibold">{view.data.service}</dd></div>
            <div><dt className="text-xs uppercase tracking-widest text-slate-400">Data provenance</dt><dd className="mt-2 text-lg font-semibold text-cyan-300">Synthetic demonstration</dd></div>
            <div><dt className="text-xs uppercase tracking-widest text-slate-400">Dataset version</dt><dd className="mt-2 font-mono text-sm">{view.data.datasetVersion}</dd></div>
            <div><dt className="text-xs uppercase tracking-widest text-slate-400">Seed reference (UTC)</dt><dd className="mt-2 font-mono text-sm">{new Date(view.data.seededAtUtc).toISOString()}</dd></div>
          </dl>}
        </section>
        <p className="mt-8 text-xs text-slate-500">Demo data only. No live vessel information or shipping integrations.</p>
      </main>
    </div>
  );
}
