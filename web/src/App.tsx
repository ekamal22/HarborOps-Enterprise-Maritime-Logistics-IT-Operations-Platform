import { useCallback, useEffect, useRef, useState } from "react";
import { parseBaselineStatus } from "./lib/baseline.mjs";
import type { BaselineStatus } from "./lib/baseline.mjs";
import AuthPanel from "./auth/AuthPanel";
import LanguageSwitcher from "./i18n/LanguageSwitcher";
import { useI18n } from "./i18n/useI18n";
import { syncDocumentLanguage } from "./i18n/i18n.mjs";

type ViewState =
  | { status: "loading" }
  | { status: "ready"; data: BaselineStatus }
  | { status: "error"; message: string };

const apiOrigin = "";
const REQUEST_TIMEOUT_MS = 10_000;

export default function App() {
  const { t, date, locale } = useI18n();
  useEffect(() => {
    syncDocumentLanguage();
    document.title = locale === "tr-TR" ? "HarborOps | Sistem Durumu" : "HarborOps | Engineering Baseline";
    const description = document.querySelector('meta[name="description"]');
    if (description) description.setAttribute("content", locale === "tr-TR" ? "HarborOps sistem durumu ve deniz taşımacılığı operasyonları" : "HarborOps engineering baseline");
  }, [locale]);
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
      <header className="sticky top-0 z-50 border-b border-slate-800/80 bg-slate-950/95 backdrop-blur">
        <div className="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-3 px-4 py-3 sm:px-6 sm:py-5">
          <div className="flex items-center gap-3">
            <span className="grid h-10 w-10 place-items-center rounded-xl bg-cyan-400/15 text-cyan-300" aria-hidden="true">◈</span>
            <div><p className="text-lg font-bold tracking-tight">HarborOps</p><p className="text-xs text-slate-400">{t("Enterprise maritime operations")}</p></div>
          </div>
          <div className="flex items-center gap-3"><span className="hidden rounded-full border border-slate-700 px-3 py-1 text-xs text-slate-300 sm:inline">{t("Operations platform")}</span><LanguageSwitcher /></div>
        </div>
      </header>
      <main className="mx-auto max-w-5xl px-6 py-16">
        <p className="mb-3 text-xs font-semibold uppercase tracking-[.2em] text-cyan-300">{t("Live platform status")}</p>
        <h1 className="max-w-2xl text-4xl font-bold tracking-tight sm:text-5xl">{t("Maritime operations overview")}</h1>
        <p className="mt-5 max-w-2xl text-slate-400">{t("Platform services and operational workspaces are available according to your organization permissions. Live vessel positions and predictive ETA are not provided.")}</p>
        <section className="mt-12 rounded-2xl border border-slate-800 bg-slate-900/80 p-7" >
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div><h2 className="text-xl font-semibold">{t("System baseline")}</h2><p className="mt-1 text-sm text-slate-400">{t("Read-only status from /api/v1/system/baseline")}</p></div>
            <button onClick={() => void refresh()} className="rounded-lg bg-cyan-300 px-4 py-2 text-sm font-semibold text-slate-950 transition hover:bg-cyan-200 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-cyan-200">{t("Refresh status")}</button>
          </div>
          {view.status === "loading" && <p className="mt-8 text-slate-300" role="status">{t("Checking database-backed status...")}</p>}
          {view.status === "error" && <div role="alert" className="mt-8 rounded-lg border border-rose-800 bg-rose-950/40 p-4"><p className="font-semibold text-rose-200">{t("Service unavailable")}</p><p className="mt-1 text-sm text-rose-300">{view.message.startsWith("Service returned HTTP ") ? t("Service returned HTTP {status}", {status: view.message.slice(22)}) : t(view.message)}</p></div>}
          {view.status === "ready" && <p className="sr-only" role="status">{t("System baseline status loaded")}</p>}
          {view.status === "ready" && <dl className="mt-8 grid gap-6 sm:grid-cols-2">
            <div><dt className="text-xs uppercase tracking-widest text-slate-400">{t("Service")}</dt><dd className="mt-2 text-lg font-semibold">{view.data.service}</dd></div>
            <div><dt className="text-xs uppercase tracking-widest text-slate-400">{t("Data provenance")}</dt><dd className="mt-2 text-lg font-semibold text-cyan-300">{t("Synthetic demonstration")}</dd></div>
            <div><dt className="text-xs uppercase tracking-widest text-slate-400">{t("Dataset version")}</dt><dd className="mt-2 font-mono text-sm">{view.data.datasetVersion}</dd></div>
            <div><dt className="text-xs uppercase tracking-widest text-slate-400">{t("Seed reference (UTC)")}</dt><dd className="mt-2 font-mono text-sm">{date(view.data.seededAtUtc, {timeZone: "UTC"})} UTC</dd></div>
          </dl>}
        </section>
        <AuthPanel />
        <p className="mt-8 text-xs text-slate-400">{t("Synthetic demo references are not live vessel positions or predictive ETA.")}</p>
      </main>
    </div>
  );
}
