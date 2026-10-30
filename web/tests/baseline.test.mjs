import test from "node:test";
import assert from "node:assert/strict";
import { parseBaselineStatus } from "../src/lib/baseline.mjs";

const valid = {
  service: "HarborOps",
  datasetVersion: "m0-synthetic-v1",
  seededAtUtc: "2026-10-30T00:00:00+00:00",
  source: "synthetic"
};

test("accepts seeded synthetic baseline status", () => {
  assert.deepEqual(parseBaselineStatus(valid), valid);
});

test("rejects missing or unexpected baseline fields", () => {
  for (const payload of [null, [], {}, { ...valid, source: "live" },
    { ...valid, service: "Different" }, { ...valid, datasetVersion: "" }]) {
    assert.throws(() => parseBaselineStatus(payload));
  }
});

test("rejects invalid or non-UTC baseline timestamps", () => {
  for (const seededAtUtc of ["invalid", "2026-99-99T00:00:00Z",
    "2026-10-30T00:00:00+03:00", "2026-10-30", 1720000000]) {
    assert.throws(() => parseBaselineStatus({ ...valid, seededAtUtc }));
  }
});
