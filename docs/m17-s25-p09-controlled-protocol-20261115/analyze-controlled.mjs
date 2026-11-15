// Pre-registered analysis of complete P09 measurements. Read-only: it never changes a gate, a budget or a method.
// usage: node analyze-controlled.mjs <out.json> <label=path-to-m17-performance-raw.json>... [--diagnostics label=dir]...
// The gate for every run is the approved rule, evaluated by the repository's own recalculateP09. Everything else is
// characterization and is reported next to it, never in place of it.
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath, pathToFileURL} from "node:url";
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../..");
const {recalculateP09} = await import(pathToFileURL(path.join(repo, "scripts/g17-p09-review.mjs")).href);

const args = process.argv.slice(2);
const out = args.shift();
const runs = [], diag = new Map();
for (let i = 0; i < args.length; i++) {
  if (args[i] === "--diagnostics") {const [l, d] = args[++i].split("="); diag.set(l, d);}
  else {const [l, p] = args[i].split("="); runs.push({label: l, raw: JSON.parse(fs.readFileSync(p, "utf8"))});}
}
const BURST_RATIO = 5;      // pre-registered before any new run, chosen after seeing f6 and f7 (disclosed): worst 100-sample window p95 >= 5 x the row median on either route
const WINDOW = 100, BLOCK = 50, RESAMPLES = 2000, SEED = 12345;
const q = (a, p) => {const s = [...a].sort((x, y) => x - y); return s[Math.min(s.length - 1, Math.ceil(s.length * p) - 1)];};
const p95 = a => q(a, 0.95);
const mean = a => a.reduce((x, y) => x + y, 0) / a.length;
const pct = (on, off) => 100 * (on / off - 1);
let seed = SEED;
const rnd = () => {seed = (seed * 1664525 + 1013904223) % 4294967296; return seed / 4294967296;};
const lf = n => {let s = 0; for (let i = 2; i <= n; i++) s += Math.log(i); return s;};
function fisherOneSided(a, b, c, d) {   // P(X >= a) for burst rows among telemetry-on rows, hypergeometric
  const n1 = a + b, n2 = c + d, k = a + c, N = n1 + n2;
  const logC = (n, r) => lf(n) - lf(r) - lf(n - r);
  let p = 0;
  for (let x = a; x <= Math.min(n1, k); x++) if (k - x <= n2) p += Math.exp(logC(n1, x) + logC(n2, k - x) - logC(N, k));
  return p;
}

const result = {pre_registration: {burst_ratio: BURST_RATIO, window_samples: WINDOW, bootstrap_block: BLOCK, resamples: RESAMPLES, seed: SEED,
  gate: "approved pooled p95 over three matched pairs <= 10 percent per route and mean paired peak memory <= 15 percent, unchanged"}, runs: {}};
const allRows = [];
for (const {label, raw} of runs) {
  const n = raw.rows[0].raw_ms.baseline.length;
  const gate = recalculateP09(raw, n);
  const rows = raw.rows.map((row, order) => {
    const perRoute = {};
    for (const route of ["baseline", "authorized"]) {
      const a = row.raw_ms[route], med = q(a, 0.5);
      let worst = 0, at = 0;
      for (let i = 0; i + WINDOW <= a.length; i += 10) {const v = p95(a.slice(i, i + WINDOW)); if (v > worst) {worst = v; at = i;}}
      perRoute[route] = {n: a.length, median: +med.toFixed(2), p95: +p95(a).toFixed(2), p99: +q(a, 0.99).toFixed(2), max: +Math.max(...a).toFixed(1),
        over_2x_median: a.filter(x => x > 2 * med).length, worst_window_p95: +worst.toFixed(1), worst_window_start: at,
        worst_window_ratio: +(worst / med).toFixed(2), burst: worst / med >= BURST_RATIO};
    }
    return {label, order: order + 1, round: row.round, telemetry_on: row.telemetry_enabled, rss_peak_mib: row.rss_peak_mib, routes: perRoute,
      burst_row: Object.values(perRoute).some(r => r.burst)};
  });
  allRows.push(...rows);
  result.runs[label] = {gate_result: gate.result, pooled: Object.fromEntries(Object.entries(gate.pooled).map(([k, v]) =>
      [k, {off_p95_ms: +v.off_p95_ms.toFixed(2), on_p95_ms: +v.on_p95_ms.toFixed(2), increase_pct: +v.increase_pct.toFixed(2)}])),
    mean_memory_increase_pct: +gate.mean_memory_increase_pct.toFixed(2),
    pairs: gate.pairs.map(p => ({round: p.round, baseline_pct: +p.routes.baseline.increase_pct.toFixed(2),
      authorized_pct: +p.routes.authorized.increase_pct.toFixed(2), memory_pct: +p.memory_increase_pct.toFixed(2)})),
    execution_order: raw.rows.map(r => r.round + (r.telemetry_enabled ? "-on" : "-off")).join(" "), rows};
}
// Characterization across every complete run, reported separately from the per-run gates.
const labels = runs.map(r => r.label);
const pooledAll = {}, byRun = new Map(runs.map(r => [r.label, r.raw]));
for (const route of ["baseline", "authorized"]) {
  const pick = on => runs.flatMap(r => r.raw.rows.filter(x => x.telemetry_enabled === on).map(x => x.raw_ms[route]));
  const offB = pick(false), onB = pick(true);
  const blocks = sets => sets.map(a => {const b = []; for (let i = 0; i + BLOCK <= a.length; i += BLOCK) b.push(a.slice(i, i + BLOCK)); return b;});
  const draw = bs => bs.flatMap(rb => Array.from({length: rb.length}, () => rb[Math.floor(rnd() * rb.length)]).flat());
  const ob = blocks(offB), nb = blocks(onB), reps = [];
  for (let k = 0; k < RESAMPLES; k++) reps.push(pct(p95(draw(nb)), p95(draw(ob))));
  reps.sort((a, b) => a - b);
  pooledAll[route] = {runs: labels.length, increase_pct: +pct(p95(onB.flat()), p95(offB.flat())).toFixed(2),
    ci95: [+reps[Math.floor(RESAMPLES * 0.025)].toFixed(2), +reps[Math.floor(RESAMPLES * 0.975)].toFixed(2)],
    share_over_10: +(100 * reps.filter(x => x > 10).length / reps.length).toFixed(1)};
}
const on = allRows.filter(r => r.telemetry_on), off = allRows.filter(r => !r.telemetry_on);
const a = on.filter(r => r.burst_row).length, c = off.filter(r => r.burst_row).length;
result.characterization_not_a_gate = {pooled_across_all_runs: pooledAll,
  burst_rows: {telemetry_on: {burst: a, rows: on.length}, telemetry_off: {burst: c, rows: off.length},
    one_sided_fisher_p: +fisherOneSided(a, on.length - a, c, off.length - c).toFixed(4)},
  runs_passing_gate: runs.filter(r => result.runs[r.label].gate_result === "PASS").length, runs_total: runs.length};

