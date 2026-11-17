import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
import {FAMILIES, FAMILIES_WITH_REAL_FAULT, FIRST_FAILURE_STEPS, classifyFamily, familyFaultPlan,
  evaluateFirstFailure, firstFailureReportProblems} from "./g17-first-failure-evidence.mjs";

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const read = relative => fs.readFileSync(path.join(repo, relative), "utf8");

const perfect = () => ({
  exported: {auth: 25, organization: 25, tracking: 25, health: 26, business: 25, other: 0},
  client: {auth: {requests: 45, server_errors: 25}, organization: {requests: 55, server_errors: 25}, tracking: {requests: 45, server_errors: 25},
    health: {requests: 57, server_errors: 26}, business: {requests: 55, server_errors: 25}, other: {requests: 1, server_errors: 0}},
  increases: {auth: 25.4, organization: 25.1, tracking: 25.2, health: 26.3, business: 25.5, other: 0},
  series: FAMILIES.map(operation => ({__name__: "harborops_request_failures_total", operation, status_class: "5xx", job: "m17_collector", instance: "otelcol:9464",
    exported_job: "harborops-api", exported_instance: "0a1b2c3d-0000-4000-8000-0123456789ab"}))
});
const problemsOf = edit => { const input = perfect(); edit(input); return evaluateFirstFailure(input).problems; };

