import {
  Bot,
  LayoutDashboard,
  LockKeyhole,
  MessagesSquare,
  ShieldCheck,
  Sparkles,
} from "lucide-react";
import { NavLink } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

const primaryItems = [
  {
    to: "/dashboard",
    icon: LayoutDashboard,
    label: "Dashboard",
    caption: "Health, activity, and product overview",
  },
  {
    to: "/knowledge-base",
    icon: Sparkles,
    label: "Knowledge Base",
    caption: "Documents, ingestion, and readiness",
  },
  {
    to: "/assistant",
    icon: Bot,
    label: "Assistant",
    caption: "Grounded answers and source context",
  },
  {
    to: "/conversations",
    icon: MessagesSquare,
    label: "Conversations",
    caption: "Session history and answer trails",
  },
] as const;

const futureItems = [
  {
    to: "/auth/login",
    icon: LockKeyhole,
    label: "Authentication",
    caption: "Prepared for future login flows",
  },
  {
    to: "/auth/invite",
    icon: ShieldCheck,
    label: "Invites",
    caption: "Client onboarding and access later",
  },
] as const;

export function SidebarNav() {
  return (
    <div className="space-y-8">
      <section className="space-y-3">
        <div className="px-2 text-xs font-semibold uppercase tracking-[0.24em] text-muted-foreground">Workspace</div>
        <nav className="space-y-2">
          {primaryItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                cn(
                  "group flex items-start gap-3 rounded-[22px] border border-transparent px-3 py-3.5 transition-[transform,background-color,border-color,box-shadow,color] duration-200 hover:-translate-y-[1px] active:translate-y-0",
                  isActive
                    ? "border-sidebar-border bg-sidebar-accent text-sidebar-accent-foreground shadow-[0_22px_44px_-30px_rgba(20,89,208,0.4)]"
                    : "text-sidebar-foreground/84 hover:border-sidebar-border/80 hover:bg-sidebar-accent/82 hover:text-sidebar-foreground hover:shadow-[0_18px_38px_-30px_rgba(19,67,160,0.28)]",
                )
              }
            >
              {({ isActive }) => {
                const Icon = item.icon;

                return (
                  <>
                    <div
                      className={cn(
                        "mt-0.5 flex size-10 shrink-0 items-center justify-center rounded-2xl border transition-[border-color,background-color,color,transform] duration-200",
                        isActive
                          ? "border-primary/18 bg-primary/12 text-primary"
                          : "border-sidebar-border/70 bg-background/50 text-muted-foreground group-hover:border-primary/16 group-hover:bg-background/78 group-hover:text-primary",
                      )}
                    >
                      <Icon className="size-[18px]" />
                    </div>
                    <div className="min-w-0 space-y-1">
                      <div className="font-medium tracking-[-0.01em]">{item.label}</div>
                      <p className="text-sm leading-6 text-muted-foreground">{item.caption}</p>
                    </div>
                  </>
                );
              }}
            </NavLink>
          ))}
        </nav>
      </section>

      <section className="space-y-3">
        <div className="flex items-center justify-between px-2">
          <div className="text-xs font-semibold uppercase tracking-[0.24em] text-muted-foreground">Prepared Next</div>
          <Badge variant="secondary">Later</Badge>
        </div>
        <div className="space-y-2">
          {futureItems.map((item) => {
            const Icon = item.icon;

            return (
              <NavLink
                key={item.to}
                to={item.to}
                className="group flex items-start gap-3 rounded-[22px] border border-transparent px-3 py-3.5 text-sidebar-foreground/76 transition-[transform,background-color,border-color,box-shadow,color] duration-200 hover:-translate-y-[1px] hover:border-sidebar-border/80 hover:bg-sidebar-accent/72 hover:text-sidebar-foreground hover:shadow-[0_18px_38px_-30px_rgba(19,67,160,0.24)] active:translate-y-0"
              >
                <div className="mt-0.5 flex size-10 shrink-0 items-center justify-center rounded-2xl border border-sidebar-border/70 bg-background/40 text-muted-foreground transition-[border-color,background-color,color] duration-200 group-hover:border-primary/16 group-hover:bg-background/76 group-hover:text-primary">
                  <Icon className="size-[18px]" />
                </div>
                <div className="min-w-0 space-y-1">
                  <div className="font-medium tracking-[-0.01em]">{item.label}</div>
                  <p className="text-sm leading-6 text-muted-foreground">{item.caption}</p>
                </div>
              </NavLink>
            );
          })}
        </div>
      </section>
    </div>
  );
}
