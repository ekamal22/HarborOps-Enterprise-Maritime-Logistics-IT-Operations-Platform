import { changeLocale } from "./i18n.mjs";
import { useI18n } from "./useI18n";

export default function LanguageSwitcher() {
  const { locale, t } = useI18n();
  return <label className="flex shrink-0 items-center gap-2 text-xs text-slate-200">
    <span className="sr-only sm:not-sr-only">{t("Language")}</span>
    <select aria-label={t("Select language")} value={locale}
      onChange={event => changeLocale(event.target.value as "en" | "tr-TR")}
      className="max-w-28 rounded border border-slate-600 bg-slate-950 px-2 py-2 text-slate-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-cyan-300">
      <option value="en">English</option>
      <option value="tr-TR">Türkçe</option>
    </select>
  </label>;
}
