import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import {verifyManifest} from "./verify-g17-manifest.mjs";
import {source, writeCampaignFiles, writeManifest, readJson, writeJson} from "./g17-campaign-fixture.mjs";

const withCampaign = (t, mutate) => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "g17-campaign-"));
  t.after(() => fs.rmSync(dir, {recursive: true, force: true}));
  writeCampaignFiles(dir);
  if (mutate) mutate(dir);
  return {dir, manifest: writeManifest(dir)};
};
const json = (file, fn) => dir => {const v = readJson(dir, file); fn(v); writeJson(dir, file, v);};

test("a complete, consistent synthetic full campaign verifies", t => {
  const {manifest} = withCampaign(t);
  const report = verifyManifest(manifest, source);
  assert.equal(report.complete, true);
  assert.equal(report.gate_acceptance, "NOT_ACCEPTED");
  assert.equal(report.assertions_reviewed, 0);
});

// Every row breaks exactly one fact of the complete campaign. The manifest is rebuilt after
// the change, so file hashes are consistent and only the named evidence fact is wrong.
const CASES = [
  ["ledger claims fewer than 72 technical passes", json("g17-technical-assertion-ledger.json", v => {v.technical_passes = 71;}), /raw atomic technical witnesses/],
  ["ledger technical record without artifact", json("g17-technical-assertion-ledger.json", v => {v.records[0].source_artifact = null;}), /raw atomic technical witnesses/],
  ["ledger record naming a missing artifact", json("g17-technical-assertion-ledger.json", v => {v.records[0].source_artifact = "nowhere.json";}), /raw atomic technical witnesses/],
  ["ledger independent review not pending", json("g17-technical-assertion-ledger.json", v => {v.records[74].status = "TECHNICAL_PASS";}), /raw atomic technical witnesses/],
  ["ledger acceptance not pending", json("g17-technical-assertion-ledger.json", v => {v.acceptance = "ACCEPTED";}), /raw atomic technical witnesses/],
  ["ledger errors present", json("g17-technical-assertion-ledger.json", v => {v.errors = ["x"];}), /raw atomic technical witnesses/],
  ["coverage preflight not strict", json("g17-coverage-preflight.json", v => {v.strict = false;}), /preflight/],
  ["coverage preflight 71 authored", json("g17-coverage-preflight.json", v => {v.technically_authored = 71;}), /preflight/],
  ["network web not internal", json("m17-network-results.json", v => {v.observed.web = ["dashboard_access"];}), /network-isolation/],
  ["network result FAIL", json("m17-network-results.json", v => {v.result = "FAIL";}), /network-isolation/],
  ["network project differs", json("m17-network-results.json", v => {v.project = "ho-verify-m17-other";}), /network-isolation/],
  ["browser case skipped", json("m17-browser-results.json", v => {v.stats.skipped = 1;}), /Incomplete, skipped, flaky or failing/],
  ["cardinality browser case missing", json("m17-cardinality-results.json", v => {v.suites[0].specs = [];}), /Missing, unexpected or duplicated browser cases/],
  ["hidden last-run record reports a failure", json("browser-artifacts/.last-run.json", v => {v.status = "failed"; v.failedTests = ["x"];}), /last-run record/],
  ["hidden cardinality record removed", dir => fs.unlinkSync(path.join(dir, "cardinality-artifacts/.last-run.json")), /Manifest expected-artifact discrepancy|missing evidence|Missing hidden/],
  ["cardinality 9999 paths", json("m17-cardinality-raw.json", v => {v.requested_paths = 9999;}), /Cardinality or memory/],
  ["cardinality not all 404", json("m17-cardinality-raw.json", v => {v.all_requests_returned_404 = false;}), /Cardinality or memory/],
  ["cardinality 501 series", json("m17-cardinality-raw.json", v => {v.exported_series_across_window = 501;}), /Cardinality or memory/],
  ["cardinality no series", json("m17-cardinality-raw.json", v => {v.exported_series_across_window = 0;}), /Cardinality or memory/],
  ["cardinality one memory sample", json("m17-cardinality-raw.json", v => {v.memory_samples = [1];}), /Cardinality or memory/],
  ["privacy report without traces", json("m17-export-privacy-results.json", v => {v.traces_inspected = 0;}), /privacy|retained|differs/i],
  ["memory stress growth 15.1", json("m17-cardinality-memory-results.json", v => {v.post_stress_memory_growth_pct = 15.1;}), /Sustained hostile-path/],
  ["memory stress 39999 paths", json("m17-cardinality-memory-results.json", v => {v.total_paths = 39999;}), /Sustained hostile-path/],
  ["memory stress one pair", json("m17-cardinality-memory-results.json", v => {v.pairs.pop();}), /Sustained hostile-path/],
  ["memory stress pair over 15", json("m17-cardinality-memory-results.json", v => {v.pairs[0].relative_memory_peak_increase_pct = 15.5;}), /Sustained hostile-path/],
  ["memory stress pair failed", json("m17-cardinality-memory-results.json", v => {v.pairs[1].pass = false;}), /Sustained hostile-path/],
  ["restart api not restored", json("m17-restart-results.json", v => {v.api_restored = false;}), /API cancellation, shutdown, restart/],
  ["restart four checks", json("m17-restart-results.json", v => {v.checks.pop();}), /API cancellation, shutdown, restart/],
  ["restart a check failed", json("m17-restart-results.json", v => {v.checks[2].status = "FAIL";}), /API cancellation, shutdown, restart/],
  ["retention policy 12 hours", json("m17-retention-age-results.json", v => {v.policy_hours = 12;}), /24h block-expiry/],
  ["retention no old block removed", json("m17-retention-age-results.json", v => {v.checks[0].old_removed = 0;}), /24h block-expiry/],
  ["retention no new block preserved", json("m17-retention-age-results.json", v => {v.checks[0].new_preserved = 0;}), /24h block-expiry/],
  ["retention only two passing checks", json("m17-retention-age-results.json", v => {v.checks[2].status = "FAIL";}), /24h block-expiry/],
  ["tempo trace not ingested", json("m17-tempo-retention-results.json", v => {v.runtime_trace_ingested = false;}), /selective trace expiry/],
  ["tempo expired trace kept", json("m17-tempo-retention-results.json", v => {v.expired_trace_removed = false;}), /selective trace expiry/],
  ["tempo recent trace lost", json("m17-tempo-retention-results.json", v => {v.recent_trace_preserved = false;}), /selective trace expiry/],
  ["tempo policy 48 hours", json("m17-tempo-retention-results.json", v => {v.reference_policy_hours = 48;}), /selective trace expiry/],
  ["tempo three checks", json("m17-tempo-retention-results.json", v => {v.checks[3].status = "FAIL";}), /selective trace expiry/],
  ["retention matrix metrics 12 hours", json("m17-retention-matrix-results.json", v => {v.metrics_retention_hours = 12;}), /retention evidence is incomplete/],
  ["retention matrix traces 12 hours", json("m17-retention-matrix-results.json", v => {v.traces_retention_hours = 12;}), /retention evidence is incomplete/],
  ["retention matrix two sources", json("m17-retention-matrix-results.json", v => {v.raw_sources.pop();}), /retention evidence is incomplete/],
  ["regression group missing", json("g17-regression-matrix-results.json", v => {delete v.cases.M11;}), /regression groups omitted/],
  ["regression group executed nothing", json("g17-regression-matrix-results.json", v => {v.cases.audit.passed = 0;}), /regression groups omitted/],
  ["regression seven required groups", json("g17-regression-matrix-results.json", v => {v.required_groups = 7;}), /regression groups omitted/],
  ["regression trx with a failure", dir => fs.writeFileSync(path.join(dir, "g17-regressions.trx"),
    '<TestRun><ResultSummary><Counters total="1" passed="0" failed="1" notExecuted="0" /></ResultSummary><UnitTestResult testId="a" testName="N.x" outcome="Failed" /></TestRun>'), /TRX/],
  ["performance five rows", json("m17-performance-raw.json", v => {v.rows.pop();}), /matched P09 overhead pairs/],
  ["performance two pairs", json("m17-performance-raw.json", v => {v.pairs.pop();}), /matched P09 overhead pairs/],
  ["performance gate failed", json("m17-performance-raw.json", v => {v.gate.pass = false;}), /matched P09 overhead pairs/],
  ["performance pooled gate failed", json("m17-performance-raw.json", v => {v.gate.pooled_p95_pass = false;}), /matched P09 overhead pairs/],
  ["performance memory gate failed", json("m17-performance-raw.json", v => {v.gate.memory_pass = false;}), /matched P09 overhead pairs/],
  ["performance pooled p95 over 10", json("m17-performance-raw.json", v => {v.pooled.authorized.p95_increase_pct = 10.1;}), /matched P09 overhead pairs/],
  ["performance too few telemetry samples", json("m17-performance-raw.json", v => {v.pooled.baseline.telemetry_on_samples = 2999;}), /matched P09 overhead pairs/],
  ["performance mean memory over 15", json("m17-performance-raw.json", v => {v.gate.mean_memory_increase_pct = 15.2;}), /matched P09 overhead pairs/],
  ["performance a pair without memory", json("m17-performance-raw.json", v => {v.pairs[0].memory_increase_pct = null;}), /matched P09 overhead pairs/],
  ["performance no cpu count", json("m17-performance-raw.json", v => {v.system.cpus = 0;}), /matched P09 overhead pairs/],
  ["performance no stddev", json("m17-performance-raw.json", v => {v.p95_increase_stddev_pct = null;}), /matched P09 overhead pairs/],
  ["latency 99 requests", json("m17-live-latency-alert-results.json", v => {v.request_count = 99;}), /latency witnesses/],
  ["latency 14 slow requests", json("m17-live-latency-alert-results.json", v => {v.slow_requests = 14;}), /latency witnesses/],
  ["latency a check failed", json("m17-live-latency-alert-results.json", v => {v.checks[1].status = "FAIL";}), /latency witnesses/],
  ["latency two checks", json("m17-live-latency-alert-results.json", v => {v.checks.pop();}), /latency witnesses/],
  ["alert matrix five checks", json("m17-alert-matrix-results.json", v => {v.checks.pop();}), /alert or tenfold pressure/],
  ["alert matrix a check failed", json("m17-alert-matrix-results.json", v => {v.checks[4].status = "FAIL";}), /alert or tenfold pressure/],
  ["pressure three cases", json("m17-pressure-matrix-results.json", v => {v.cases.pop();}), /alert or tenfold pressure/],
  ["pressure a case failed", json("m17-pressure-matrix-results.json", v => {v.cases[0].status = "FAIL";}), /alert or tenfold pressure/],
  ["database not restored", json("m17-database-fault-results.json", v => {v.database_restored = false;}), /PostgreSQL outage/],
  ["database steps out of order", json("m17-database-fault-results.json", v => {[v.checks[1], v.checks[2]] = [v.checks[2], v.checks[1]];}), /PostgreSQL outage/],
  ["database no alert recorded", json("m17-database-fault-results.json", v => {v.alerts = [];}), /PostgreSQL outage/],
  ["database five steps", json("m17-database-fault-results.json", v => {v.checks.pop();}), /PostgreSQL outage/],
  ["collector not restored", json("m17-collector-fault-results.json", v => {v.collector_restored = false;}), /Collector fault/],
  ["collector step renamed", json("m17-collector-fault-results.json", v => {v.checks[2].name = "x";}), /Collector fault/],
  ["collector step failed", json("m17-collector-fault-results.json", v => {v.checks[3].result = "FAIL";}), /Collector fault/],
  ["first failure a step removed", json("m17-first-failure-results.json", v => {v.checks.pop();}), /First-failure baseline/],
  ["first failure steps swapped", json("m17-first-failure-results.json", v => {[v.checks[4], v.checks[5]] = [v.checks[5], v.checks[4]];}), /First-failure baseline/],
  ["first failure database not restored", json("m17-first-failure-results.json", v => {v.database_restored = false;}), /First-failure baseline/],
  ["first failure a family count differs", json("m17-first-failure-results.json", v => {v.checks[5].data.exported_5xx.organization = 24;}), /First-failure baseline/],
  ["first failure no increase from the baseline", json("m17-first-failure-results.json", v => {v.checks[5].data.prometheus_increase_5m.auth = 0;}), /First-failure baseline/],
  ["first failure baseline not zero", json("m17-first-failure-results.json", v => {v.checks[2].data.families.tracking = 2;}), /First-failure baseline/],
  ["first failure eligible floor not met", json("m17-first-failure-results.json", v => {v.checks[6].data.eligible_requests_5m = 99;}), /First-failure baseline/],
  ["first failure alert not firing", json("m17-first-failure-results.json", v => {v.checks[6].data.alert = "inactive";}), /First-failure baseline/],
  ["worker journey 49 receipts", json("m17-worker-journey-results.json", v => {v.receipts_accepted = 49;}), /Worker receipt\/replay/],
  ["worker journey 49 reconciled", json("m17-worker-journey-results.json", v => {v.reconciled = 49;}), /Worker receipt\/replay/],
  ["worker journey no replay", json("m17-worker-journey-results.json", v => {v.duplicate_replay = false;}), /Worker receipt\/replay/],
  ["worker journey one trace pair", json("m17-worker-journey-results.json", v => {v.matched_safe_trace_pairs = 1;}), /Worker receipt\/replay/],
  ["worker journey owner role", json("m17-worker-journey-results.json", v => {v.worker_role = "owner";}), /Worker receipt\/replay/],
  ["worker results a check failed", json("m17-worker-results.json", v => {v.checks[3].status = "FAIL";}), /Worker diagnostic fault campaign/],
  ["worker results three checks", json("m17-worker-results.json", v => {v.checks.pop();}), /Worker diagnostic fault campaign/],
  ["a stage failed in a COMPLETE run", json("run-status.json", v => {v.stage_results[0].status = "FAIL"; v.stage_results[0].exit_code = 1;}), /failed stage/],
  ["full campaign flag missing", json("run-status.json", v => {v.full_campaign_requested = false;}), /Mandatory full G17/],
  ["worker fixture omitted", json("expected-artifacts.json", v => {v.worker_fixture = false;}), /Mandatory full G17|Expected artifact contracts differ/],
  ["fault probe omitted", json("expected-artifacts.json", v => {v.fault_probe = false;}), /Mandatory full G17|Expected artifact contracts differ/]
];
for (const [label, mutate, pattern] of CASES) {
  test("complete campaign rejects: " + label, t => {
    const {manifest} = withCampaign(t, mutate);
    assert.throws(() => verifyManifest(manifest, source), pattern);
  });
}

