import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import {fileURLToPath} from "node:url";

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const read = relative => fs.readFileSync(path.join(repo, relative), "utf8");

// The collector's Prometheus exporter derives names from the OpenTelemetry
// instrument: dots become underscores, a unit adds a suffix (s -> _seconds,
// 1 -> _ratio) and monotonic counters add _total. Queries that use any other
// name silently match nothing, so every consumer is checked against the source.
const UNIT_SUFFIX = new Map([["s", "_seconds"], ["1", "_ratio"]]);
export function exportedNames(csharp) {
  const names = new Set();
  // Each instrument is one statement; its unit is the final string argument.
  const pattern = /Create(Counter|Histogram|ObservableGauge)<[^>]+>\(\s*"([a-z0-9_.]+)"([^;]*);/g;
  for (const match of csharp.matchAll(pattern)) {
    const [, kind, name, rest] = match;
    const unit = rest.match(/,\s*"([^"]+)"\s*\)\s*$/)?.[1]
      ?? rest.match(/unit:\s*"([^"]+)"/)?.[1] ?? null;
    let exported = "harborops_" + name.replace(/^harborops\./, "").replaceAll(".", "_");
    exported = exported.replace(/^harborops_harborops_/, "harborops_");
    if (unit && UNIT_SUFFIX.has(unit) && !exported.endsWith(UNIT_SUFFIX.get(unit)))
      exported += UNIT_SUFFIX.get(unit);
    if (kind === "Counter" && !exported.endsWith("_total")) exported += "_total";
    names.add(exported);
    if (kind === "Histogram")
      for (const suffix of ["_bucket", "_count", "_sum"]) names.add(exported + suffix);
  }
  return names;
}
function sourceNames() {
  return new Set([
    ...exportedNames(read("src/HarborOps.Api/Observability/M17Telemetry.cs")),
    ...exportedNames(read("src/HarborOps.Worker/Observability/M17WorkerTelemetry.cs"))
  ]);
}
const CONSUMERS = [
  "monitoring/m17/alerts.yaml",
  "monitoring/m17/alert-test.yaml",
  "monitoring/m17/grafana/dashboard.json",
  "tests/browser/m17/dashboard.spec.mjs",
  "scripts/check-g17-database-fault.mjs",
  "scripts/check-g17-collector-fault.mjs",
  "scripts/check-g17-restart.mjs",
  "scripts/check-g17-worker.mjs"
];

test("the exporter name derivation matches the live collector output", () => {
  const names = sourceNames();
  for (const expected of [
    "harborops_db_readiness_ratio", "harborops_service_running_ratio",
    "harborops_telemetry_collector_reachable_ratio",
    "harborops_db_readiness_sample_unix_seconds",
    "harborops_request_duration_seconds_bucket", "harborops_request_failures_total",
    "harborops_telemetry_heartbeat_total", "harborops_worker_backlog_age_seconds",
    "harborops_worker_heartbeat_total", "harborops_worker_attempts_total"
  ]) assert.ok(names.has(expected), "Derivation lost " + expected);
});

test("every metric queried by alerts, dashboard and verifiers is exported by source", () => {
  const names = sourceNames();
  assert.ok(names.size >= 20, "Instrument inventory unexpectedly small");
  for (const file of CONSUMERS) {
    const used = new Set(read(file).match(/harborops_[a-z0-9_]+/g) ?? []);
    assert.ok(used.size > 0, file + " references no M17 metric");
    for (const name of used) {
      if (/^harborops_(api|worker|integration|test)\b/.test(name) &&
        !names.has(name)) continue;
      assert.ok(names.has(name),
        file + " queries " + name + ", which no M17 instrument exports");
    }
  }
});

test("a unit-suffixed gauge queried without its suffix is rejected", () => {
  const names = sourceNames();
  assert.equal(names.has("harborops_db_readiness"), false);
  assert.equal(names.has("harborops_service_running"), false);
  assert.equal(names.has("harborops_telemetry_collector_reachable"), false);
});

test("every verifier that reads a Tempo v2 trace unwraps the trace envelope", () => {
  // Tempo 2.x answers /api/v2/traces/<id> as {trace:{resourceSpans:[...]}}. A reader
  // of the bare resourceSpans path inspects zero spans and either fails or, worse,
  // passes a privacy scan over nothing.
  const readers = fs.readdirSync(path.join(repo, "scripts"))
    .filter(name => /^check-g17-.*\.mjs$/.test(name))
    .filter(name => read("scripts/" + name).includes("/api/v2/traces/"));
  assert.ok(readers.length >= 3, "Expected the privacy, restart, journey and retention readers");
  for (const name of readers)
    assert.match(read("scripts/" + name), /trace\?\.resourceSpans/,
      name + " does not unwrap the Tempo v2 trace envelope");
});
