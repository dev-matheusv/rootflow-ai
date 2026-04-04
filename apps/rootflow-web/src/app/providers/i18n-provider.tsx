import { createContext, useCallback, useContext, useEffect, useMemo, useState, type PropsWithChildren } from "react";

import { messages } from "@/lib/i18n/messages";
import { defaultLocale, intlLocaleMap, isSupportedLocale, supportedLocales, type AppLocale } from "@/lib/i18n/types";

const I18N_STORAGE_KEY = "rootflow.locale";

type TranslationValue = string | number;

interface I18nContextValue {
  locale: AppLocale;
  intlLocale: string;
  setLocale: (locale: AppLocale) => void;
  t: (key: string, values?: Record<string, TranslationValue>) => string;
}

const I18nContext = createContext<I18nContextValue | null>(null);

function getStoredLocale() {
  if (typeof window === "undefined") {
    return defaultLocale;
  }

  const storedLocale = window.localStorage.getItem(I18N_STORAGE_KEY);
  return isSupportedLocale(storedLocale) ? storedLocale : defaultLocale;
}

function getMessageValue(tree: Record<string, unknown>, key: string) {
  return key.split(".").reduce<unknown>((currentValue, part) => {
    if (!currentValue || typeof currentValue !== "object" || !(part in currentValue)) {
      return null;
    }

    return (currentValue as Record<string, unknown>)[part];
  }, tree);
}

function interpolate(message: string, values?: Record<string, TranslationValue>) {
  if (!values) {
    return message;
  }

  return message.replace(/\{\{(\w+)\}\}/g, (_, token: string) => `${values[token] ?? ""}`);
}

export function I18nProvider({ children }: PropsWithChildren) {
  const [locale, setLocaleState] = useState<AppLocale>(() => getStoredLocale());

  useEffect(() => {
    if (typeof window === "undefined") {
      return;
    }

    window.localStorage.setItem(I18N_STORAGE_KEY, locale);
    document.documentElement.lang = locale;
  }, [locale]);

  const setLocale = useCallback((nextLocale: AppLocale) => {
    setLocaleState(nextLocale);
  }, []);

  const t = useCallback(
    (key: string, values?: Record<string, TranslationValue>) => {
      const message = getMessageValue(messages[locale] as Record<string, unknown>, key);

      if (typeof message !== "string") {
        return key;
      }

      return interpolate(message, values);
    },
    [locale],
  );

  const contextValue = useMemo<I18nContextValue>(
    () => ({
      locale,
      intlLocale: intlLocaleMap[locale],
      setLocale,
      t,
    }),
    [locale, setLocale, t],
  );

  return <I18nContext.Provider value={contextValue}>{children}</I18nContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useI18n() {
  const context = useContext(I18nContext);

  if (!context) {
    throw new Error("useI18n must be used within an I18nProvider.");
  }

  return context;
}

// eslint-disable-next-line react-refresh/only-export-components
export { supportedLocales };
