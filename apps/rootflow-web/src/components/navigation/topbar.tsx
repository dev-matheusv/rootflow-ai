import { BellDot, LogOut, Menu, Search } from "lucide-react";
import { Link } from "react-router-dom";

import { useI18n } from "@/app/providers/i18n-provider";
import { TopbarCreditSummary } from "@/components/billing/topbar-credit-summary";
import { RootFlowBrand } from "@/components/branding/rootflow-brand";
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
  const workspaceName = session?.workspace.name ?? t("common.labels.workspace");
  const workspaceSlug = t("topbar.workspaceSlug", { slug: session?.workspace.slug ?? "workspace" });

  const initials = session?.user.fullName
    ?.split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");

  return (
    <header className="sticky top-0 z-30 px-4 pt-4 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-[1320px]">
        <div className="relative overflow-hidden rounded-[24px] border border-border/75 bg-card/74 px-4 py-3 shadow-[0_22px_46px_-36px_rgba(16,36,71,0.16)] backdrop-blur-xl before:pointer-events-none before:absolute before:inset-x-8 before:top-0 before:h-px before:bg-[linear-gradient(90deg,transparent,rgba(255,255,255,0.76),transparent)] sm:px-5">
          <div className="flex flex-col gap-3 lg:grid lg:grid-cols-[minmax(0,320px)_minmax(0,1fr)_auto] lg:items-center lg:gap-4">
            <div className="flex min-w-0 items-center gap-2.5">
              <Button
                variant="outline"
                size="sm"
                className="shrink-0 gap-2 rounded-[16px] border-border/78 bg-background/86 px-3.5 text-foreground"
                aria-label={navigationLabel}
                onClick={isDesktop ? onToggleSidebar : onOpenNavigation}
              >
                <Menu className="size-4" />
                <span>{t("topbar.navigation")}</span>
              </Button>

              <div className="flex min-w-0 flex-1 items-center gap-2.5 rounded-[20px] border border-border/78 bg-background/76 px-2.5 py-2 shadow-[0_16px_30px_-28px_rgba(16,36,71,0.16)]">
                <div className="flex size-10 shrink-0 items-center justify-center rounded-[16px] border border-primary/14 bg-primary/[0.09]">
                  <RootFlowBrand variant="icon" size="sm" className="h-7 w-7" />
                </div>
                <div className="min-w-0 flex-1">
                  <div className="flex min-w-0 items-center gap-2">
                    <div className="truncate text-sm font-semibold tracking-[-0.02em] text-foreground">{workspaceName}</div>
                    <Badge variant="secondary" className="hidden shrink-0 px-2 py-0.5 text-[10px] font-semibold sm:inline-flex">
                      {roleLabel}
                    </Badge>
                  </div>
                  <div className="truncate text-xs text-muted-foreground">{workspaceSlug}</div>
                </div>
              </div>
            </div>

            <div className="min-w-0">
              <Button
                variant="outline"
                className="h-11 w-full justify-start gap-3 rounded-[18px] border-border/80 bg-background/84 px-4 text-left shadow-[0_16px_30px_-24px_rgba(16,36,71,0.16)]"
                asChild
              >
                <Link to="/assistant">
                  <Search className="size-4 shrink-0 text-muted-foreground" />
                  <span className="truncate text-foreground">{t("topbar.askAssistant")}</span>
                </Link>
              </Button>
            </div>

            <div className="flex min-w-0 flex-col gap-2.5 sm:flex-row sm:flex-wrap sm:items-center sm:justify-end lg:flex-nowrap lg:justify-self-end">
              <TopbarCreditSummary workspaceId={session?.workspace.id} className="w-full sm:w-[224px]" />

              <div className="flex min-w-0 flex-wrap items-center gap-2 sm:justify-end lg:flex-nowrap">
                <LanguageSwitcher compact className="shrink-0" />

                <div className="flex shrink-0 items-center gap-1 rounded-[18px] border border-border/76 bg-background/76 p-1 shadow-[0_16px_28px_-26px_rgba(16,36,71,0.18)]">
                  <Button
                    variant="ghost"
                    size="icon"
                    className="size-9 rounded-[14px] border-transparent bg-transparent shadow-none hover:border-border/64 hover:bg-secondary/78"
                    aria-label={t("topbar.openNotificationsSettings")}
                    asChild
                  >
                    <Link to="/settings?section=notifications">
                      <BellDot />
                    </Link>
                  </Button>
                  <ThemeToggle
                    variant="ghost"
                    className="size-9 rounded-[14px] border-transparent bg-transparent shadow-none hover:border-border/64 hover:bg-secondary/78"
                  />
                </div>

                <div className="flex min-w-0 items-center gap-1.5 rounded-[18px] border border-border/76 bg-background/76 py-1 pl-1.5 pr-1 shadow-[0_16px_28px_-26px_rgba(16,36,71,0.18)]">
                  <Avatar className="h-8 w-8">
                    <AvatarFallback>{initials || "RF"}</AvatarFallback>
                  </Avatar>
                  <div className="hidden min-w-0 xl:block">
                    <div className="truncate text-sm font-medium text-foreground">{session?.user.fullName ?? t("common.labels.rootflowUser")}</div>
                  </div>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-8 shrink-0 rounded-[12px] px-2.5 text-muted-foreground hover:text-foreground"
                    onClick={logout}
                  >
                    <LogOut className="size-4" />
                    <span className="hidden 2xl:inline">{t("common.actions.signOut")}</span>
                  </Button>
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
