#!/usr/bin/env node
import assert from "node:assert/strict";
import http from "node:http";

const root = process.env.HARBOROPS_LOCAL_WEB_URL ?? "http://127.0.0.1:5173";
const target = new URL(root);

function get(path, host) {
  return new Promise((resolve, reject) => {
    const request = http.get({
      hostname: target.hostname,
      port: target.port || 80,
      path,
      headers: host ? { Host: host } : {}
    }, response => {
      response.resume();
      response.on("end", () => resolve({ status: response.statusCode, headers: response.headers }));
    });
    request.on("error", reject);
    request.setTimeout(10000, () => request.destroy(new Error("HTTP check timed out")));
  });
}

const page = await get("/");
assert.equal(page.status, 200, "Local frontend must remain reachable");
for (const key of [
  "content-security-policy", "x-frame-options", "x-content-type-options",
  "referrer-policy", "permissions-policy"
]) {
  assert.ok(page.headers[key], "Missing security header: " + key);
}
assert.match(String(page.headers["content-security-policy"]), /frame-ancestors 'none'/);
assert.equal(page.headers["x-frame-options"], "DENY");
assert.equal(page.headers["x-content-type-options"], "nosniff");

const denied = await get("/", "untrusted.example");
assert.equal(denied.status, 421, "Unexpected Host must be rejected");

const csrf = await get("/api/v1/auth/csrf");
assert.equal(csrf.status, 200, "Browser-to-API authentication bootstrap must work on local Compose");

const backendDenied = await get("/api/v1/auth/csrf", "untrusted.example");
assert.equal(backendDenied.status, 421, "Unknown host must not reach API");

console.log("Local browser-header, Host rejection and CSRF bootstrap checks passed.");
