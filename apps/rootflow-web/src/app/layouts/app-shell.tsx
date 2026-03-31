import { ArrowUpRight, Building2, CircleCheckBig, Orbit } from "lucide-react";
import { Link } from "react-router-dom";
import { Outlet } from "react-router-dom";

import { RootFlowLogo } from "@/components/branding/rootflow-logo";
import { SidebarNav } from "@/components/navigation/sidebar-nav";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Topbar } from "@/components/navigation/topbar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useAuth } from "@/features/auth/auth-provider";

export function AppShell() {
  const { session } = useAuth();
  const workspaceName = session?.workspace.name ?? "Workspace";
  const workspaceSlug = session?.workspace.slug ?? "workspace";
  const role = session?.role ?? "Member";
  const initials = session?.user.fullName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");

  return (
    <div className="relative min-h-screen overflow-hidden bg-background text-foreground">
      <div className="pointer-events-none absolute inset-0 opacity-[0.85]">
        <div className="absolute left-[-12%] top-[-18%] h-[34rem] w-[34rem] rounded-full bg-[radial-gradient(circle,_rgba(116,160,255,0.18),transparent_65%)] blur-3xl" />
        <div className="absolute right-[-10%] top-[12%] h-[28rem] w-[28rem] rounded-full bg-[radial-gradient(circle,_rgba(198,223,255,0.34),transparent_60%)] blur-3xl dark:bg-[radial-gradient(circle,_rgba(54,74,110,0.26),transparent_60%)]" />
        <div className="absolute bottom-[-22%] left-[25%] h-[26rem] w-[26rem] rounded-full bg-[radial-gradient(circle,_rgba(79,121,212,0.12),transparent_65%)] blur-3xl dark:bg-[radial-gradient(circle,_rgba(43,62,94,0.2),transparent_65%)]" />
      </div>

      <div className="relative mx-auto grid min-h-screen max-w-[1600px] lg:grid-cols-[292px_1fr]">
        <aside className="hidden border-r border-sidebar-border/80 bg-sidebar/90 px-5 py-6 shadow-[16px_0_48px_-44px_rgba(15,37,79,0.22)] backdrop-blur-2xl lg:flex lg:flex-col dark:shadow-none">
          <div className="flex items-center justify-between">
            <RootFlowLogo />
            <Badge variant="secondary">Beta</Badge>
          </div>

          <div className="mt-8 flex-1">
            <SidebarNav />
          </div>

          <Card className="border-sidebar-border/80 bg-background/78 shadow-[0_18px_40px_-34px_rgba(17,43,88,0.14)] dark:shadow-none">
            <CardHeader className="pb-4">
              <Badge className="w-fit">
                <Orbit className="size-3.5" />
                {role} workspace
              </Badge>
              <CardTitle className="text-base">{workspaceName}</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="rounded-2xl border border-border/70 bg-background/88 p-4">
                <div className="flex items-center justify-between">
                  <div className="space-y-1">
                    <div className="text-sm font-medium text-foreground">Readiness</div>
                    <div className="text-sm text-muted-foreground">Authenticated workspace with isolated documents and chat.</div>
                  </div>
                  <CircleCheckBig className="size-4 text-emerald-500" />
                </div>
              </div>

              <div className="flex items-center gap-3 rounded-2xl border border-border/70 bg-background/88 p-4">
                <div className="flex size-11 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                  <Building2 className="size-5" />
                </div>
                <div className="min-w-0">
                  <div className="truncate text-sm font-semibold text-foreground">@{workspaceSlug}</div>
                  <div className="text-sm text-muted-foreground">JWT-scoped SaaS foundation with room for multi-user growth.</div>
                </div>
              </div>

              <div className="flex items-center gap-3 rounded-2xl border border-border/70 bg-background/88 p-4">
                <Avatar className="size-11">
                  <AvatarFallback>{initials || "RF"}</AvatarFallback>
                </Avatar>
                <div className="min-w-0">
                  <div className="truncate text-sm font-semibold text-foreground">{session?.user.fullName ?? "RootFlow User"}</div>
                  <div className="truncate text-sm text-muted-foreground">{session?.user.email ?? "workspace@rootflow.local"}</div>
                </div>
              </div>

              <Button variant="outline" className="w-full justify-between" asChild>
                <Link to="/settings">
                  Product settings
                  <ArrowUpRight />
                </Link>
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
