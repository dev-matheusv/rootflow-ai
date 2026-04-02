import { Building2 } from "lucide-react";

import { RootFlowBrand } from "@/components/branding/rootflow-brand";
import { SidebarNav } from "@/components/navigation/sidebar-nav";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { useAuth } from "@/features/auth/auth-provider";
import { cn } from "@/lib/utils";

interface ShellSidebarProps {
  collapsed?: boolean;
  onNavigate?: () => void;
  showBrand?: boolean;
}

export function ShellSidebar({ collapsed = false, onNavigate, showBrand = true }: ShellSidebarProps) {
  const { session } = useAuth();
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
        <div
          className={cn(
            "flex items-center border-b border-sidebar-border/70 pb-4",
            collapsed ? "justify-center px-1" : "justify-start px-2",
          )}
        >
          {collapsed ? (
            <div className="flex h-11 w-11 items-center justify-center">
              <RootFlowBrand variant="icon" size="sm" className="h-8 w-8" />
            </div>
          ) : (
            <div className="flex h-11 items-center">
              <RootFlowBrand variant="logo" size="sm" className="h-8" />
            </div>
          )}
        </div>
      ) : null}

      <div className={cn("flex-1", showBrand ? "mt-6" : "mt-2")}>
        <SidebarNav collapsed={collapsed} onNavigate={onNavigate} />
      </div>

      <div
        className={cn(
          "mt-6 rounded-[22px] border border-sidebar-border/75 bg-background/70",
          collapsed ? "p-2.5" : "p-3.5",
        )}
      >
        {collapsed ? (
          <div className="flex justify-center">
            <div className="flex h-14 w-14 items-center justify-center rounded-[20px] bg-background/88">
              <Avatar className="size-10">
                <AvatarFallback>{initials || "RF"}</AvatarFallback>
              </Avatar>
            </div>
          </div>
        ) : (
          <>
            <div className="flex items-center gap-3">
              <div className="flex size-10 items-center justify-center rounded-[16px] bg-primary/10 text-primary">
                <Building2 className="size-5" />
              </div>
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <div className="truncate text-sm font-semibold text-foreground">{workspaceName}</div>
                  <Badge variant="secondary" className="shrink-0 px-2 py-0.5 text-[10px] font-semibold">
                    {role}
                  </Badge>
                </div>
                <div className="truncate text-xs text-muted-foreground">@{workspaceSlug}</div>
              </div>
            </div>

            <div className="mt-3 flex items-center gap-3 border-t border-border/65 pt-3">
              <Avatar className="size-11">
                <AvatarFallback>{initials || "RF"}</AvatarFallback>
              </Avatar>
              <div className="min-w-0">
                <div className="truncate text-sm font-semibold text-foreground">{session?.user.fullName ?? "RootFlow User"}</div>
                <div className="truncate text-xs text-muted-foreground">{session?.user.email ?? "workspace@rootflow.local"}</div>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
