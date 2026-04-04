export const supportedLocales = ["en", "pt-BR"] as const;

export type AppLocale = (typeof supportedLocales)[number];

export const defaultLocale: AppLocale = "en";

export const intlLocaleMap: Record<AppLocale, string> = {
  en: "en-US",
  "pt-BR": "pt-BR",
};

export function isSupportedLocale(value: string | null | undefined): value is AppLocale {
  return supportedLocales.includes(value as AppLocale);
}
