import { BellDot, LogOut, Menu, Search } from "lucide-react";
import { Link } from "react-router-dom";

import { useI18n } from "@/app/providers/i18n-provider";
import { ApiBaseUrlIndicator } from "@/components/diagnostics/api-base-url-indicator";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { LanguageSwitcher } from "@/components/ui/language-switcher";
import { ThemeToggle } from "@/components/ui/theme-toggle";
import { useAuth } from "@/features/auth/auth-provider";

interface TopbarProps {
  isDesktop: boolean;
  isSidebarCollapsed: boolean;
  onOpenNavigation: () => void;
  onToggleSidebar: () => void;
}

export function Topbar({ isDesktop, isSidebarCollapsed, onOpenNavigation, onToggleSidebar }: TopbarProps) {
  const { logout, session } = useAuth();
  const { t } = useI18n();
  const roleLabel =
    session?.role === "Owner"
      ? t("common.labels.owner")
      : session?.role === "Admin"
        ? t("common.labels.admin")
        : t("common.labels.member");
  const navigationLabel = isDesktop
    ? isSidebarCollapsed
      ? t("common.actions.showNavigation")
      : t("common.actions.hideNavigation")
    : t("topbar.menu");

  const initials = session?.user.fullName
    ?.split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");

  return (
    <header className="sticky top-0 z-30 px-4 pt-4 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-[1320px]">
        <div className="relative overflow-hidden rounded-[24px] border border-border/75 bg-card/74 px-4 py-2.5 shadow-[0_22px_46px_-36px_rgba(16,36,71,0.16)] backdrop-blur-xl before:pointer-events-none before:absolute before:inset-x-8 before:top-0 before:h-px before:bg-[linear-gradient(90deg,transparent,rgba(255,255,255,0.76),transparent)] sm:px-5">
          <div className="flex flex-col gap-2.5 lg:flex-row lg:items-center lg:justify-between">
            <div className="flex min-w-0 flex-wrap items-center gap-3">
              <Button
                variant="outline"
                size="sm"
                className="shrink-0 gap-2 rounded-[16px] border-border/78 bg-background/86 px-3.5 text-foreground"
                aria-label={navigationLabel}
                onClick={isDesktop ? onToggleSidebar : onOpenNavigation}
              >
                <Menu className="size-4" />
                <span>{isDesktop ? t("topbar.navigation") : navigationLabel}</span>
              </Button>

              {isDesktop ? (
                <div className="hidden min-w-0 items-center gap-3 lg:flex">
                  <div className="h-8 w-px bg-border/70" />
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <div className="truncate text-sm font-semibold tracking-[-0.02em] text-foreground">{session?.workspace.name ?? t("common.labels.workspace")}</div>
                      <Badge variant="secondary" className="shrink-0 px-2 py-0.5 text-[10px] font-semibold">
                        {roleLabel}
                      </Badge>
                    </div>
                    <div className="truncate text-xs text-muted-foreground">{t("topbar.workspaceSlug", { slug: session?.workspace.slug ?? "workspace" })}</div>
                  </div>
                </div>
              ) : (
                <div className="flex h-11 w-11 shrink-0 items-center justify-center">
                  <div className="flex h-10 w-10 items-center justify-center rounded-[16px] bg-background/72">
                    <img src="/rootflow-icon.png" alt="RootFlow" className="h-[2rem] w-[2rem] object-contain" />
                  </div>
                </div>
              )}
            </div>

            <div className="flex min-w-0 flex-wrap items-center gap-2 sm:justify-end">
              <Button variant="outline" className="min-w-0 flex-1 justify-start gap-3 px-3.5 text-left sm:min-w-[168px] sm:flex-none" asChild>
                <Link to="/assistant">
                  <Search className="size-4 shrink-0 text-muted-foreground" />
                  <span className="truncate text-foreground">{t("topbar.askAssistant")}</span>
                </Link>
              </Button>

              <div className="flex min-w-0 flex-wrap items-center gap-1.5">
                <LanguageSwitcher compact />
                <Button variant="outline" size="icon" aria-label={t("topbar.openNotificationsSettings")} asChild>
                  <Link to="/settings?section=notifications">
                    <BellDot />
                  </Link>
                </Button>
                <ThemeToggle />
                <Button variant="ghost" className="gap-2 px-3 text-muted-foreground hover:text-foreground" onClick={logout}>
                  <LogOut className="size-4" />
                  <span className="hidden xl:inline">{t("common.actions.signOut")}</span>
                </Button>
                <div className="flex min-w-0 items-center gap-2.5 rounded-[18px] border border-border/75 bg-background/78 px-2.5 py-1.5 shadow-[0_16px_30px_-28px_rgba(16,36,71,0.18)]">
                  <Avatar className="h-8 w-8">
                    <AvatarFallback>{initials || "RF"}</AvatarFallback>
                  </Avatar>
                  <div className="hidden min-w-0 lg:block">
                    <div className="truncate text-sm font-medium text-foreground">{session?.user.fullName ?? t("common.labels.rootflowUser")}</div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
        <div className="mt-2 flex justify-end px-1">
          <ApiBaseUrlIndicator />
        </div>
      </div>
    </header>
  );
}
