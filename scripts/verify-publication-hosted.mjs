#!/usr/bin/env node
import assert from "node:assert/strict";

const input = process.argv[2];
if (!input) throw new Error("Usage: node scripts/verify-publication-hosted.mjs https://verified.example");
const base = new URL(input);
assert.equal(base.protocol, "https:", "Hosted verification requires HTTPS");
assert.equal(base.pathname, "/", "Specify the hosted origin rather than an API path");
assert.ok(!["localhost", "127.0.0.1"].includes(base.hostname), "Use the public hosted domain");

const page = await fetch(base, { redirect: "manual" });
assert.equal(page.status, 200, "Hosted frontend must return HTTP 200 over TLS");
for (const name of [
  "content-security-policy", "x-frame-options", "x-content-type-options",
  "referrer-policy", "permissions-policy", "strict-transport-security"
]) {
  assert.ok(page.headers.has(name), "Missing hosted response header: " + name);
}
assert.match(page.headers.get("content-security-policy"), /upgrade-insecure-requests/);
assert.match(page.headers.get("strict-transport-security"), /max-age=\d+/);

const insecure = new URL(base);
insecure.protocol = "http:";
const redirected = await fetch(insecure, { redirect: "manual" });
assert.equal(redirected.status, 308, "Plaintext HTTP must redirect to HTTPS");
assert.equal(redirected.headers.get("location"), base.href);

const csrf = await fetch(new URL("/api/v1/auth/csrf", base), { redirect: "manual" });
assert.equal(csrf.status, 200, "TLS reverse-proxy authentication bootstrap must work");
const cookie = csrf.headers.get("set-cookie") ?? "";
assert.match(cookie, /secure/i, "Hosted antiforgery cookie must be Secure");
assert.match(cookie, /httponly/i, "Hosted antiforgery cookie must be HttpOnly");
console.log("Hosted HTTPS, security headers, redirect and Secure-cookie bootstrap passed.");
