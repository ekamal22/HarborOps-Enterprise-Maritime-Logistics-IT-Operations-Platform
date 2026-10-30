import test from "node:test";
import assert from "node:assert/strict";
import { normalizeApiOrigin } from "../src/lib/config.mjs";

test("uses local API when unspecified", () => {
  assert.equal(normalizeApiOrigin(), "http://localhost:8080");
});
test("normalizes a configured HTTPS origin", () => {
  assert.equal(normalizeApiOrigin("https://example.test/"), "https://example.test");
});
test("rejects credentials and embedded paths", () => {
  assert.throws(() => normalizeApiOrigin("http://user:pass@host.test"));
  assert.throws(() => normalizeApiOrigin("https://host.test/api"));
  assert.throws(() => normalizeApiOrigin("file:///tmp/test"));
});
