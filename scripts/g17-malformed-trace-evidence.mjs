import assert from "node:assert/strict";
export function exportedSpans(doc) {
  const resources=doc?.trace?.resourceSpans??doc?.resourceSpans??[];
  return resources.flatMap(resource=>
    (resource.scopeSpans??[]).flatMap(scope=>scope.spans??[]));
}
export function verifyMalformedBaselineTrace(doc,windowStart,windowEnd,canary) {
  assert.ok(Number.isInteger(windowStart)&&Number.isInteger(windowEnd)&&windowEnd>=windowStart);
  assert.ok(canary&&typeof canary==="string");
  const raw=JSON.stringify(doc);
  assert.ok(!raw.toLowerCase().includes(canary.toLowerCase()),
    "Malformed header or baggage canary reached exported trace");
  const spans=exportedSpans(doc);
  let baseline=false;
  for(const span of spans) {
    const started=BigInt(span.startTimeUnixNano??span.start_time_unix_nano??"0")/1000000000n;
    const inWindow=started>=BigInt(windowStart)&&started<=BigInt(windowEnd);
    const operation=(span.attributes??[]).some(a=>a.key==="harborops.operation" &&
      (a.value?.stringValue??a.value?.string_value)==="baseline_read");
    for(const attr of span.attributes??[]) {
      assert.ok(!/cookie|authorization|password|sql|query|payload|user\.id|tenant|organization_id|session|email/i.test(attr.key??""),
        "Forbidden exported trace attribute");
    }
    assert.ok(!span.traceState&&!span.events?.length&&!span.links?.length&&
      !span.status?.message,"Raw trace context or event reached exporter");
    if(operation&&inWindow)baseline=true;
  }
  return baseline;
}
