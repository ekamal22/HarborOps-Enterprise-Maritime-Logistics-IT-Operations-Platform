import assert from "node:assert/strict";
export function verifyOwnedWorkerLog(raw,privateValues) {
  assert.equal(typeof raw,"string");
  assert.ok(raw.length>0 && Buffer.byteLength(raw)<4*1024*1024,
    "A real bounded disposable worker log is required");
  assert.match(raw,/Tracking work polling started|Tracking worker disposition/,
    "No positive working-service log witness");
  assert.ok(Array.isArray(privateValues)&&privateValues.length>=4,
    "No negative synthetic canary controls were supplied");
  const lower=raw.toLowerCase();
  for(const value of privateValues) {
    assert.ok(typeof value==="string"&&value.length>=8);
    assert.ok(!lower.includes(value.toLowerCase()),
      "Owned worker logs exposed a synthetic private identifier");
  }
  assert.ok(!/\bBearer\s+[A-Za-z0-9._~-]{16,}|\bPassword\s*[:=]\s*\S{8,}/i.test(raw),
    "Owned worker logs contain a credential-shaped value");
  return {checked_values:privateValues.length,bytes:Buffer.byteLength(raw)};
}
