import assert from "node:assert/strict";

// The live alert has a sample-volume guard. A batch of HTTP failures is
// insufficient evidence unless Prometheus observed a counter increase across
// actual exported samples and evaluated the configured failure-rate threshold.
export function validateLive5xxEvidence({
  baseline_counter,observed_counter,failed_increase_5m,
  eligible_requests_5m,failure_fraction_5m,requested_503_responses
}) {
  for(const value of [baseline_counter,observed_counter,failed_increase_5m,
    eligible_requests_5m,failure_fraction_5m])
    assert.ok(Number.isFinite(value)&&value>=0,"Invalid exported Prometheus sample");
  assert.equal(requested_503_responses,120,"Missing real failing HTTP responses");
  assert.ok(observed_counter>baseline_counter,
    "The failure counter did not increase after its exported baseline");
  assert.ok(failed_increase_5m>0,
    "Prometheus did not observe a failure counter increase over time");
  assert.ok(eligible_requests_5m>=100,
    "The live alert has not met its genuine minimum traffic volume");
  assert.ok(failure_fraction_5m>0.01&&failure_fraction_5m<=1,
    "The Prometheus failure-rate fraction did not exceed its real trigger");
  return true;
}
