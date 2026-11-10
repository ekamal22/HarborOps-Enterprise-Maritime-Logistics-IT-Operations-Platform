import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { TURKISH } from "../src/i18n/messages.mjs";
import { normalizeLocale, initialLocale, translate, plural, formatNumber,
  formatCurrency, formatPercentage, formatDateTime, formatLocalDateTime, formatRelative, STORAGE_KEY,
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
    for (const match of source.matchAll(/\bt\("([^"\n]+)"/g)) {
      assert.ok(Object.hasOwn(TURKISH, match[1]), name + ": missing " + match[1]);
      assertions++;
    }
    for (const option of source.matchAll(/<option\b([^>]*)>([\s\S]*?)<\/option>/g)) {
      if (option[2].includes("{t("))
        assert.match(option[1], /\bvalue\s*=/,
          name + ": translated visible option must preserve the server-facing value");
    }
  }
  assert.ok(assertions >= 200, "Expected broad message catalog integration");
});

test("all source-defined KPI labels and descriptions have Turkish presentations", () => {
  const source = readFileSync(resolve(root,
    "src/HarborOps.Domain/Reporting/OperationalKpiPolicy.cs"), "utf8");
  let count = 0;
  for (const match of source.matchAll(/new\("K\d{2}",\s*"([^"]+)",\s*"[^"]+",\s*(?:null|"[^"]+"),\s*"([^"]+)"\)/g)) {
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
  const sentences = [...section.matchAll(/=>\s*"([^"]+)"/g)]
    .map(match => match[1]);
  assert.ok(sentences.length >= 8);
  for (const text of sentences) assert.ok(Object.hasOwn(TURKISH, text),
    "Notification label unavailable in Turkish: " + text);
});

test("local session times are explicit and never silently use UTC", () => {
  const instant = "2026-11-09T11:30:00Z";
  const utc = formatDateTime(instant, "tr-TR", {timeZone: "UTC"});
  const local = formatLocalDateTime(instant, "tr-TR", "Europe/Istanbul");
  assert.match(utc, /11[:.]30/);
  assert.match(local, /14[:.]30/);
  assert.notEqual(local, utc);
  assert.equal(formatLocalDateTime("invalid date", "tr-TR", "Europe/Istanbul"), "");
});

test("distinct roles and acknowledgment states remain understandable", () => {
  assert.notEqual(translate("Manager", "tr-TR"), translate("Administrator", "tr-TR"));
  assert.notEqual(translate("Acknowledged", "tr-TR"), translate("Exception acknowledged", "tr-TR"));
  for (const role of ["Analyst", "Manager", "Support", "Administrator", "Auditor"]) {
    assert.ok(Object.hasOwn(TURKISH, role), "Missing role " + role);
    assert.notEqual(translate(role, "tr-TR"), role);
  }
  assert.equal(translate("Arrived", "tr-TR"), "Varış kaydedildi");
  assert.match(translate("Disable this account and revoke every session?", "tr-TR"), /oturumlar sonlandırılsın/);
});

test("source-defined exception reasons and evidence states have localized explanations", () => {
  const source = readFileSync(resolve(root,
    "src/HarborOps.Domain/Exceptions/ShipmentExceptionPolicy.cs"), "utf8");
  const reasons = [...new Set([...source.matchAll(/"((?:[a-z]+_)+[a-z]+)"/g)]
    .map(match => match[1]))];
  assert.equal(reasons.length, 16, "Unexpected exception reason-code contract change");
  for (const code of reasons) {
    assert.ok(Object.hasOwn(TURKISH, code), "Missing exception reason " + code);
    assert.notEqual(translate(code, "tr-TR"), code);
  }
  for (const value of ["Info", "Warning", "High", "NotApplicable", "Unverified",
    "RecordedEquipment", "RecordedWorkflow", "ScheduleOnly", "Conflict"]) {
    assert.ok(Object.hasOwn(TURKISH, value), "Missing evidence label " + value);
  }
});

test("source-defined maritime equipment types display Turkish without changing wire values", () => {
  const policy = readFileSync(resolve(root,
    "src/HarborOps.Domain/Maritime/MaritimePolicy.cs"), "utf8");
  const match = policy.match(/Types = \[([^\]]+)\]/);
  assert.ok(match, "Equipment type policy not found");
  const types = [...match[1].matchAll(/"([^"]+)"/g)].map(m => m[1]);
  assert.equal(types.length, 6);
  for (const type of types) {
    assert.ok(Object.hasOwn(TURKISH, type), "Missing equipment type " + type);
    assert.notEqual(translate(type, "tr-TR"), type);
  }
  for (const code of ["GTIN", "GTOT", "ARRI", "DEPA"])
    assert.ok(translate(code, "tr-TR").includes(code), "Display must retain event code " + code);
});

test("newly introduced platform text and stable authentication errors are translated", () => {
  for (const source of ["Operations platform", "Maritime operations overview",
    "Synthetic demo references are not live vessel positions or predictive ETA.",
    "Authentication required. Sign in again.", "Unable to sign in with these credentials",
    "Request validation failed", "The request could not be completed."]) {
    assert.ok(Object.hasOwn(TURKISH, source), "Missing text " + source);
  }
  const source = readFileSync(resolve(root, "web/src/auth/AuthPanel.tsx"), "utf8");
  const body = source.split("const safeAuthMessages:")[1].split("};")[0];
  for (const match of body.matchAll(/:\s*"([^"]+)"/g))
    assert.ok(Object.hasOwn(TURKISH, match[1]), "Untranslated stable authentication error " + match[1]);
});

test("all translated interpolation placeholders match their English source", () => {
  const keys = text => [...text.matchAll(/\{([A-Za-z][A-Za-z0-9_]*)\}/g)]
    .map(match => match[1]).sort();
  for (const [english, turkish] of Object.entries(TURKISH))
    assert.deepEqual(keys(turkish), keys(english), "Placeholder contract changed for " + english);
  assert.equal(translate("{count} unread notifications", "tr-TR", {count: "1.234"}),
    "1.234 okunmamış bildirim");
  assert.equal(translate("Status changed from {from} to {to} at {timestamp}", "tr-TR",
    {from: "Taslak", to: "Varış kaydedildi", timestamp: "09.11.2026 UTC"}),
    "Durum geçişi: Taslak → Varış kaydedildi (09.11.2026 UTC)");
});

test("Turkish percentages use locale-defined placement and preserved half-up rounding", () => {
  assert.equal(formatPercentage(1, 8, "tr-TR"), "%12,50");
  assert.equal(formatPercentage(1, 8, "en"), "12.50%");
  assert.equal(formatPercentage(1, 3, "tr-TR"), "%33,33");
  assert.equal(formatPercentage(0, 8, "tr-TR"), "%0,00");
  assert.equal(formatPercentage(1, 0, "tr-TR"), "Not enough data");
  assert.equal(formatPercentage(Number.NaN, 8, "tr-TR"), "Not enough data");
  const dashboard = readFileSync(resolve(root,
    "web/src/analytics/OperationalDashboard.tsx"), "utf8");
  assert.match(dashboard, /const \{ t, date, number, percent \} = useI18n\(\)/);
  assert.match(dashboard, /percent\(n, den\)/);
  assert.doesNotMatch(dashboard, /format\(rounded\) \+ "%"/);
});

test("backend-defined tracking states all have Turkish presentation labels", () => {
  const reducer = readFileSync(resolve(root,
    "src/HarborOps.Domain/Tracking/TrackingTimelinePolicy.cs"), "utf8");
  const names = ["EquipmentIn", "EquipmentOut", "Mixed", "Conflict", "Unknown"];
  for (const name of names) {
    assert.match(reducer, new RegExp('"' + name + '"'), "Expected backend tracking state " + name);
    assert.ok(Object.hasOwn(TURKISH, name), "Untranslated tracking state " + name);
    assert.notEqual(translate(name, "tr-TR"), name);
  }
  const timeline = readFileSync(resolve(root,
    "web/src/shipments/ShipmentTrackingTimeline.tsx"), "utf8");
  assert.match(timeline, /t\(projection.observedTrackingState\)/);
  assert.match(timeline, /t\(item.observedState\)/);
});

test("assignment selectors use complete translated prompts while preserving IDs", () => {
  const panel = readFileSync(resolve(root,
    "web/src/assignments/ShipmentAssignmentsPanel.tsx"), "utf8");
  assert.ok(panel.includes('t(kind === "container-assignments" ? "Select container for assignment" : "Select voyage for assignment")'));
  assert.doesNotMatch(panel, /t\("Select"\)\}\{label\(kind\)\.toLowerCase\(\)/);
  for (const key of ["Select container for assignment", "Select voyage for assignment"])
    assert.notEqual(translate(key, "tr-TR"), key);
  assert.match(panel, /<option value="">/);
  assert.match(panel, /<option key=\{row.id\} value=\{row.id\}>/);
});

test("exception drill uses live API fields and a context-specific review label", () => {
  const store = readFileSync(resolve(root,
    "src/HarborOps.Infrastructure/Exceptions/ShipmentExceptionStore.cs"), "utf8");
  assert.match(store, /string Condition, string State, string Severity/);
  const drill = readFileSync(resolve(root, "web/src/analytics/KpiDrillPanel.tsx"), "utf8");
  assert.match(drill, /state: string; condition: string; ruleCode: string/);
  assert.match(drill, /setExceptionState\(item.exception\)/);
  assert.doesNotMatch(drill, /lifecycleState|conditionState/);
  assert.match(drill, /"Exception acknowledged"/);
  assert.notEqual(translate("Acknowledged", "tr-TR"),
    translate("Exception acknowledged", "tr-TR"));
});

test("Administrator role label reflects the organization permission boundary", () => {
  const backend = readFileSync(resolve(root,
    "src/HarborOps.Domain/Organizations/OrganizationPolicy.cs"), "utf8");
  assert.match(backend, /public enum OrganizationRole \{ Analyst, Manager, Support, Administrator, Auditor \}/);
  assert.equal(translate("Administrator", "tr-TR"), "Kuruluş Yöneticisi");
  assert.notEqual(translate("Manager", "tr-TR"), translate("Administrator", "tr-TR"));
});

test("KPI arrival-transition descriptions do not use the obsolete Turkish wording", () => {
  for (const key of ["First recorded Arrived at or before the proven planned arrival",
    "First recorded Arrived no later than proven plan plus 60 minutes"]) {
    const translated = translate(key, "tr-TR");
    assert.doesNotMatch(translated, /Vardı/);
    assert.match(translated, /varış/i);
  }
});

test("open tabs adopt external language preference changes without duplicating mutations", () => {
  const existingWindow = globalThis.window;
  const existingDocument = globalThis.document;
  const prior = getLocale();
  const events = [];
  const handlers = new Map();
  const added = [];
  const removed = [];
  globalThis.window = {
    addEventListener: (name, callback) => { handlers.set(name, callback); added.push(name); },
    removeEventListener: (name, callback) => {
      if (handlers.get(name) === callback) handlers.delete(name);
      removed.push(name);
    },
    localStorage: { setItem: () => { throw new Error("Not an application mutation"); } }
  };
  globalThis.document = { documentElement: { lang: prior } };
  let unsubscribe;
  try {
    unsubscribe = subscribeLocale(() => events.push(getLocale()));
    assert.ok(handlers.has("storage"));
    handlers.get("storage")({ key: "unrelated", newValue: "tr-TR" });
    assert.deepEqual(events, []);
    handlers.get("storage")({ key: STORAGE_KEY, newValue: "tr-TR" });
    assert.equal(getLocale(), "tr-TR");
    assert.equal(globalThis.document.documentElement.lang, "tr-TR");
    handlers.get("storage")({ key: STORAGE_KEY, newValue: "tr-TR" });
    assert.equal(events.length, prior === "tr-TR" ? 0 : 1);
    handlers.get("storage")({ key: STORAGE_KEY, newValue: "en" });
    assert.equal(getLocale(), "en");
    assert.equal(globalThis.document.documentElement.lang, "en");
    assert.ok(events.length > 0);
  } finally {
    unsubscribe?.();
    assert.deepEqual(added, ["storage"]);
    assert.deepEqual(removed, ["storage"]);
    if (existingWindow === undefined) delete globalThis.window;
    else globalThis.window = existingWindow;
    if (existingDocument === undefined) delete globalThis.document;
    else globalThis.document = existingDocument;
    changeLocale(prior);
  }
});


test("current G13 reporting errors remain localized without altering API error codes", () => {
  const backend = readFileSync(resolve(root,
    "src/HarborOps.Api/Reporting/OperationalKpiEndpoints.cs"), "utf8");
  const errors = [...new Set([...backend.matchAll(/Fail\(ctx,\s*\d+,\s*[^,]+,\s*"([^"]+)"\)/g)]
    .map(match => match[1]))];
  assert.ok(errors.includes("The organization's reporting capacity is currently in use"),
    "Missing tenant reporting capacity API contract");
  assert.ok(errors.length >= 20, "Reporting error coverage unexpectedly reduced");
  for (const message of errors) {
    assert.ok(Object.hasOwn(TURKISH, message), "Untranslated reporting error " + message);
    assert.notEqual(translate(message, "tr-TR"), message);
  }
  assert.match(translate("The organization's reporting capacity is currently in use", "tr-TR"),
    /raporlama kapasitesi/);
});
