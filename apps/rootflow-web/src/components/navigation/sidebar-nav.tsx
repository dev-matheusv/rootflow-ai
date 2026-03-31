import {
  Bot,
  LayoutDashboard,
  MessagesSquare,
  Sparkles,
} from "lucide-react";
import { NavLink } from "react-router-dom";

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

export function SidebarNav() {
  return (
    <section className="space-y-3">
      <div className="px-2 text-xs font-semibold uppercase tracking-[0.24em] text-muted-foreground">Workspace</div>
      <nav className="space-y-2">
        {primaryItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            className={({ isActive }) =>
              cn(
                "group relative flex items-start gap-3 rounded-[22px] border border-transparent px-3.5 py-3.5 transition-[transform,background-color,border-color,box-shadow,color] duration-200 hover:-translate-y-[1px] active:translate-y-0",
                isActive
                  ? "border-primary/16 bg-sidebar-accent text-sidebar-accent-foreground shadow-[0_22px_44px_-34px_rgba(33,87,188,0.18)] dark:shadow-[0_18px_36px_-32px_rgba(0,0,0,0.4)]"
                  : "text-sidebar-foreground/84 hover:border-sidebar-border/90 hover:bg-sidebar-accent/76 hover:text-sidebar-foreground hover:shadow-[0_18px_38px_-34px_rgba(19,67,160,0.12)] dark:hover:shadow-[0_18px_32px_-30px_rgba(0,0,0,0.4)]",
              )
            }
          >
            {({ isActive }) => {
              const Icon = item.icon;

              return (
                <>
                  <div
                    className={cn(
                      "absolute inset-y-3 left-1 w-1 rounded-full transition-[background-color,opacity] duration-200",
                      isActive ? "bg-primary opacity-100" : "bg-transparent opacity-0 group-hover:opacity-40",
                    )}
                  />
                  <div
                    className={cn(
                      "mt-0.5 flex size-10 shrink-0 items-center justify-center rounded-2xl border transition-[border-color,background-color,color,transform] duration-200",
                      isActive
                        ? "border-primary/18 bg-primary/10 text-primary"
                        : "border-sidebar-border/70 bg-background/72 text-muted-foreground group-hover:border-primary/16 group-hover:bg-background group-hover:text-primary",
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
  );
}
