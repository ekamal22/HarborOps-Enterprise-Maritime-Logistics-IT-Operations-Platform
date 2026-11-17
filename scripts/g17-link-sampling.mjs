// The deterministic keyed sampling decision for a linked tracking journey, as implemented in
// src/HarborOps.Infrastructure/Observability/M17CorrelationDigest.cs (SampleLinkedJourney): the leading 64 bits of the
// 256-bit digest, masked to a non-negative signed value, must be below one tenth of the signed maximum. The verifier
// uses it to know, before any trace is looked up, how many of its receipts the API and the worker are bound to trace.
const MAX_SIGNED = 0x7fffffffffffffffn;
const THRESHOLD = MAX_SIGNED / 10n;

export function linkedJourneySampled(digest) {
  if (typeof digest !== "string" || !/^[0-9a-f]{64}$/.test(digest)) return false;
  return (BigInt("0x" + digest.slice(0, 16)) & MAX_SIGNED) < THRESHOLD;
}

// How many further receipts are needed so that at least `required` of them are sampled.
export const REQUIRED_SAMPLED_JOURNEYS = 3;
export function sampledCount(digests) { return [...digests].filter(linkedJourneySampled).length; }

// Cross-language vectors: the same table is asserted by tests/HarborOps.IntegrationTests/M17LinkSamplingVectorTests.cs.
export const SAMPLING_VECTORS = [
  ["0000000000000000" + "a".repeat(48), true],
  ["0ccccccccccccccb" + "b".repeat(48), true],
  ["0ccccccccccccccc" + "b".repeat(48), false],
  ["0ccccccccccccccd" + "b".repeat(48), false],
  ["7fffffffffffffff" + "c".repeat(48), false],
  ["8000000000000000" + "d".repeat(48), true],
  ["8ccccccccccccccb" + "e".repeat(48), true],
  ["8ccccccccccccccc" + "e".repeat(48), false],
  ["ffffffffffffffff" + "f".repeat(48), false],
  ["1000000000000000" + "0".repeat(48), false]
];
