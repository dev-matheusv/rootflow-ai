import { Languages } from "lucide-react";

import { useI18n, supportedLocales } from "@/app/providers/i18n-provider";
import { cn } from "@/lib/utils";

interface LanguageSwitcherProps {
  compact?: boolean;
  className?: string;
}

export function LanguageSwitcher({ compact = false, className }: LanguageSwitcherProps) {
  const { locale, setLocale, t } = useI18n();

  return (
    <div
      className={cn(
        "flex min-w-0 items-center gap-1 rounded-[18px] border border-border/78 bg-background/78 p-1 shadow-[0_14px_28px_-24px_rgba(16,36,71,0.18)]",
        className,
      )}
    >
      {!compact ? (
        <div className="hidden items-center gap-1.5 pl-2 pr-1 text-xs font-semibold uppercase tracking-[0.14em] text-muted-foreground sm:flex">
          <Languages className="size-3.5" />
          <span>{t("common.language.label")}</span>
        </div>
      ) : (
        <div className="flex items-center pl-1 text-muted-foreground sm:hidden">
          <Languages className="size-3.5" />
        </div>
      )}
      {supportedLocales.map((supportedLocale) => {
        const isActive = locale === supportedLocale;
        const isEnglish = supportedLocale === "en";

        return (
          <button
            key={supportedLocale}
            type="button"
            className={cn(
              "min-w-0 rounded-[14px] px-2.5 py-1.5 text-xs font-semibold tracking-[0.08em] transition-[background-color,color,box-shadow] duration-200",
              isActive
                ? "bg-primary text-primary-foreground shadow-[0_14px_26px_-20px_rgba(37,99,235,0.42)]"
                : "text-foreground/78 hover:bg-secondary/80 hover:text-foreground",
            )}
            aria-pressed={isActive}
            aria-label={t(isEnglish ? "common.language.switchToEnglish" : "common.language.switchToPortuguese")}
            onClick={() => setLocale(supportedLocale)}
          >
            {t(isEnglish ? "common.language.english" : "common.language.portuguese")}
          </button>
        );
      })}
    </div>
  );
}
