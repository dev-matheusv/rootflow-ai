import { BellDot, Command, PanelLeftOpen, Search, ShieldCheck, Sparkles } from "lucide-react";
import { useMatches } from "react-router-dom";

import { RootFlowLogo } from "@/components/branding/rootflow-logo";
import { ApiBaseUrlIndicator } from "@/components/diagnostics/api-base-url-indicator";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ThemeToggle } from "@/components/ui/theme-toggle";

interface RouteHandle {
  title?: string;
  subtitle?: string;
}

export function Topbar() {
  const matches = useMatches();
  const handle = [...matches]
    .reverse()
    .map((match) => match.handle as RouteHandle | undefined)
    .find(Boolean);

  return (
    <header className="sticky top-0 z-20 border-b border-border/70 bg-background/75 backdrop-blur-xl">
      <div className="flex flex-col gap-4 px-4 py-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between gap-4 lg:hidden">
          <RootFlowLogo compact />
          <div className="flex items-center gap-2">
            <ThemeToggle />
            <Button variant="outline" size="icon" aria-label="Navigation">
              <PanelLeftOpen />
            </Button>
          </div>
        </div>

        <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
          <div className="space-y-2">
            <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.24em] text-primary/80">
              <Sparkles className="size-3.5" />
              RootFlow Product Workspace
            </div>
            <div>
              <h2 className="font-display text-2xl tracking-[-0.04em] text-foreground">{handle?.title ?? "RootFlow"}</h2>
              <p className="text-sm text-muted-foreground">{handle?.subtitle ?? "Modern AI operations for business knowledge."}</p>
            </div>
            <ApiBaseUrlIndicator />
          </div>

          <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
            <div className="relative min-w-[260px] max-w-[360px] flex-1">
              <Search className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                aria-label="Search"
                className="h-11 pl-11 pr-20"
                placeholder="Search knowledge, sources, sessions..."
                readOnly
              />
              <div className="pointer-events-none absolute right-3 top-1/2 hidden -translate-y-1/2 items-center gap-1 rounded-full border border-border/70 bg-background/90 px-2 py-1 text-[11px] text-muted-foreground sm:flex">
                <Command className="size-3" />
                K
              </div>
            </div>

            <div className="flex items-center gap-2">
              <Badge variant="success" className="hidden sm:inline-flex">
                <ShieldCheck className="size-3.5" />
                API-ready shell
              </Badge>
              <Button variant="outline" size="icon" aria-label="Notifications">
                <BellDot />
              </Button>
              <ThemeToggle />
            </div>
          </div>
        </div>
      </div>
    </header>
  );
}
