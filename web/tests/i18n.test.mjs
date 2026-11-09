import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { TURKISH } from "../src/i18n/messages.mjs";
import { normalizeLocale, initialLocale, translate, plural, formatNumber,
  formatCurrency, formatDateTime, formatRelative, STORAGE_KEY,
  changeLocale, getLocale, subscribeLocale } from "../src/i18n/i18n.mjs";

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

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");

test("locale switching notifies subscribers and remains independent of application state", () => {
  const events = [];
  const unsubscribe = subscribeLocale(() => events.push(getLocale()));
  const original = getLocale();
  const workflowSelection = { workflowStatus: "InTransit", draftText: "unsaved" };
  changeLocale(original === "en" ? "tr-TR" : "en");
  assert.equal(events.length, 1);
  assert.equal(workflowSelection.workflowStatus, "InTransit");
  assert.equal(workflowSelection.draftText, "unsaved");
  changeLocale(original);
  assert.equal(events.length, 2);
  unsubscribe();
  changeLocale(original === "en" ? "tr-TR" : "en");
  assert.equal(events.length, 2);
  changeLocale(original);
});

test("all statically referenced translation messages exist in the Turkish catalog", () => {
  const components = [
    "App.tsx", "auth/AuthPanel.tsx", "auth/OrganizationWorkspace.tsx",
    "analytics/KpiDrillPanel.tsx", "analytics/KpiGrantPanel.tsx",
    "analytics/OperationalDashboard.tsx", "assignments/ShipmentAssignmentsPanel.tsx",
    "maritime/MaritimeWorkspace.tsx", "notifications/NotificationCenter.tsx",
    "references/ReferenceDataWorkspace.tsx", "shipments/ShipmentExceptionPanel.tsx",
    "shipments/ShipmentTrackingTimeline.tsx", "shipments/ShipmentWorkspace.tsx"
  ];
  let assertions = 0;
  for (const name of components) {
    const source = readFileSync(resolve(root, "web", "src", name), "utf8");
    for (const match of source.matchAll(/\\bt\\("([^"\\n]+)"/g)) {
      assert.ok(Object.hasOwn(TURKISH, match[1]), name + ": missing " + match[1]);
      assertions++;
    }
    for (const option of source.matchAll(/<option\\b([^>]*)>([\\s\\S]*?)<\\/option>/g)) {
      if (option[2].includes("{t("))
        assert.match(option[1], /\\bvalue\\s*=/,
          name + ": translated visible option must preserve the server-facing value");
    }
  }
  assert.ok(assertions >= 200, "Expected broad message catalog integration");
});

test("all source-defined KPI labels and descriptions have Turkish presentations", () => {
  const source = readFileSync(resolve(root,
    "src/HarborOps.Domain/Reporting/OperationalKpiPolicy.cs"), "utf8");
  let count = 0;
  for (const match of source.matchAll(/new\\("K\\d{2}",\\s*"([^"]+)",\\s*"[^"]+",\\s*(?:null|"[^"]+"),\\s*"([^"]+)"\\)/g)) {
    assert.ok(Object.hasOwn(TURKISH, match[1]), "Missing KPI label " + match[1]);
    assert.ok(Object.hasOwn(TURKISH, match[2]), "Missing KPI description " + match[2]);
    count++;
  }
  assert.equal(count, 14);
});

test("source-approved notification titles and events use catalog text, not altered records", () => {
  const source = readFileSync(resolve(root,
    "src/HarborOps.Infrastructure/Notifications/NotificationStore.cs"), "utf8");
  const section = source.split("public static class NotificationText")[1]
    .split("public sealed class NotificationStore")[0];
  const sentences = [...section.matchAll(/=>\\s*"([^"]+)"/g)]
    .map(match => match[1]);
  assert.ok(sentences.length >= 8);
  for (const text of sentences) assert.ok(Object.hasOwn(TURKISH, text),
    "Notification label unavailable in Turkish: " + text);
});
