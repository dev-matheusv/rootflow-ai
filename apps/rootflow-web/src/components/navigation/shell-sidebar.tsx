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
            "border-b border-sidebar-border/70 pb-4",
            collapsed ? "px-1" : "px-2",
          )}
        >
          {collapsed ? (
            <div className="flex items-center justify-center">
              <div className="flex h-11 w-11 items-center justify-center rounded-[18px] bg-background/66">
                <RootFlowBrand variant="icon" size="sm" className="h-8 w-8" />
              </div>
            </div>
          ) : (
            <div className="space-y-3">
              <div className="flex h-10 items-center">
                <RootFlowBrand variant="logo" size="sm" className="h-7.5" />
              </div>
              <div className="space-y-1 px-1">
                <div className="flex items-center gap-2">
                  <div className="truncate text-sm font-semibold tracking-[-0.02em] text-foreground">{workspaceName}</div>
                  <Badge variant="secondary" className="shrink-0 px-2 py-0.5 text-[10px] font-semibold">
                    {role}
                  </Badge>
                </div>
                <div className="truncate text-xs text-muted-foreground">@{workspaceSlug}</div>
              </div>
            </div>
          )}
        </div>
      ) : null}

      <div className={cn("flex-1", showBrand ? "mt-5" : "mt-2")}>
        <SidebarNav collapsed={collapsed} onNavigate={onNavigate} />
      </div>

      <div
        className={cn(
          "mt-5 rounded-[20px] border border-sidebar-border/70 bg-background/62",
          collapsed ? "p-2.5" : "p-3",
        )}
      >
        {collapsed ? (
          <div className="flex justify-center">
            <div className="flex h-12 w-12 items-center justify-center rounded-[18px] bg-background/84">
              <Avatar className="size-9">
                <AvatarFallback>{initials || "RF"}</AvatarFallback>
              </Avatar>
            </div>
          </div>
        ) : (
          <div className="flex items-center gap-3">
            <Avatar className="size-10">
              <AvatarFallback>{initials || "RF"}</AvatarFallback>
            </Avatar>
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold text-foreground">{session?.user.fullName ?? "RootFlow User"}</div>
              <div className="truncate text-xs text-muted-foreground">{session?.user.email ?? "workspace@rootflow.local"}</div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