test("the family set equals the set the API seeds, so a new family cannot be classified without a baseline", () => {
  const source = read("src/HarborOps.Api/Observability/M17Telemetry.cs");
  const declared = [...source.matchAll(/private const string (\w+)Operation = "([a-z]+)";/g)].map(m => m[2]);
  assert.deepEqual([...declared].sort(), [...FAMILIES].sort());
  const seeded = source.match(/OperationFamilies =\s*\[([^\]]+)\]/)[1].match(/\w+Operation/g).map(n => n);
  assert.equal(seeded.length, FAMILIES.length, "every constant is in the seeded list");
  for (const constant of source.matchAll(/private const string (\w+Operation) = "/g)) assert.ok(seeded.includes(constant[1]), constant[1]);
  const classifier = source.slice(source.indexOf("private static string Operation("));
  for (const constant of seeded) assert.ok(classifier.includes("return " + constant), `${constant} is returned by the classifier`);
});

test("the seeding loop and the zero value stay in the heartbeat path", () => {
  const source = read("src/HarborOps.Api/Observability/M17Telemetry.cs");
  const seed = source.slice(source.indexOf("private static void SeedCounterSeries()"));
  assert.match(seed, /foreach \(var family in OperationFamilies\)\s*FailedRequests\.Add\(0,/);
  assert.match(seed, /new KeyValuePair<string, object\?>\("status_class", "5xx"\)/);
  assert.match(source, /public static void Heartbeat\(\)\s*\{\s*SeedCounterSeries\(\);/);
});

test("classification matches the API prefix rules for the concrete URLs the campaign uses", () => {
  const cases = {"/api/v1/auth/csrf": "auth", "/api/v1/auth/session": "auth", "/api/v1/organizations/abc/ports": "organization",
    "/integrations/v1/tracking-events": "tracking", "/health/ready": "health", "/api/v1/system/baseline": "business",
    "/g17-unmatched": "other", "/": "other", "/api/v1/auth/session?x=1": "auth"};
  for (const [route, family] of Object.entries(cases)) assert.equal(classifyFamily(route), family, route);
});

test("every family with a real fault path has a plan, and the other family has none", () => {
  const plan = familyFaultPlan("11111111-1111-1111-1111-111111111111");
  assert.deepEqual(Object.keys(plan).sort(), [...FAMILIES].sort());
  for (const family of FAMILIES_WITH_REAL_FAULT) {
    assert.ok(plan[family].count >= 20, family);
    assert.equal(classifyFamily(plan[family].route), family, `the ${family} plan really lands in ${family}`);
  }
  assert.equal(plan.other, null);
  assert.equal(plan.tracking.randomBearer, true);
  assert.equal(plan.organization.cookie && plan.auth.cookie, true);
});

test("a complete observation set has no problems", () => {
  const verdict = evaluateFirstFailure(perfect());
  assert.deepEqual(verdict.problems, []);
  assert.deepEqual(verdict.families_with_real_fault.sort(), [...FAMILIES_WITH_REAL_FAULT].sort());
});

test("each defect is rejected with its own reason", () => {
  const cases = [
    ["a family exported fewer failures than were produced", i => { i.exported.organization = 24; }, "organization: exported 24 differs from the 25 real 5xx responses"],
    ["a family exported more failures than were produced", i => { i.exported.auth = 26; }, "auth: exported 26 differs from the 25 real 5xx responses"],
    ["a family with no exported counter", i => { delete i.exported.tracking; }, "no exported failure counter for tracking"],
    ["a family whose burst never produced a 5xx", i => { i.client.health.server_errors = 0; i.exported.health = 0; }, "health: no real 5xx response was produced"],
    ["Prometheus saw no increase (the lost first sample)", i => { i.increases.business = 0; }, "business: Prometheus saw no increase from the zero baseline"],
    ["an increase that is not a number", i => { i.increases.business = null; }, "business: Prometheus saw no increase from the zero baseline"],
    ["the unmatched family left its zero", i => { i.client.other.server_errors = 1; i.exported.other = 1; }, "other: the unmatched-route family must stay at its seeded zero"],
    ["a missing series", i => { i.series.pop(); }, "expected exactly 6 failure series, saw 5"],
    ["an extra series", i => { i.series.push({...i.series[0], operation: "auth", status_class: "5xx"}); }, "expected exactly 6 failure series, saw 7"],
    ["a duplicated family instead of a distinct one", i => { i.series[5] = {...i.series[0]}; }, "duplicate series for auth"],
    ["a label that identifies an organization", i => { i.series[0].organization_id = "x"; }, "forbidden label name organization_id"],
    ["a route label", i => { i.series[0].route = "/api/v1/x"; }, "forbidden label name route"],
    ["an unapproved extra label", i => { i.series[0].color = "red"; }, "unapproved label name color"],
    ["an operation outside the closed set", i => { i.series[0].operation = "organization/abc"; }, "unapproved operation value organization/abc"],
    ["a non-5xx status class", i => { i.series[0].status_class = "4xx"; }, "unexpected status_class 4xx"],
    ["a renamed job label that carries something other than the service name", i => { i.series[0].exported_job = "tenant-a"; }, "unapproved exported_job value tenant-a"],
    ["a renamed instance label that is not a process identifier", i => { i.series[0].exported_instance = "user@example.test"; }, "exported_instance is not a process identifier: user@example.test"]
  ];
  for (const [label, edit, expected] of cases) assert.ok(problemsOf(edit).includes(expected), `${label}: ${JSON.stringify(problemsOf(edit))}`);
});

function goodReport() {
  const exact = {exported_5xx: {auth: 25, organization: 25, tracking: 25, health: 26, business: 25, other: 0},
    client_5xx: {auth: 25, organization: 25, tracking: 25, health: 26, business: 25, other: 0},
    prometheus_increase_5m: {auth: 25, organization: 25, tracking: 25, health: 26, business: 25, other: 0}, series_count: 6};
  const data = {
    "fresh-api-collector-and-prometheus-processes": {api_container_replaced: true},
    "zero-baseline-of-every-family-is-exported-before-any-failure": {families: Object.fromEntries(FAMILIES.map(f => [f, 0])), series_count: 6, unknown_before_traffic: true},
    "exported-counters-equal-the-real-503-counts-per-family": exact,
    "eligible-floor-fraction-and-reference-alert-from-the-first-burst": {eligible_requests_5m: 230, failure_fraction_5m: 0.35, dashboard_rate: 0.3, alert: "firing"}
  };
  return {source_sha: "a".repeat(40), project: "ho-verify-m17-x", result: "PASS", database_restored: true,
    checks: FIRST_FAILURE_STEPS.map(name => ({name, status: "PASS", data: data[name] ?? {}}))};
}
const identity = {source: "a".repeat(40), project: "ho-verify-m17-x"};

test("a complete report passes and each missing fact is reported separately", () => {
  assert.deepEqual(firstFailureReportProblems(goodReport(), identity), []);
  const cases = [
    ["not PASS", r => { r.result = "FAIL"; }, "result is not PASS"],
    ["foreign source", r => { r.source_sha = "b".repeat(40); }, "source differs"],
    ["foreign project", r => { r.project = "ho-verify-m17-y"; }, "project differs"],
    ["database not restored", r => { r.database_restored = false; }, "database was not restored"],
    ["a step removed", r => { r.checks.pop(); }, "the eight required steps are missing, reordered or not all PASS"],
    ["a step failed", r => { r.checks[3].status = "FAIL"; }, "the eight required steps are missing, reordered or not all PASS"],
    ["steps swapped", r => { [r.checks[1], r.checks[2]] = [r.checks[2], r.checks[1]]; }, "the eight required steps are missing, reordered or not all PASS"],
    ["baseline not zero in a family", r => { r.checks[2].data.families.health = 3; }, "the zero baseline of every family was not exported before the failure"],
    ["baseline missing a series", r => { r.checks[2].data.series_count = 1; }, "the zero baseline of every family was not exported before the failure"],
    ["Unknown not demonstrated", r => { r.checks[2].data.unknown_before_traffic = false; }, "the zero baseline of every family was not exported before the failure"],
    ["processes not fresh", r => { r.checks[1].data.api_container_replaced = false; }, "processes were not fresh"],
    ["a count that differs", r => { r.checks[5].data.exported_5xx.auth = 24; }, "exported counts do not equal the real 5xx counts in every family"],
    ["no increase in a family", r => { r.checks[5].data.prometheus_increase_5m.tracking = 0; }, "exported counts do not equal the real 5xx counts in every family"],
    ["the unmatched family not zero", r => { r.checks[5].data.exported_5xx.other = 1; }, "exported counts do not equal the real 5xx counts in every family"],
    ["floor not met", r => { r.checks[6].data.eligible_requests_5m = 99; }, "the eligible floor, failure fraction or alert evidence is missing"],
    ["fraction at the trigger", r => { r.checks[6].data.failure_fraction_5m = 0.01; }, "the eligible floor, failure fraction or alert evidence is missing"],
    ["alert not firing", r => { r.checks[6].data.alert = "inactive"; }, "the eligible floor, failure fraction or alert evidence is missing"]
  ];
  for (const [label, edit, expected] of cases) {
    const report = goodReport();
    edit(report);
    assert.ok(firstFailureReportProblems(report, identity).includes(expected), `${label}: ${JSON.stringify(firstFailureReportProblems(report, identity))}`);
  }
});

test("a family without a series is named even when the series count still adds up", () => {
  const problems = problemsOf(i => { i.series[5] = {...i.series[0]}; });
  assert.ok(problems.includes("no series for other"), JSON.stringify(problems));
  assert.ok(problems.includes("duplicate series for auth"));
});

test("a family whose burst produced no real failure cannot be reported complete, even if nothing was exported either", () => {
  const report = goodReport();
  report.checks[5].data.client_5xx.health = 0;
  report.checks[5].data.exported_5xx.health = 0;
  assert.ok(firstFailureReportProblems(report, identity).includes("exported counts do not equal the real 5xx counts in every family"));
});

test("the synthetic source credential has exactly the layout the API parses, and is never reused", async () => {
  const {syntheticBearer} = await import("./g17-first-failure-evidence.mjs");
  const crypto = await import("node:crypto");
  const seen = new Set();
  for (let i = 0; i < 50; i++) {
    const token = syntheticBearer(crypto.randomBytes);
    assert.equal(token.length, 83);
    assert.match(token, /^hop_v1_[0-9a-f]{32}_[A-Za-z0-9_-]{43}$/);
    assert.equal(token[39], "_");
    assert.ok(!seen.has(token));
    seen.add(token);
  }
  // The same arithmetic as the API's ParseSecret: 32 secret bytes encode to 43 URL-safe characters.
  const decoded = Buffer.from(syntheticBearer(crypto.randomBytes).slice(40).replaceAll("-", "+").replaceAll("_", "/") + "=", "base64");
  assert.equal(decoded.length, 32);
});