test("a tampered byte after indexing is detected on the complete campaign", t => {
  const {dir, manifest} = withCampaign(t);
  fs.appendFileSync(path.join(dir, "m17-restart-results.json"), " ");
  assert.throws(() => verifyManifest(manifest, source), /Evidence tampered or unlisted: m17-restart-results.json/);
});
test("a missing required artifact is rejected on the complete campaign", t => {
  const {dir} = withCampaign(t);
  fs.unlinkSync(path.join(dir, "m17-worker-results.json"));
  const manifest = writeManifest(dir, v => ({...v, missing_expected_artifacts: []}));
  assert.throws(() => verifyManifest(manifest, source), /Manifest expected-artifact discrepancy|missing evidence/);
});
test("a falsely reviewed inventory, count or disposition is rejected", t => {
  for (const [adjust, pattern] of [
    [v => ({...v, reviewed_assertion_count: 1}), /Unreconciled or falsely reviewed/],
    [v => ({...v, assertion_count: 74}), /Unreconciled or falsely reviewed/],
    [v => ({...v, scenario_count: 24}), /Unreconciled or falsely reviewed/],
    [v => ({...v, assertion_inventory_sha256: "0".repeat(64)}), /Unreconciled or falsely reviewed/],
    [v => ({...v, assertion_disposition: "REVIEWED"}), /may not claim reviewed assertions/],
    [v => ({...v, gate_acceptance: "ACCEPTED"}), /Invalid source-locked G17 candidate manifest header/]
  ]) {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "g17-campaign-review-"));
    t.after(() => fs.rmSync(dir, {recursive: true, force: true}));
    writeCampaignFiles(dir);
    const manifest = writeManifest(dir, adjust);
    assert.throws(() => verifyManifest(manifest, source), pattern);
  }
});

