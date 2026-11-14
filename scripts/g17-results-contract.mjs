const DASHBOARD_TITLES = [
  "dashboard requires authentication and shows actual monitoring panels",
  "authenticated samples match the disposable ready API and suppress held workers",
  "dashboard is unavailable to an anonymous browser context",
  "alert dashboard links stay local and never embed private diagnostic parameters",
  "critical monitoring panels use live series and no-data views are explicitly unknown",
  "zero and insufficient traffic render unknown rather than a healthy zero",
  "rendered readiness distinguishes missing data from real downtime in three browsers",
  "browser API denial retains server-issued correlation under spoofed context"
];
const CARDINALITY_TITLES = [
  "ten thousand hostile HTTP requests remain within complete exported-series budget"
];
const ENGINES = ["chromium","firefox","webkit"];

function ensure(ok,message) { if (!ok) throw Error(message); }
function same(a,b) { return a.length === b.length && a.every((x,i)=>x===b[i]); }
function caseList(suites) {
  const result=[];
  function visit(suite) {
    for (const spec of suite.specs ?? [])
      for (const test of spec.tests ?? [])
        result.push({title:spec.title,project:test.projectName,
          expectedStatus:test.expectedStatus,status:test.status,
          results:test.results ?? []});
    for (const child of suite.suites ?? []) visit(child);
  }
  for (const suite of suites ?? []) visit(suite);
  return result;
}
export function verifyPlaywrightCases(raw, mode) {
  ensure(["dashboard","cardinality"].includes(mode),"Unexpected browser suite");
  const titles = mode === "dashboard" ? DASHBOARD_TITLES : CARDINALITY_TITLES;
  const engines = mode === "dashboard" ? ENGINES : ["chromium"];
  const required=engines.flatMap(engine=>titles.map(title=>engine+"::"+title)).sort();
  const cases=caseList(raw.suites);
  const actual=cases.map(x=>x.project+"::"+x.title).sort();
  ensure(same(required,actual),
    "Missing, unexpected or duplicated browser cases: "+JSON.stringify(actual));
  ensure(raw.stats?.expected === required.length && raw.stats?.unexpected === 0 &&
    raw.stats?.skipped === 0 && raw.stats?.flaky === 0,
    "Incomplete, skipped, flaky or failing Playwright reporter totals");
  for (const c of cases) {
    ensure(c.expectedStatus === "passed" && c.status === "expected" &&
      c.results.length === 1 && c.results[0].status === "passed" &&
      c.results[0].retry === 0,
      "Browser case was skipped, expected to fail, retried or incomplete: "+c.title);
  }
  return {passed:cases.length,case_ids:actual};
}
export function verifyTrxCases(xml,requiredMethods=[]) {
  const counters=xml.match(/<Counters\b[^>]*>/i)?.[0];
  ensure(Boolean(counters),"Missing TRX counters");
  const attr=(tag,key)=>Number(tag.match(new RegExp('\\b'+key+'="(\\d+)"'))?.[1] ?? -1);
  const total=attr(counters,"total"),passed=attr(counters,"passed");
  const failed=attr(counters,"failed"),notExecuted=attr(counters,"notExecuted");
  ensure(total>0 && passed===total && failed===0 && notExecuted===0,
    "Incomplete, skipped or failing TRX counters");
  const identities=[];
  const results=[...xml.matchAll(/<UnitTestResult\b[^>]*>/g)].map(x=>{
    const tag=x[0],name=tag.match(/\btestName="([^"]+)"/)?.[1];
    const outcome=tag.match(/\boutcome="([^"]+)"/)?.[1];
    ensure(Boolean(name) && outcome==="Passed","Nonpassing or unnamed raw TRX case");
    // xUnit truncates long theory-row display names, so distinct rows can share a name.
    // Each executed row has its own testId; double reporting repeats the same testId.
    identities.push(tag.match(/\btestId="([^"]+)"/)?.[1] ?? name);
    return name;
  });
  ensure(results.length===total,"TRX totals differ from actual UnitTestResult cases");
  ensure(new Set(identities).size===identities.length,
    "Duplicated exact TRX test results");
  for (const method of requiredMethods) {
    const matching=results.filter(name=>name===method || name.endsWith("."+method) ||
      name.includes("."+method+"("));
    ensure(matching.length===1,
      "Required M17 method missing or duplicated: "+method);
  }
  return {passed:total,methods:results};
}
export const G17_REQUIRED_METHODS = {
  contract: [
    "EnabledTelemetryDoesNotChangeExistingHealthAndDatabaseErrorContracts",
    "SpoofedCorrelationIsReplacedAndWarningKeepsServerGeneratedScope",
    "EnabledTelemetryRecordsExactlyOneRequestDurationAndFailureObservation"
  ],
  integration: [
    "EveryLocalInstrumentHasTheApprovedNameTypeAndUnit",
    "RequestCountIsDerivedOnlyFromDurationHistogramAndNotDuplicated",
    "RealNonzeroRequestAndDatabaseDurationsReachMeterListeners",
    "WorkerDispositionsAreMeasuredWithExactFiniteOutcomes",
    "KeyedReceiptAndWorkerSamplersAgreeWithoutASharedTraceParent",
    "WorkerTraceSanitizerRejectsUntrustedDetailsAndNonTagFields"
  ]
};
