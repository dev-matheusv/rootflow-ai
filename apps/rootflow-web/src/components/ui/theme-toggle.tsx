import { MoonStar, SunMedium } from "lucide-react";
import { useTheme } from "next-themes";

import { useI18n } from "@/app/providers/i18n-provider";
import { Button } from "@/components/ui/button";

export function ThemeToggle() {
  const { resolvedTheme, setTheme } = useTheme();
  const { t } = useI18n();
  const isResolved = resolvedTheme === "dark" || resolvedTheme === "light";
  const isDark = resolvedTheme === "dark";
  const icon = isResolved ? (isDark ? <SunMedium /> : <MoonStar />) : <span className="size-4 rounded-full bg-border/80" />;

  return (
    <Button
      variant="outline"
      size="icon"
      type="button"
      aria-label={isDark ? t("topbar.switchToLightMode") : t("topbar.switchToDarkMode")}
      disabled={!isResolved}
      onClick={() => setTheme(isDark ? "light" : "dark")}
    >
      {icon}
    </Button>
  );
}