// Host attribution for burst rows, when diagnostics were captured (new runs only).
function attribution(label, dir) {
  const readLines = f => fs.existsSync(path.join(dir, f)) ? fs.readFileSync(path.join(dir, f), "utf8").split(/\r?\n/).filter(Boolean) : [];
  const starts = readLines("docker-events.jsonl").map(l => {try {return JSON.parse(l);} catch {return null;}}).filter(e => e && e.Type === "container"
    && e.Action === "start" && e.Actor?.Attributes?.["com.docker.compose.service"] === "api").map(e => Number(e.timeNano) / 1e6);
  const host = readLines("host-counters.csv").slice(1).map(l => l.split('","').map(x => x.replace(/"/g, ""))).map(f => ({t: new Date(f[0]).getTime(),
    cpu: Number(f[1]), priv: Number(f[2]), availMB: Number(f[3]), disk: Number(f[4]), vmmem: Number(f[5])})).filter(x => Number.isFinite(x.t));
  const psi = []; let cur = null;
  for (const l of readLines("vm-pressure.log")) {
    if (l.startsWith("T ")) {const v = Number(l.slice(2)); cur = {t: v > 1e14 ? v / 1e6 : v * 1000}; psi.push(cur);}   // nanoseconds or whole seconds, depending on the sidecar date command
    else if (cur && l.startsWith("some ") && cur.cpuSome === undefined) cur.cpuSome = Number(l.match(/avg10=([\d.]+)/)[1]);
  }
  const rows = result.runs[label].rows;
  return rows.map((row, i) => {
    const t0 = starts[i], t1 = starts[i + 1] ?? (host.at(-1)?.t ?? t0);
    const within = xs => xs.filter(x => x.t >= t0 && x.t <= t1);
    const h = within(host), p = within(psi);
    return {row: i + 1, round: row.round, telemetry_on: row.telemetry_on, burst_row: row.burst_row, window_seconds: t0 ? +((t1 - t0) / 1000).toFixed(0) : null,
      host_cpu_mean: h.length ? +mean(h.map(x => x.cpu)).toFixed(1) : null, host_cpu_max: h.length ? +Math.max(...h.map(x => x.cpu)).toFixed(1) : null,
      host_min_available_mb: h.length ? Math.min(...h.map(x => x.availMB)) : null, vm_cpu_pressure_some_avg10_max: p.length ? Math.max(...p.map(x => x.cpuSome ?? 0)) : null};
  });
}
result.host_attribution = {};
for (const [label, dir] of diag) if (result.runs[label]) result.host_attribution[label] = attribution(label, dir);
fs.writeFileSync(out, JSON.stringify(result, null, 1));
console.log(JSON.stringify({runs: labels.map(l => [l, result.runs[l].gate_result, result.runs[l].pooled.authorized.increase_pct, result.runs[l].mean_memory_increase_pct]),
  characterization: result.characterization_not_a_gate}, null, 1));
