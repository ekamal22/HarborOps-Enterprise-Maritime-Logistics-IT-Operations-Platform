import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import {fileURLToPath} from "node:url";

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const read = relative => fs.readFileSync(path.join(repo, relative), "utf8");

// A readiness sample reaches a query after: its own probe period, the OTLP metric export
// interval and the Prometheus scrape interval. The "current sample" bound used by the
// database alert, the dashboard and the verifiers must exceed that worst case.
function worstCasePipelineSeconds() {
  const probe = Number(read("src/HarborOps.Api/Observability/M17ReadinessProbeService.cs")
    .match(/PeriodicTimer\(TimeSpan\.FromSeconds\((\d+)\)\)/)?.[1]);
  const exportMs = Number(read("compose.m17.observability.override.yaml")
    .match(/OTEL_METRIC_EXPORT_INTERVAL:\s*"(\d+)"/)?.[1]);
  const scrape = Number(read("monitoring/m17/prometheus.yaml")
    .match(/scrape_interval:\s*(\d+)s/)?.[1]);
  assert.ok(probe > 0 && exportMs > 0 && scrape > 0, "Could not read the pipeline intervals");
  return probe + exportMs / 1000 + scrape;
}

const boundsIn = text =>
  [...text.matchAll(/harborops_db_readiness_sample_unix_seconds\s*<\s*(\d+)\)/g)]
    .map(match => Number(match[1]));

test("every current-sample bound exceeds the telemetry pipeline worst case", () => {
  const worst = worstCasePipelineSeconds();
  assert.equal(worst, 45);
  for (const file of ["monitoring/m17/alerts.yaml", "monitoring/m17/grafana/dashboard.json",
    "scripts/check-g17-database-fault.mjs", "scripts/check-g17-restart.mjs"]) {
    const bounds = boundsIn(read(file));
    assert.ok(bounds.length >= 1, file + " has no current-sample bound");
    for (const bound of bounds)
      assert.ok(bound > worst, file + " bound " + bound + "s is not above " + worst + "s");
  }
});

test("the stale-readiness check waits long enough for the newest sample to expire", () => {
  const restart = read("scripts/check-g17-restart.mjs");
  const bound = Math.max(...boundsIn(restart));
  const wait = Number(restart.match(/await wait\((\d+)\);\s*\n\s*const collector/)?.[1]) / 1000;
  // The newest sample can be nearly fresh at shutdown, so only a wait longer than the
  // bound guarantees it has expired.
  assert.ok(wait > bound + 10, "wait " + wait + "s cannot outlast a " + bound + "s bound");
});
