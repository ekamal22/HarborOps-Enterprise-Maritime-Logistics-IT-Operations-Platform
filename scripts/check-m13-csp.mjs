import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import assert from "node:assert/strict";

const root = resolve(import.meta.dirname, "..");
function csp(relative) {
  const source = readFileSync(resolve(root, relative), "utf8");
  const matches = [...source.matchAll(/add_header\s+Content-Security-Policy\s+"([^"]+)"\s+always;/g)];
  assert.equal(matches.length, 1, `${relative}: exactly one CSP header required`);
  const directives = new Map();
  for (const part of matches[0][1].split(";").map(s => s.trim()).filter(Boolean)) {
    const [name, ...values] = part.split(/\s+/);
    assert.ok(!directives.has(name), `${relative}: duplicate directive ${name}`);
    directives.set(name, values);
  }
  assert.ok(source.includes("proxy_read_timeout 30s;"), `${relative}: preserve API proxy envelope`);
  return directives;
}
const local = csp("web/nginx.conf");
const hosted = csp("web/nginx.hosted.conf.template");
for (const [name, values] of local) {
  if (name === "upgrade-insecure-requests") continue;
  assert.deepEqual(hosted.get(name), values, `hosted ${name} diverges from local policy`);
}
for (const [name, values] of hosted) {
  if (name === "upgrade-insecure-requests") continue;
  assert.deepEqual(local.get(name), values, `local ${name} diverges from hosted policy`);
}
assert.deepEqual(hosted.get("upgrade-insecure-requests"), [],
  "hosted policy must upgrade mixed-content requests");
assert.equal(local.has("upgrade-insecure-requests"), false,
  "loopback HTTP must not be upgraded");
for (const policy of [local, hosted]) {
  for (const directive of ["default-src", "base-uri", "object-src",
      "frame-ancestors", "form-action", "script-src", "style-src",
      "img-src", "font-src", "connect-src"]) {
    assert.ok(policy.has(directive), `missing ${directive}`);
  }
  assert.deepEqual(policy.get("script-src"), ["'self'"]);
  assert.deepEqual(policy.get("style-src"), ["'self'"]);
  assert.deepEqual(policy.get("object-src"), ["'none'"]);
  assert.deepEqual(policy.get("frame-ancestors"), ["'none'"]);
  assert.equal(policy.get("script-src").includes("'unsafe-inline'"), false);
  assert.equal(policy.get("style-src").includes("'unsafe-inline'"), false);
}
console.log("M13_CSP local_and_hosted_profiles=PASS");
