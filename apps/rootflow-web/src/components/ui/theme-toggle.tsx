import { MoonStar, SunMedium } from "lucide-react";
import { useTheme } from "next-themes";
import type { ComponentProps } from "react";

import { useI18n } from "@/app/providers/i18n-provider";
import { Button } from "@/components/ui/button";

interface ThemeToggleProps {
  className?: string;
  variant?: ComponentProps<typeof Button>["variant"];
}

export function ThemeToggle({ className, variant = "outline" }: ThemeToggleProps) {
  const { resolvedTheme, setTheme } = useTheme();
  const { t } = useI18n();
  const isResolved = resolvedTheme === "dark" || resolvedTheme === "light";
  const isDark = resolvedTheme === "dark";
  const icon = isResolved ? (isDark ? <SunMedium /> : <MoonStar />) : <span className="size-4 rounded-full bg-border/80" />;

  return (
    <Button
      variant={variant}
      size="icon"
      type="button"
      className={className}
      aria-label={isDark ? t("topbar.switchToLightMode") : t("topbar.switchToDarkMode")}
      disabled={!isResolved}
      onClick={() => setTheme(isDark ? "light" : "dark")}
    >
      {icon}
    </Button>
  );
}