// Header and ledger guards: every field is broken alone, so none can hide behind another.
const OTHER = "b".repeat(40);
const HEADER = [
  ["run-status schema version", json("run-status.json", v => {v.schema_version = 3;}), /stage ledger/],
  ["expected-artifacts schema version", json("expected-artifacts.json", v => {v.schema_version = 2;}), /stage ledger/],
  ["run-status gate", json("run-status.json", v => {v.gate = "G16";}), /stage ledger/],
  ["expected-artifacts gate", json("expected-artifacts.json", v => {v.gate = "G16";}), /stage ledger/],
  ["run-status source sha", json("run-status.json", v => {v.source_sha = OTHER;}), /stage ledger/],
  ["expected-artifacts source sha", json("expected-artifacts.json", v => {v.source_sha = OTHER;}), /stage ledger/],
  ["requested source sha", json("run-status.json", v => {v.requested_source_sha = OTHER;}), /stage ledger/],
  ["source not verified", json("run-status.json", v => {v.source_verified = false;}), /stage ledger/],
  ["gate accepted in the run status", json("run-status.json", v => {v.gate_acceptance = "ACCEPTED";}), /stage ledger/],
  ["assertions already reviewed in the run status", json("run-status.json", v => {v.g17_assertion_review = "REVIEWED";}), /stage ledger/],
  ["run state unknown", json("run-status.json", v => {v.run_state = "DONE";}), /stage ledger/],
  ["run state differs from the manifest", json("run-status.json", v => {v.run_state = "FAILED";}), /stage ledger/],
  ["required artifacts not an array", json("expected-artifacts.json", v => {v.required_artifacts = "x";}), /stage ledger/],
  ["full campaign flag not boolean", json("expected-artifacts.json", v => {v.require_full_g17 = "yes";}), /stage ledger/],
  ["stage with exit code 1 but PASS", json("run-status.json", v => {v.stage_results[0].exit_code = 1;}), /failed stage/],
  ["stage FAIL with exit code 0", json("run-status.json", v => {v.stage_results[0].status = "FAIL";}), /failed stage/],
  ["no stages at all", json("run-status.json", v => {v.stage_results = [];}), /stage ledger|failed stage/],
  ["preflight 25 scenarios claimed as 24", json("g17-coverage-preflight.json", v => {v.scenarios = 24;}), /preflight/],
  ["preflight with an error", json("g17-coverage-preflight.json", v => {v.errors = ["x"];}), /preflight/],
  ["preflight 74 assertions", json("g17-coverage-preflight.json", v => {v.assertions = 74;}), /preflight/],
  ["preflight not passed", json("g17-coverage-preflight.json", v => {v.passed = false;}), /preflight/],
  ["ledger source sha", json("g17-technical-assertion-ledger.json", v => {v.source_sha = OTHER;}), /raw atomic technical witnesses/],
  ["ledger project", json("g17-technical-assertion-ledger.json", v => {v.project = "ho-verify-m17-other";}), /raw atomic technical witnesses/],
  ["ledger not passed", json("g17-technical-assertion-ledger.json", v => {v.pass = false;}), /raw atomic technical witnesses/],
  ["ledger 74 records", json("g17-technical-assertion-ledger.json", v => {v.records.pop();}), /raw atomic technical witnesses/],
  ["ledger duplicate id", json("g17-technical-assertion-ledger.json", v => {v.records[1].id = v.records[0].id;}), /raw atomic technical witnesses/],
  ["ledger independent count", json("g17-technical-assertion-ledger.json", v => {v.independent_pending = 2;}), /raw atomic technical witnesses/]
];
for (const [label, mutate, pattern] of HEADER) {
  test("complete campaign header guard: " + label, t => {
    const {manifest} = withCampaign(t, mutate);
    assert.throws(() => verifyManifest(manifest, source), pattern);
  });
}
test("a manifest header with a wrong schema version, gate or source is rejected one field at a time", t => {
  for (const adjust of [v => ({...v, schema_version: 3}), v => ({...v, gate: "G16"}),
      v => ({...v, source_sha: "xyz"})]) {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "g17-campaign-header-"));
    t.after(() => fs.rmSync(dir, {recursive: true, force: true}));
    writeCampaignFiles(dir);
    assert.throws(() => verifyManifest(writeManifest(dir, adjust), source),
      /Invalid source-locked G17 candidate manifest header/);
  }
});

