export const LOCALES = ["en", "tr-TR"];
export const STORAGE_KEY = "harborops.language";

export function normalizeLocale(value) {
  return typeof value === "string" && /^tr(?:[-_]|$)/i.test(value) ? "tr-TR" : "en";
}

export function initialLocale(storage, browserLanguages = []) {
  const saved = storage?.getItem?.(STORAGE_KEY);
  if (saved === "en" || saved === "tr-TR") return saved;
  return normalizeLocale(browserLanguages[0] ?? "en");
}

let selected;
const subscribers = new Set();

function environmentLocale() {
  if (typeof window === "undefined") return "en";
  try {
    return initialLocale(window.localStorage,
      window.navigator.languages?.length ? window.navigator.languages : [window.navigator.language]);
  } catch {
    return normalizeLocale(typeof navigator === "undefined" ? "en" :
      navigator.languages?.[0] ?? navigator.language);
  }
}

export function getLocale() {
  if (!selected) selected = environmentLocale();
  return selected;
}

export function subscribeLocale(listener) {
  subscribers.add(listener);
  return () => subscribers.delete(listener);
}

export function changeLocale(locale) {
  if (locale !== "en" && locale !== "tr-TR") return;
  const changed = getLocale() !== locale;
  selected = locale;
  if (typeof document !== "undefined") document.documentElement.lang = locale;
  try {
    if (typeof window !== "undefined") window.localStorage.setItem(STORAGE_KEY, locale);
  } catch { /* Browser storage may be disabled. */ }
  if (changed) for (const subscriber of subscribers) subscriber();
}

export function syncDocumentLanguage() {
  if (typeof document !== "undefined") document.documentElement.lang = getLocale();
}

export function translate(message, locale = getLocale(), parameters = {}) {
  if (typeof message !== "string") return message;
  const resolved = locale === "tr-TR" && Object.hasOwn(TURKISH, message)
    ? TURKISH[message] : message;
  return resolved.replace(/\{([A-Za-z][A-Za-z0-9_]*)\}/g,
    (match, key) => Object.hasOwn(parameters, key) ? String(parameters[key]) : match);
}

export function plural(count, forms, locale = getLocale()) {
  const rule = new Intl.PluralRules(locale).select(count);
  return translate(forms[rule] ?? forms.other ?? "", locale, { count });
}

export function formatNumber(value, locale = getLocale(), options) {
  return new Intl.NumberFormat(locale, options).format(value);
}

export function formatCurrency(value, currency, locale = getLocale()) {
  return new Intl.NumberFormat(locale, { style: "currency", currency }).format(value);
}

export function formatDateTime(value, locale = getLocale(), options = {}) {
  const date = value instanceof Date ? value : new Date(value);
  if (!Number.isFinite(date.getTime())) return "";
  return new Intl.DateTimeFormat(locale, {
    year: "numeric", month: "short", day: "2-digit",
    hour: "2-digit", minute: "2-digit", timeZone: "UTC", ...options
  }).format(date);
}

export function formatRelative(value, locale = getLocale(), now = Date.now()) {
  const delta = new Date(value).getTime() - now;
  if (!Number.isFinite(delta)) return "";
  const abs = Math.abs(delta);
  const [unit, divisor] = abs >= 86_400_000 ? ["day", 86_400_000]
    : abs >= 3_600_000 ? ["hour", 3_600_000]
    : abs >= 60_000 ? ["minute", 60_000] : ["second", 1000];
  return new Intl.RelativeTimeFormat(locale, { numeric: "auto" })
    .format(Math.round(delta / divisor), unit);
}

import { TURKISH } from "./messages.mjs";
