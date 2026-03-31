import { BellDot, LogOut, PanelLeftOpen, Search, ShieldCheck, Sparkles, X } from "lucide-react";
import { useEffect, useState } from "react";
import { Link, useLocation, useMatches } from "react-router-dom";

import { RootFlowLogo } from "@/components/branding/rootflow-logo";
import { ApiBaseUrlIndicator } from "@/components/diagnostics/api-base-url-indicator";
import { SidebarNav } from "@/components/navigation/sidebar-nav";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { ThemeToggle } from "@/components/ui/theme-toggle";
import { useAuth } from "@/features/auth/auth-provider";

interface RouteHandle {
  title?: string;
  subtitle?: string;
}

export function Topbar() {
  const location = useLocation();
  const matches = useMatches();
  const { logout, session } = useAuth();
  const [isMobileNavOpen, setIsMobileNavOpen] = useState(false);
  const handle = [...matches]
    .reverse()
    .map((match) => match.handle as RouteHandle | undefined)
    .find(Boolean);

  useEffect(() => {
    setIsMobileNavOpen(false);
  }, [location.pathname, location.search]);

  const initials = session?.user.fullName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");

  return (
    <>
      <header className="sticky top-0 z-20 border-b border-border/80 bg-background/82 backdrop-blur-xl">
        <div className="flex flex-col gap-4 px-4 py-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between gap-4 lg:hidden">
            <RootFlowLogo compact />
            <div className="flex items-center gap-2">
              <Avatar className="size-9">
                <AvatarFallback>{initials || "RF"}</AvatarFallback>
              </Avatar>
              <ThemeToggle />
              <Button variant="outline" size="icon" aria-label="Open navigation" onClick={() => setIsMobileNavOpen(true)}>
                <PanelLeftOpen />
              </Button>
            </div>
          </div>

          <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.24em] text-primary/75">
                <Sparkles className="size-3.5" />
                Authenticated RootFlow Workspace
              </div>
              <div>
                <h2 className="font-display text-[1.95rem] tracking-[-0.05em] text-foreground">{handle?.title ?? "RootFlow"}</h2>
                <p className="text-sm leading-6 text-muted-foreground">{handle?.subtitle ?? "Modern AI operations for business knowledge."}</p>
              </div>
              <ApiBaseUrlIndicator />
            </div>

            <div className="flex flex-col gap-3 xl:min-w-[540px] sm:flex-row sm:items-center sm:justify-end">
              <Button
                variant="outline"
                className="h-11 min-w-[260px] max-w-[420px] flex-1 justify-between px-4 text-left text-muted-foreground"
                asChild
              >
                <Link to="/assistant">
                  <span className="flex min-w-0 items-center gap-3">
                    <Search className="size-4 shrink-0 text-muted-foreground" />
                    <span className="truncate">Ask RootFlow, review sources, or continue a conversation</span>
                  </span>
                  <span className="text-xs font-semibold uppercase tracking-[0.16em] text-primary/72">Assistant</span>
                </Link>
              </Button>

              <div className="flex items-center gap-2">
                <Badge variant="success" className="hidden sm:inline-flex">
                  <ShieldCheck className="size-3.5" />
                  {session?.role ?? "Member"}
                </Badge>
                <div className="hidden min-w-0 items-center gap-3 rounded-2xl border border-border/75 bg-background/80 px-3 py-2 sm:flex">
                  <Avatar className="size-10">
                    <AvatarFallback>{initials || "RF"}</AvatarFallback>
                  </Avatar>
                  <div className="min-w-0">
                    <div className="truncate text-sm font-semibold text-foreground">{session?.user.fullName}</div>
                    <div className="truncate text-xs text-muted-foreground">
                      {session?.workspace.name} - @{session?.workspace.slug}
                    </div>
                  </div>
                </div>
                <Button variant="ghost" className="hidden sm:inline-flex" onClick={logout}>
                  Log out
                  <LogOut />
                </Button>
                <Button variant="outline" size="icon" aria-label="Open notifications settings" asChild>
                  <Link to="/settings?section=notifications">
                    <BellDot />
                  </Link>
                </Button>
                <ThemeToggle />
              </div>
            </div>
          </div>
        </div>
      </header>

      {isMobileNavOpen ? (
        <div className="fixed inset-0 z-40 lg:hidden">
          <button
            type="button"
            className="absolute inset-0 bg-slate-950/34 backdrop-blur-sm"
            aria-label="Close navigation"
            onClick={() => setIsMobileNavOpen(false)}
          />
          <aside className="absolute inset-y-0 right-0 flex w-full max-w-[360px] flex-col border-l border-border/80 bg-background/96 p-5 shadow-[0_22px_48px_-28px_rgba(15,37,79,0.34)]">
            <div className="flex items-center justify-between">
              <RootFlowLogo compact />
              <Button variant="outline" size="icon" aria-label="Close navigation" onClick={() => setIsMobileNavOpen(false)}>
                <X />
              </Button>
            </div>

            <div className="mt-6 flex-1 overflow-y-auto pr-1">
              <SidebarNav />
            </div>

            <div className="mt-6 space-y-3 border-t border-border/80 pt-4">
              <div className="flex items-center gap-3 rounded-2xl border border-border/75 bg-background/82 p-3">
                <Avatar className="size-11">
                  <AvatarFallback>{initials || "RF"}</AvatarFallback>
                </Avatar>
                <div className="min-w-0">
                  <div className="truncate text-sm font-semibold text-foreground">{session?.user.fullName}</div>
                  <div className="truncate text-xs text-muted-foreground">
                    {session?.workspace.name} - {session?.role}
                  </div>
                </div>
              </div>
              <Button variant="outline" className="w-full justify-between" asChild>
                <Link to="/settings">Product settings</Link>
              </Button>
              <Button variant="ghost" className="w-full justify-between" onClick={logout}>
                Log out
                <LogOut />
              </Button>
            </div>
          </aside>
        </div>
      ) : null}
    </>
  );
}