// Second-pass material survivors (S25-M series). The guards below are written as one condition per
// report, so a weakened operator would let a report that is wrong in exactly one field through.
// Every case here changes exactly one field of an otherwise valid complete campaign.
const REPORT_FILES = ["m17-cardinality-memory-results.json", "m17-restart-results.json", "m17-retention-age-results.json",
  "m17-retention-matrix-results.json", "m17-performance-raw.json", "m17-live-latency-alert-results.json",
  "m17-database-fault-results.json", "m17-export-privacy-results.json"];
for (const file of REPORT_FILES) {
  for (const [field, wrong] of [["source_sha", "b".repeat(40)], ["project", "ho-verify-m17-other"], ["result", "FAIL"]]) {
    test("complete campaign rejects " + file + " with only " + field + " wrong", t => {
      const {manifest} = withCampaign(t, json(file, v => {v[field] = wrong;}));
      assert.throws(() => verifyManifest(manifest, source), /./);
    });
  }
}
const SINGLE_FACT = [
  ["performance run kind", json("m17-performance-raw.json", v => {v.run_kind = "SINGLE_PAIR";}), /matched P09 overhead pairs/],
  ["privacy report with no exported metric series", json("m17-export-privacy-results.json", v => {v.exported_metric_series_inspected = 0;}), /./],
  ["privacy report with a single snapshot", json("m17-export-privacy-results.json", v => {v.snapshots = v.snapshots.slice(0, 1);}), /./],
  ["ledger claims 74 total assertions", json("g17-technical-assertion-ledger.json", v => {v.total_assertions = 74;}), /raw atomic technical witnesses/],
  ["ledger lists 71 technical records while claiming 72", json("g17-technical-assertion-ledger.json", v => {v.records[0].status = "PENDING_INDEPENDENT_REVIEW";}), /raw atomic technical witnesses/],
  ["ledger source differs", json("g17-technical-assertion-ledger.json", v => {v.source_sha = "b".repeat(40);}), /raw atomic technical witnesses/],
  ["ledger pass flag false", json("g17-technical-assertion-ledger.json", v => {v.pass = false;}), /raw atomic technical witnesses/]
];
for (const [label, mutate, pattern] of SINGLE_FACT) {
  test("complete campaign rejects: " + label, t => {
    const {manifest} = withCampaign(t, mutate);
    assert.throws(() => verifyManifest(manifest, source), pattern);
  });
}

