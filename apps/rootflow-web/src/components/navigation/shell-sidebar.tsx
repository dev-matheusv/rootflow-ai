import { Building2, LogOut, Orbit } from "lucide-react";

import { RootFlowBrand } from "@/components/branding/rootflow-brand";
import { SidebarNav } from "@/components/navigation/sidebar-nav";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/features/auth/auth-provider";
import { cn } from "@/lib/utils";

interface ShellSidebarProps {
  collapsed?: boolean;
  onNavigate?: () => void;
  showBrand?: boolean;
}

export function ShellSidebar({ collapsed = false, onNavigate, showBrand = true }: ShellSidebarProps) {
  const { logout, session } = useAuth();
  const workspaceName = session?.workspace.name ?? "Workspace";
  const workspaceSlug = session?.workspace.slug ?? "workspace";
  const role = session?.role ?? "Member";
  const initials = session?.user.fullName
    ?.split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");

  return (
    <div className="flex h-full flex-col">
      {showBrand ? (
        <div className={cn("flex items-center", collapsed ? "justify-center" : "justify-between gap-3")}>
          {collapsed ? (
            <div className="relative flex h-16 w-16 items-center justify-center">
              <div className="absolute inset-0 rounded-[24px] bg-[radial-gradient(circle,_rgba(15,99,236,0.18),rgba(255,255,255,0)_72%)]" />
              <div className="absolute inset-x-3 bottom-1 h-px bg-[linear-gradient(90deg,transparent,rgba(15,99,236,0.22),transparent)]" />
              <RootFlowBrand variant="icon" size="sm" className="relative h-8 w-8" />
            </div>
          ) : (
            <>
              <RootFlowBrand variant="logo" size="sm" className="h-9" />
              <Badge variant="secondary">Beta</Badge>
            </>
          )}
        </div>
      ) : null}

      <div className={cn("flex-1", showBrand ? (collapsed ? "mt-7" : "mt-10") : "mt-2")}>
        <SidebarNav collapsed={collapsed} onNavigate={onNavigate} />
      </div>

      <div
        className={cn(
          "mt-6 rounded-[26px] border border-sidebar-border/85 bg-background/78 backdrop-blur-xl",
          collapsed ? "space-y-3 p-2.5" : "space-y-4 p-4",
        )}
      >
        {collapsed ? (
          <>
            <div className="flex justify-center">
              <div className="flex h-14 w-14 items-center justify-center rounded-[20px] bg-background/88 shadow-[0_16px_30px_-26px_rgba(16,36,71,0.24)]">
                <Avatar className="size-10">
                  <AvatarFallback>{initials || "RF"}</AvatarFallback>
                </Avatar>
              </div>
            </div>
            <Button
              variant="ghost"
              size="icon"
              className="mx-auto flex size-10 items-center justify-center rounded-[16px] text-muted-foreground hover:bg-background/86 hover:text-foreground"
              aria-label="Log out"
              onClick={logout}
            >
              <LogOut />
            </Button>
          </>
        ) : (
          <>
            <div className="flex items-center gap-3">
              <div className="flex size-11 items-center justify-center rounded-[18px] bg-primary/10 text-primary">
                <Building2 className="size-5" />
              </div>
              <div className="min-w-0">
                <div className="truncate text-sm font-semibold text-foreground">{workspaceName}</div>
                <div className="text-xs text-muted-foreground">@{workspaceSlug} - {role}</div>
              </div>
            </div>

            <div className="rounded-[20px] border border-border/70 bg-background/74 p-3">
              <div className="flex items-center gap-2 text-[11px] font-semibold uppercase tracking-[0.18em] text-primary/74">
                <Orbit className="size-3.5" />
                Ready
              </div>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">
                Auth, documents, and grounded conversations are scoped to the active workspace.
              </p>
            </div>

            <div className="flex items-center gap-3 rounded-[20px] border border-border/70 bg-background/74 p-3">
              <Avatar className="size-11">
                <AvatarFallback>{initials || "RF"}</AvatarFallback>
              </Avatar>
              <div className="min-w-0">
                <div className="truncate text-sm font-semibold text-foreground">{session?.user.fullName ?? "RootFlow User"}</div>
                <div className="truncate text-xs text-muted-foreground">{session?.user.email ?? "workspace@rootflow.local"}</div>
              </div>
            </div>

            <div className="flex flex-col gap-2">
              <Button variant="ghost" className="w-full justify-between" onClick={logout}>
                Log out
                <LogOut />
              </Button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
