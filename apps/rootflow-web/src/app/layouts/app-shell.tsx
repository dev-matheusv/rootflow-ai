import { ArrowUpRight, Building2, CircleCheckBig, Orbit } from "lucide-react";
import { Outlet } from "react-router-dom";

import { RootFlowLogo } from "@/components/branding/rootflow-logo";
import { SidebarNav } from "@/components/navigation/sidebar-nav";
import { Topbar } from "@/components/navigation/topbar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export function AppShell() {
  return (
    <div className="relative min-h-screen overflow-hidden bg-background text-foreground">
      <div className="pointer-events-none absolute inset-0 opacity-[0.9]">
        <div className="absolute left-[-12%] top-[-18%] h-[34rem] w-[34rem] rounded-full bg-[radial-gradient(circle,_rgba(84,151,255,0.26),transparent_65%)] blur-3xl" />
        <div className="absolute right-[-10%] top-[12%] h-[30rem] w-[30rem] rounded-full bg-[radial-gradient(circle,_rgba(113,203,255,0.22),transparent_62%)] blur-3xl" />
        <div className="absolute bottom-[-22%] left-[25%] h-[28rem] w-[28rem] rounded-full bg-[radial-gradient(circle,_rgba(18,101,255,0.18),transparent_65%)] blur-3xl" />
      </div>

      <div className="relative mx-auto grid min-h-screen max-w-[1600px] lg:grid-cols-[292px_1fr]">
        <aside className="hidden border-r border-sidebar-border/70 bg-sidebar/82 px-5 py-6 backdrop-blur-2xl lg:flex lg:flex-col">
          <div className="flex items-center justify-between">
            <RootFlowLogo />
            <Badge variant="secondary">Beta</Badge>
          </div>

          <div className="mt-8 flex-1">
            <SidebarNav />
          </div>

          <Card className="border-sidebar-border/80 bg-background/55 shadow-none">
            <CardHeader className="pb-4">
              <Badge className="w-fit">
                <Orbit className="size-3.5" />
                Default workspace
              </Badge>
              <CardTitle className="text-base">RootFlow Core Demo</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="rounded-2xl border border-border/70 bg-background/70 p-4">
                <div className="flex items-center justify-between">
                  <div className="space-y-1">
                    <div className="text-sm font-medium text-foreground">Readiness</div>
                    <div className="text-sm text-muted-foreground">Frontend shell prepared for API integration</div>
                  </div>
                  <CircleCheckBig className="size-4 text-emerald-500" />
                </div>
              </div>

              <div className="flex items-center gap-3 rounded-2xl border border-border/70 bg-background/70 p-4">
                <div className="flex size-11 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                  <Building2 className="size-5" />
                </div>
                <div className="min-w-0">
                  <div className="truncate text-sm font-semibold text-foreground">Client-ready foundation</div>
                  <div className="text-sm text-muted-foreground">Premium SaaS shell, dark mode, scalable routing.</div>
                </div>
              </div>

              <Button variant="outline" className="w-full justify-between">
                Product settings
                <ArrowUpRight />
              </Button>
            </CardContent>
          </Card>
        </aside>

        <div className="flex min-h-screen flex-col">
          <Topbar />
          <main className="flex-1 px-4 py-5 sm:px-6 sm:py-6 lg:px-8 lg:py-8">
            <div className="mx-auto max-w-[1280px] space-y-6">
              <Outlet />
            </div>
          </main>
        </div>
      </div>
    </div>
  );
}
