import { useI18n } from "@/app/providers/i18n-provider";
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
  const { t } = useI18n();
  const workspaceName = session?.workspace.name ?? t("common.labels.workspace");
  const workspaceSlug = session?.workspace.slug ?? "workspace";
  const role = session?.role ?? "Member";
  const roleLabel =
    role === "Owner" ? t("common.labels.owner") : role === "Admin" ? t("common.labels.admin") : t("common.labels.member");
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
            "border-b border-sidebar-border/85 pb-4",
            collapsed ? "px-1" : "px-2",
          )}
        >
          {collapsed ? (
            <div className="flex items-center justify-center">
              <div className="flex h-11 w-11 items-center justify-center rounded-[18px] border border-sidebar-border/75 bg-background/72 shadow-[0_14px_28px_-24px_rgba(18,38,74,0.18)]">
                <RootFlowBrand variant="icon" size="sm" className="h-8 w-8" />
              </div>
            </div>
          ) : (
            <div className="space-y-2.5 rounded-[22px] border border-sidebar-border/80 bg-background/62 px-3 py-3 shadow-[0_18px_36px_-30px_rgba(18,38,74,0.16)]">
              <div className="flex h-9 items-center px-0.5">
                <RootFlowBrand variant="logo" size="sm" className="h-7" />
              </div>
              <div className="space-y-1 px-0.5">
                <div className="flex items-center gap-2">
                  <div className="truncate text-sm font-semibold tracking-[-0.02em] text-foreground">{workspaceName}</div>
                  <Badge variant="secondary" className="shrink-0 px-2 py-0.5 text-[10px] font-semibold">
                    {roleLabel}
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

      <div className={cn("mt-5 border-t border-sidebar-border/85 pt-4", collapsed ? "px-1" : "px-0.5")}>
        {collapsed ? (
          <div className="flex justify-center">
            <div className="flex h-11 w-11 items-center justify-center rounded-[18px] border border-sidebar-border/75 bg-background/72 shadow-[0_14px_28px_-24px_rgba(18,38,74,0.18)]">
              <Avatar className="size-9">
                <AvatarFallback>{initials || "RF"}</AvatarFallback>
              </Avatar>
            </div>
          </div>
        ) : (
          <div className="flex items-center gap-3 rounded-[22px] border border-sidebar-border/80 bg-background/62 px-3 py-3 shadow-[0_18px_34px_-30px_rgba(18,38,74,0.14)]">
            <Avatar className="size-10">
              <AvatarFallback>{initials || "RF"}</AvatarFallback>
            </Avatar>
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold text-foreground">{session?.user.fullName ?? t("common.labels.rootflowUser")}</div>
              <div className="truncate text-xs text-muted-foreground">{session?.user.email ?? "workspace@rootflow.local"}</div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