// Third-pass survivors from the exhaustive manifest campaign (S25-M series). Each case changes one fact.
import {spawnSync} from "node:child_process";
import {pathToFileURL, fileURLToPath} from "node:url";
import crypto from "node:crypto";
const scriptsDir = path.dirname(fileURLToPath(import.meta.url));
const repoDir = path.resolve(scriptsDir, "..");

const REPORTS_WITH_IDENTITY = ["m17-tempo-retention-results.json", "g17-regression-matrix-results.json", "m17-alert-matrix-results.json",
  "m17-pressure-matrix-results.json", "m17-collector-fault-results.json", "m17-worker-journey-results.json",
  "m17-first-failure-results.json"];
for (const file of REPORTS_WITH_IDENTITY) {
  for (const [field, wrong] of [["source_sha", "b".repeat(40)], ["project", "ho-verify-m17-other"], ["result", "FAIL"]]) {
    test("complete campaign rejects " + file + " with only " + field + " wrong", t => {
      const {manifest} = withCampaign(t, json(file, v => {v[field] = wrong;}));
      assert.throws(() => verifyManifest(manifest, source), /./);
    });
  }
}
for (const [field, wrong] of [["source_sha", "b".repeat(40)], ["result", "FAIL"]]) {
  test("complete campaign rejects m17-worker-results.json with only " + field + " wrong", t => {
    const {manifest} = withCampaign(t, json("m17-worker-results.json", v => {v[field] = wrong;}));
    assert.throws(() => verifyManifest(manifest, source), /Worker diagnostic fault campaign incomplete/);
  });
}

