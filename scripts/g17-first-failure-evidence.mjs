// Pure evidence rules for the first-failure baseline campaign (scripts/check-g17-first-failure.mjs). Kept separate
// so every rule has a negative control that runs without a stack.

// The closed set of "operation" label values of harborops_request_failures_total. It must equal the set the API
// seeds with a zero baseline (src/HarborOps.Api/Observability/M17Telemetry.cs, OperationFamilies).
export const FAMILIES = ["auth", "organization", "tracking", "health", "business", "other"];
// Families that have a real HTTP fault path through a stopped PostgreSQL. "other" is every unmatched route, which
// can only be a 404, so it is verified to stay at its seeded zero instead.
export const FAMILIES_WITH_REAL_FAULT = ["auth", "organization", "tracking", "health", "business"];

const FORBIDDEN_LABEL = /tenant|organi[sz]ation_?id|org_?id|user|session|route|path|url|token|email|reference|shipment|correlation|trace|account|host|address/i;
const ALLOWED_EXTRA_LABELS = new Set(["__name__", "operation", "status_class", "job", "instance",
  "otel_scope_name", "otel_scope_version", "otel_scope_schema_url"]);

// Mirrors M17Telemetry.Operation for the concrete URLs this campaign uses (the API classifies the route pattern,
// which has the same prefix for these requests).
export function classifyFamily(route) {
  const pathOnly = String(route).split("?")[0];
  if (pathOnly.startsWith("/api/v1/auth")) return "auth";
  if (pathOnly.startsWith("/api/v1/organizations")) return "organization";
  if (pathOnly.startsWith("/integrations/")) return "tracking";
  if (pathOnly.startsWith("/health/")) return "health";
  if (pathOnly.startsWith("/api/v1/")) return "business";
  return "other";
}

export function familyFaultPlan(organizationId) {
  return {
    auth: {route: "/api/v1/auth/session", routeLabel: "GET /api/v1/auth/session (session cookie)", method: "GET", cookie: true, count: 25},
    organization: {route: `/api/v1/organizations/${organizationId}/ports`, routeLabel: "GET /api/v1/organizations/{id}/ports (session cookie)",
      method: "GET", cookie: true, count: 25},
    tracking: {route: "/integrations/v1/tracking-events", routeLabel: "POST /integrations/v1/tracking-events (source bearer)", method: "POST",
      headers: {"Content-Type": "application/json", "X-HarborOps-Tracking-Profile": "tnt-2.2-harborops-v1"}, body: "{}", randomBearer: true, count: 25},
    health: {route: "/health/ready", routeLabel: "GET /health/ready", method: "GET", count: 25},
    business: {route: "/api/v1/system/baseline", routeLabel: "GET /api/v1/system/baseline", method: "GET", count: 25},
    other: null
  };
}

// exported: {family: counter value}; client: {family: {requests, server_errors}}; increases: {family: Prometheus
// increase over the window}; series: label sets of every exported failure series.
export function evaluateFirstFailure({exported, client, increases, series}) {
  const problems = [];
  const labelNames = new Set();
  for (const family of FAMILIES) {
    if (!Number.isFinite(exported?.[family])) problems.push(`no exported failure counter for ${family}`);
    else if (exported[family] !== client?.[family]?.server_errors)
      problems.push(`${family}: exported ${exported[family]} differs from the ${client?.[family]?.server_errors} real 5xx responses`);
  }
  const withFault = FAMILIES.filter(f => (client?.[f]?.server_errors ?? 0) > 0);
  for (const family of FAMILIES_WITH_REAL_FAULT) {
    if (!withFault.includes(family)) problems.push(`${family}: no real 5xx response was produced`);
    const increase = increases?.[family];
    if (!(Number.isFinite(increase) && increase > 0)) problems.push(`${family}: Prometheus saw no increase from the zero baseline`);
  }
  if ((client?.other?.server_errors ?? 0) !== 0 || exported?.other !== 0) problems.push("other: the unmatched-route family must stay at its seeded zero");
  if (!Array.isArray(series) || series.length !== FAMILIES.length) problems.push(`expected exactly ${FAMILIES.length} failure series, saw ${series?.length}`);
  const seenOperations = new Set();
  for (const labels of series ?? []) {
    for (const name of Object.keys(labels)) {
      labelNames.add(name);
      if (FORBIDDEN_LABEL.test(name)) problems.push(`forbidden label name ${name}`);
      else if (!ALLOWED_EXTRA_LABELS.has(name)) problems.push(`unapproved label name ${name}`);
    }
    if (!FAMILIES.includes(labels.operation)) problems.push(`unapproved operation value ${labels.operation}`);
    if (labels.status_class !== "5xx") problems.push(`unexpected status_class ${labels.status_class}`);
    if (seenOperations.has(labels.operation)) problems.push(`duplicate series for ${labels.operation}`);
    seenOperations.add(labels.operation);
  }
  for (const family of FAMILIES) if (series && !seenOperations.has(family)) problems.push(`no series for ${family}`);
  return {problems, label_names: [...labelNames].sort(), families_with_real_fault: withFault};
}

