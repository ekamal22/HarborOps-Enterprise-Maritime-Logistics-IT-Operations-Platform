import assert from "node:assert/strict";
import crypto from "node:crypto";
import test from "node:test";
import {linkedJourneySampled, sampledCount, SAMPLING_VECTORS, REQUIRED_SAMPLED_JOURNEYS} from "./g17-link-sampling.mjs";

test("every boundary vector gives the documented decision", () => {
  for (const [digest, expected] of SAMPLING_VECTORS)
    assert.equal(linkedJourneySampled(digest), expected, digest.slice(0, 16));
});

test("malformed digests are never sampled", () => {
  for (const bad of [undefined, null, 5, "", "0".repeat(63), "0".repeat(65), "G".repeat(64), "0".repeat(63) + "A"])
    assert.equal(linkedJourneySampled(bad), false);
});

test("the sampling rate of keyed digests is close to one tenth", () => {
  const key = crypto.randomBytes(32);
  const digests = Array.from({length: 20000}, (_, i) => crypto.createHmac("sha256", key).update(String(i)).digest("hex"));
  const rate = sampledCount(digests) / digests.length;
  assert.ok(rate > 0.085 && rate < 0.115, `rate ${rate}`);
});

test("the required number of traced journeys is at least the pair gate plus one", () => {
  assert.ok(REQUIRED_SAMPLED_JOURNEYS >= 3);
});