test("a manifest entry that differs from the file in only its path, only its hash or only its size is refused", t => {
  for (const [label, change] of [
      ["path", entry => {entry.path = "zz-" + entry.path;}],
      ["sha256", entry => {entry.sha256 = "0".repeat(64);}],
      ["bytes", entry => {entry.bytes += 1;}]]) {
    const {manifest} = withCampaign(t, undefined);
    const doc = JSON.parse(fs.readFileSync(manifest, "utf8"));
    change(doc.files[3]);
    fs.writeFileSync(manifest, JSON.stringify(doc));
    assert.throws(() => verifyManifest(manifest, source), /Evidence tampered or unlisted|coverage mismatch/, label);
  }
});
test("a same-size content change is refused by hash alone", t => {
  const {dir, manifest} = withCampaign(t);
  const file = path.join(dir, "m17-restart-results.json");
  const before = fs.readFileSync(file, "utf8");
  const after = before.replace("PASS", "PAS5");
  assert.equal(after.length, before.length);
  assert.notEqual(after, before);
  fs.writeFileSync(file, after);
  assert.throws(() => verifyManifest(manifest, source), /Evidence tampered or unlisted: m17-restart-results.json/);
});
test("each required run ledger file must exist even when the index agrees with the directory", t => {
  for (const name of ["source-sha.txt", "expected-artifacts.json", "run-status.json"]) {
    const {dir} = withCampaign(t);
    fs.unlinkSync(path.join(dir, name));
    const manifest = writeManifest(dir);
    assert.throws(() => verifyManifest(manifest, source), /Missing required G17 run ledger/, name);
  }
});