// Strict shape of the finished report, used by the ledger and the manifest verifier.
export const FIRST_FAILURE_STEPS = ["provision-session-while-database-is-healthy", "fresh-api-collector-and-prometheus-processes",
  "zero-baseline-of-every-family-is-exported-before-any-failure", "healthy-traffic-in-every-family-meets-the-eligible-floor",
  "real-503-burst-in-each-family-through-a-stopped-database", "exported-counters-equal-the-real-503-counts-per-family",
  "eligible-floor-fraction-and-reference-alert-from-the-first-burst", "recovery-restores-readiness-and-the-counters-stop-rising"];

// A complete, internally consistent report for the synthetic campaign fixture and the negative tests.
export function syntheticFirstFailureReport(source, project) {
  const counts = {auth: 25, organization: 25, tracking: 25, health: 26, business: 25, other: 0};
  const data = {
    "fresh-api-collector-and-prometheus-processes": {api_container_replaced: true},
    "zero-baseline-of-every-family-is-exported-before-any-failure": {families: Object.fromEntries(FAMILIES.map(f => [f, 0])),
      series_count: FAMILIES.length, unknown_before_traffic: true},
    "exported-counters-equal-the-real-503-counts-per-family": {exported_5xx: {...counts}, client_5xx: {...counts},
      prometheus_increase_5m: {...counts}, series_count: FAMILIES.length},
    "eligible-floor-fraction-and-reference-alert-from-the-first-burst": {eligible_requests_5m: 230, failure_fraction_5m: 0.35,
      dashboard_rate: 0.3, alert: "firing"}
  };
  return {source_sha: source, project, result: "PASS", database_restored: true,
    checks: FIRST_FAILURE_STEPS.map(name => ({name, status: "PASS", data: data[name] ?? {}}))};
}

export function firstFailureReportProblems(report, {source, project} = {}) {
  const problems = [];
  if (report?.result !== "PASS") problems.push("result is not PASS");
  if (source !== undefined && report?.source_sha !== source) problems.push("source differs");
  if (project !== undefined && report?.project !== project) problems.push("project differs");
  if (report?.database_restored !== true) problems.push("database was not restored");
  const checks = report?.checks ?? [];
  if (checks.length !== FIRST_FAILURE_STEPS.length || checks.some((c, i) => c.name !== FIRST_FAILURE_STEPS[i] || c.status !== "PASS"))
    problems.push("the eight required steps are missing, reordered or not all PASS");
  const data = name => checks.find(c => c.name === name)?.data;
  const baseline = data("zero-baseline-of-every-family-is-exported-before-any-failure");
  if (!baseline || FAMILIES.some(f => baseline.families?.[f] !== 0) || baseline.series_count !== FAMILIES.length || baseline.unknown_before_traffic !== true)
    problems.push("the zero baseline of every family was not exported before the failure");
  if (data("fresh-api-collector-and-prometheus-processes")?.api_container_replaced !== true) problems.push("processes were not fresh");
  const exact = data("exported-counters-equal-the-real-503-counts-per-family");
  if (!exact || FAMILIES_WITH_REAL_FAULT.some(f => !(exact.client_5xx?.[f] > 0) || exact.exported_5xx?.[f] !== exact.client_5xx[f] ||
      !(exact.prometheus_increase_5m?.[f] > 0)) || exact.exported_5xx?.other !== 0 || exact.series_count !== FAMILIES.length)
    problems.push("exported counts do not equal the real 5xx counts in every family");
  const alert = data("eligible-floor-fraction-and-reference-alert-from-the-first-burst");
  if (!alert || !(alert.eligible_requests_5m >= 100) || !(alert.failure_fraction_5m > 0.01) || alert.alert !== "firing")
    problems.push("the eligible floor, failure fraction or alert evidence is missing");
  return problems;
}
