import { createI18n } from "vue-i18n";
import en from "./locales/en.json";
import zhCN from "./locales/zh-CN.json";

export type AppLocale = "en" | "zh-CN";

const STORAGE_KEY = "kitlib-combat-stats-locale";

function detectLocale(): AppLocale {
  const params = new URLSearchParams(window.location.search);
  const fromQuery = params.get("lang");
  if (fromQuery === "zh" || fromQuery === "zh-CN")
    return "zh-CN";
  if (fromQuery === "en")
    return "en";

  const stored = localStorage.getItem(STORAGE_KEY);
  if (stored === "en" || stored === "zh-CN")
    return stored;

  const nav = navigator.language.toLowerCase();
  if (nav.startsWith("zh"))
    return "zh-CN";
  return "en";
}

export const i18n = createI18n({
  legacy: false,
  locale: detectLocale(),
  fallbackLocale: "en",
  messages: {
    en,
    "zh-CN": zhCN,
  },
});

export function setLocale(locale: AppLocale) {
  i18n.global.locale.value = locale;
  localStorage.setItem(STORAGE_KEY, locale);
  document.documentElement.lang = locale === "zh-CN" ? "zh-CN" : "en";
}

document.documentElement.lang = i18n.global.locale.value === "zh-CN" ? "zh-CN" : "en";