test("boundary values the campaign accepts: 3000 telemetry samples, exactly 10 percent p95 and exactly 15 percent memory", t => {
  const {manifest} = withCampaign(t, json("m17-performance-raw.json", v => {
    for (const kind of ["baseline", "authorized"]) v.pooled[kind].telemetry_on_samples = 3000;
  }));
  // Only the manifest-level sample floor is under test here, so a recalculation conflict would surface as a throw.
  try {verifyManifest(manifest, source);} catch (error) {
    assert.doesNotMatch(String(error.message), /Three complete matched P09 overhead pairs/, "3000 samples is the inclusive floor");
  }
});

// The assertion inventory is read from the repository next to the verifier, so these cases run a private copy of the
// verifier beside an edited inventory. The manifest records the edited inventory's hash, so only the named fact is wrong.
async function withInventory(t, edit) {
  const tree = fs.mkdtempSync(path.join(os.tmpdir(), "g17-inventory-tree-"));
  t.after(() => fs.rmSync(tree, {recursive: true, force: true}));
  fs.mkdirSync(path.join(tree, "scripts")); fs.mkdirSync(path.join(tree, "docs"));
  for (const name of fs.readdirSync(scriptsDir).filter(n => n.endsWith(".mjs") && !n.endsWith(".test.mjs")))
    fs.copyFileSync(path.join(scriptsDir, name), path.join(tree, "scripts", name));
  for (const name of fs.readdirSync(path.join(repoDir, "docs")).filter(n => n.endsWith(".json")))
    fs.copyFileSync(path.join(repoDir, "docs", name), path.join(tree, "docs", name));
  const inventoryPath = path.join(tree, "docs", "M17_G17_ASSERTION_INVENTORY_20261112.json");
  const inventory = JSON.parse(fs.readFileSync(inventoryPath, "utf8"));
  edit(inventory);
  const bytes = Buffer.from(JSON.stringify(inventory));
  fs.writeFileSync(inventoryPath, bytes);
  const copy = await import(pathToFileURL(path.join(tree, "scripts", "verify-g17-manifest.mjs")).href);
  const {manifest} = withCampaign(t, undefined);
  const doc = JSON.parse(fs.readFileSync(manifest, "utf8"));
  doc.assertion_inventory_sha256 = crypto.createHash("sha256").update(bytes).digest("hex");
  fs.writeFileSync(manifest, JSON.stringify(doc));
  return () => copy.verifyManifest(manifest, source);
}
const INVENTORY_ERROR = /Unreconciled or falsely reviewed G17 atomic assertion inventory/;
test("the private verifier copy accepts the unedited inventory", async t => {
  const verify = await withInventory(t, () => {});
  assert.equal(verify().complete, true);
});
const INVENTORY_FACTS = [
  ["24 scenarios holding all 75 assertions", inv => {const last = inv.scenarios.pop(); inv.scenarios[23].assertions.push(...last.assertions);}],
  ["25 scenarios holding only 74 assertions", inv => {inv.scenarios[0].assertions.pop();}],
  ["inventory marked accepted", inv => {inv.gate_acceptance = "ACCEPTED";}],
  ["one assertion already executed", inv => {inv.scenarios[0].assertions[0].status = "TECHNICAL_PASS";}],
  ["one assertion already holding a witness", inv => {inv.scenarios[0].assertions[0].witness = {path: "x", case: "y"};}]
];
for (const [label, edit] of INVENTORY_FACTS) {
  test("an inventory with " + label + " is refused on that fact alone", async t => {
    const verify = await withInventory(t, edit);
    assert.throws(verify, INVENTORY_ERROR);
  });
}

