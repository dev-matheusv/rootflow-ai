import { BellDot, PanelLeftClose, PanelLeftOpen, Search, ShieldCheck } from "lucide-react";
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
        <div className="rounded-[28px] border border-border/75 bg-card/82 px-4 py-3 shadow-[0_24px_60px_-42px_rgba(16,36,71,0.16)] backdrop-blur-2xl sm:px-5">
          <div className="flex flex-col gap-3 xl:flex-row xl:items-center xl:justify-between">
            <div className="flex items-center gap-3">
              <div className="flex items-center gap-2 rounded-[22px] border border-border/72 bg-background/76 p-1.5 shadow-[0_18px_40px_-34px_rgba(16,36,71,0.16)] backdrop-blur-xl">
                <Button
                  variant="ghost"
                  size="icon"
                  className="rounded-[16px] bg-card/82 text-foreground shadow-[0_12px_26px_-20px_rgba(16,36,71,0.26)] hover:bg-card"
                  aria-label={isDesktop ? (isSidebarCollapsed ? "Expand navigation" : "Collapse navigation") : "Open navigation"}
                  onClick={isDesktop ? onToggleSidebar : onOpenNavigation}
                >
                  {isDesktop ? isSidebarCollapsed ? <PanelLeftOpen /> : <PanelLeftClose /> : <PanelLeftOpen />}
                </Button>

                {isDesktop ? (
                  <RootFlowBrand variant="logo" size="sm" className="h-9 pr-1" />
                ) : (
                  <div className="relative flex h-11 w-11 items-center justify-center">
                    <div className="absolute inset-0 rounded-[18px] bg-[radial-gradient(circle,_rgba(15,99,236,0.16),rgba(255,255,255,0)_74%)]" />
                    <RootFlowBrand variant="icon" size="md" className="relative h-[2.35rem] w-[2.35rem]" />
                  </div>
                )}
              </div>

              <div className="hidden min-w-0 items-center gap-3 lg:flex">
                <Badge variant="success">
                  <ShieldCheck className="size-3.5" />
                  {session?.role ?? "Member"}
                </Badge>
                <div className="text-sm text-muted-foreground">
                  <span className="font-semibold text-foreground">{session?.workspace.name}</span>
                  <span className="mx-1.5 text-border">/</span>
                  <span>@{session?.workspace.slug}</span>
                </div>
              </div>
            </div>

            <div className="flex flex-wrap items-center gap-2 sm:justify-end">
              <Button variant="outline" className="min-w-[220px] flex-1 justify-between px-4 text-left sm:flex-none" asChild>
                <Link to="/assistant">
                  <span className="flex min-w-0 items-center gap-3">
                    <Search className="size-4 shrink-0 text-muted-foreground" />
                    <span className="truncate text-muted-foreground">Ask RootFlow or continue a session</span>
                  </span>
                  <span className="hidden text-[11px] font-semibold uppercase tracking-[0.18em] text-primary/72 sm:inline">
                    Assistant
                  </span>
                </Link>
              </Button>

              <div className="flex items-center gap-2">
                <Button variant="outline" size="icon" aria-label="Open notifications settings" asChild>
                  <Link to="/settings?section=notifications">
                    <BellDot />
                  </Link>
                </Button>
                <ThemeToggle />
                <div className="flex items-center gap-3 rounded-[20px] border border-border/75 bg-background/76 px-3 py-2">
                  <Avatar className="size-9">
                    <AvatarFallback>{initials || "RF"}</AvatarFallback>
                  </Avatar>
                  <div className="hidden min-w-0 sm:block">
                    <div className="truncate text-sm font-semibold text-foreground">{session?.user.fullName ?? "RootFlow User"}</div>
                    <div className="truncate text-xs text-muted-foreground">{session?.user.email ?? "workspace@rootflow.local"}</div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div className="mt-3 flex flex-wrap items-center justify-between gap-3 border-t border-border/70 pt-3">
            <ApiBaseUrlIndicator />
            <Button variant="ghost" className="px-3 text-sm text-muted-foreground hover:text-foreground" onClick={logout}>
              Sign out
            </Button>
          </div>
        </div>
      </div>
    </header>
  );
}
