import test from "node:test";
import assert from "node:assert/strict";
import { TURKISH } from "../src/i18n/messages.mjs";
import { normalizeLocale, initialLocale, translate, plural, formatNumber,
  formatCurrency, formatDateTime, formatRelative, STORAGE_KEY } from "../src/i18n/i18n.mjs";

test("explicit preference overrides the browser locale", () => {
  assert.equal(initialLocale({getItem: k => k === STORAGE_KEY ? "en" : null}, ["tr-TR"]), "en");
  assert.equal(initialLocale({getItem: () => "tr-TR"}, ["en-US"]), "tr-TR");
  assert.equal(initialLocale({getItem: () => null}, ["tr-TR", "en-US"]), "tr-TR");
  assert.equal(initialLocale({getItem: () => null}, ["de-DE"]), "en");
  assert.equal(normalizeLocale("tr-CY"), "tr-TR");
});
test("catalog is non-empty, native strings and deterministic fallbacks", () => {
  assert.ok(Object.keys(TURKISH).length > 100);
  assert.equal(translate("Sign in", "tr-TR"), "Giriş yap");
  assert.equal(translate("Sign in", "en"), "Sign in");
  assert.equal(translate("Unrecognized source label", "tr-TR"), "Unrecognized source label");
  assert.equal(translate("Signed in as {name}", "en", {name: "Deniz"}), "Signed in as Deniz");
  assert.equal(Object.values(TURKISH).every(v => typeof v === "string" && v.trim()), true);
});
test("Turkish number, money, date and relative formatting preserve inputs", () => {
  assert.match(formatNumber(12345.67, "tr-TR"), /12.345,67/);
  assert.match(formatCurrency(1234.5, "TRY", "tr-TR"), /1.234,50/);
  assert.match(formatDateTime("2026-11-09T11:30:00Z", "tr-TR"), /2026/);
  assert.equal(formatRelative("2026-11-09T11:30:00Z", "tr-TR", Date.parse("2026-11-09T10:30:00Z")).length > 0, true);
  assert.equal(plural(5, {one: "{count} item", other: "{count} items"}, "en"), "5 items");
  assert.equal(plural(5, {other: "{count} kayıt"}, "tr-TR"), "5 kayıt");
});
