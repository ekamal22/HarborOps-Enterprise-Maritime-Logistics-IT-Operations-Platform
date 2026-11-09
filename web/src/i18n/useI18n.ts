import { useMemo, useSyncExternalStore } from "react";
import { getLocale, subscribeLocale, translate, formatNumber, formatCurrency,
  formatDateTime, formatLocalDateTime, formatRelative, plural, type Locale } from "./i18n.mjs";

export function useI18n() {
  const locale = useSyncExternalStore(subscribeLocale, getLocale, () => "en" as Locale);
  return useMemo(() => ({
    locale,
    t: (message: string, values?: Record<string, string | number>) =>
      translate(message, locale, values),
    number: (value: number, options?: Intl.NumberFormatOptions) =>
      formatNumber(value, locale, options),
    currency: (value: number, code: string) => formatCurrency(value, code, locale),
    date: (value: string | number | Date, options?: Intl.DateTimeFormatOptions) =>
      formatDateTime(value, locale, options),
    localDate: (value: string | number | Date, timeZone?: string) =>
      formatLocalDateTime(value, locale, timeZone),
    relative: (value: string | number | Date, now?: number) =>
      formatRelative(value, locale, now),
    plural: (count: number, forms: Partial<Record<Intl.LDMLPluralRule, string>>) =>
      plural(count, forms, locale)
  }), [locale]);
}
