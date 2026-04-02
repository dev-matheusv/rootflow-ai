import { BellDot, LogOut, PanelLeftClose, PanelLeftOpen, Search } from "lucide-react";
import { Link } from "react-router-dom";

import { RootFlowBrand } from "@/components/branding/rootflow-brand";
import { ApiBaseUrlIndicator } from "@/components/diagnostics/api-base-url-indicator";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
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

  const initials = session?.user.fullName
    ?.split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");

  return (
    <header className="sticky top-0 z-30 px-4 pt-4 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-[1320px]">
        <div className="rounded-[26px] border border-border/65 bg-background/82 px-4 py-3 shadow-[0_20px_48px_-40px_rgba(16,36,71,0.14)] backdrop-blur-2xl sm:px-5">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
            <div className="flex min-w-0 items-center gap-3">
              <Button
                variant="ghost"
                size="icon"
                className="shrink-0 rounded-[16px] border border-border/65 bg-card/74 text-foreground hover:bg-card"
                aria-label={isDesktop ? (isSidebarCollapsed ? "Expand navigation" : "Collapse navigation") : "Open navigation"}
                onClick={isDesktop ? onToggleSidebar : onOpenNavigation}
              >
                {isDesktop ? isSidebarCollapsed ? <PanelLeftOpen /> : <PanelLeftClose /> : <PanelLeftOpen />}
              </Button>

              {isDesktop ? (
                <div className="hidden min-w-0 items-center gap-3 lg:flex">
                  <RootFlowBrand variant="logo" size="sm" className="h-10 shrink-0" />
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <div className="truncate text-sm font-semibold text-foreground">{session?.workspace.name ?? "Workspace"}</div>
                      <Badge variant="secondary" className="shrink-0 px-2 py-0.5 text-[10px] font-semibold">
                        {session?.role ?? "Member"}
                      </Badge>
                    </div>
                    <div className="truncate text-xs text-muted-foreground">@{session?.workspace.slug ?? "workspace"}</div>
                  </div>
                </div>
              ) : (
                <div className="flex h-11 w-11 items-center justify-center">
                  <RootFlowBrand variant="icon" size="md" className="h-[2.3rem] w-[2.3rem]" />
                </div>
              )}
            </div>

            <div className="flex flex-wrap items-center gap-2 sm:justify-end">
              <Button variant="outline" className="min-w-[172px] flex-1 justify-start gap-3 px-4 text-left sm:flex-none" asChild>
                <Link to="/assistant">
                  <Search className="size-4 shrink-0 text-muted-foreground" />
                  <span className="truncate text-foreground">Ask RootFlow</span>
                </Link>
              </Button>

              <div className="flex items-center gap-1.5">
                <Button variant="outline" size="icon" aria-label="Open notifications settings" asChild>
                  <Link to="/settings?section=notifications">
                    <BellDot />
                  </Link>
                </Button>
                <ThemeToggle />
                <Button variant="ghost" className="gap-2 px-3 text-muted-foreground hover:text-foreground" onClick={logout}>
                  <LogOut className="size-4" />
                  <span className="hidden xl:inline">Sign out</span>
                </Button>
                <div className="flex items-center gap-2.5 rounded-[18px] border border-border/70 bg-background/76 px-3 py-2">
                  <Avatar className="h-[2.125rem] w-[2.125rem]">
                    <AvatarFallback>{initials || "RF"}</AvatarFallback>
                  </Avatar>
                  <div className="hidden min-w-0 lg:block">
                    <div className="truncate text-sm font-medium text-foreground">{session?.user.fullName ?? "RootFlow User"}</div>
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