test("the command line verifies a complete campaign, and refuses a missing manifest path or SHA with its usage text", t => {
  const {manifest} = withCampaign(t);
  const script = path.join(scriptsDir, "verify-g17-manifest.mjs");
  const ok = spawnSync(process.execPath, [script, manifest, source], {encoding: "utf8"});
  assert.equal(ok.status, 0, ok.stderr);
  assert.match(ok.stdout, /Verified G17 evidence index/);
  for (const args of [[], [manifest], ["", source], [manifest, "not-a-sha"]]) {
    const run = spawnSync(process.execPath, [script, ...args], {encoding: "utf8"});
    assert.notEqual(run.status, 0, JSON.stringify(args));
    assert.match(run.stderr, /Usage: node scripts\/verify-g17-manifest.mjs/, JSON.stringify(args));
  }
});

test("the recorded P09 budgets are inclusive: exactly 10 percent pooled p95 and exactly 15 percent mean memory are accepted, and just above is refused", t => {
  const {manifest} = withCampaign(t, json("m17-performance-raw.json", v => {
    for (const kind of ["baseline", "authorized"]) v.pooled[kind].p95_increase_pct = 10;
    v.gate.mean_memory_increase_pct = 15;
  }));
  assert.equal(verifyManifest(manifest, source).complete, true);
  for (const [label, edit] of [
      ["p95 10.0001", v => {v.pooled.authorized.p95_increase_pct = 10.0001;}],
      ["memory 15.0001", v => {v.gate.mean_memory_increase_pct = 15.0001;}]]) {
    const refused = withCampaign(t, json("m17-performance-raw.json", edit));
    assert.throws(() => verifyManifest(refused.manifest, source), /matched P09 overhead pairs/, label);
  }
});
