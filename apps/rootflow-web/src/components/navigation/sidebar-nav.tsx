import { NavLink } from "react-router-dom";

import { navigationItems } from "@/components/navigation/navigation-items";
import { cn } from "@/lib/utils";

interface SidebarNavProps {
  collapsed?: boolean;
  onNavigate?: () => void;
}

export function SidebarNav({ collapsed = false, onNavigate }: SidebarNavProps) {
  return (
    <section className="space-y-3">
      {collapsed ? null : (
        <div className="px-2 text-[11px] font-semibold uppercase tracking-[0.24em] text-muted-foreground/78">
          Workspace
        </div>
      )}
      <nav className="space-y-1.5">
        {navigationItems.map((item) => (
          <NavLink
            key={item.id}
            to={item.to}
            title={collapsed ? item.label : undefined}
            onClick={onNavigate}
            className={({ isActive }) =>
              cn(
                "group relative flex rounded-[20px] border border-transparent transition-[background-color,border-color,box-shadow,color] duration-200",
                collapsed ? "justify-center px-0 py-2" : "items-start gap-3 px-3 py-3",
                isActive
                  ? "border-primary/14 bg-sidebar-accent text-sidebar-accent-foreground shadow-[0_16px_32px_-26px_rgba(18,72,166,0.16)]"
                  : "text-sidebar-foreground/76 hover:border-sidebar-border hover:bg-sidebar-accent/70 hover:text-sidebar-foreground",
              )
            }
          >
            {({ isActive }) => {
              const Icon = item.icon;

              return (
                <>
                  <div
                    className={cn(
                      "absolute left-1 top-1/2 h-8 w-1 -translate-y-1/2 rounded-full transition-[background-color,opacity] duration-200",
                      isActive ? "bg-primary opacity-100" : "bg-transparent opacity-0 group-hover:opacity-40",
                    )}
                  />
                  <div
                    className={cn(
                      "flex size-10 shrink-0 items-center justify-center rounded-[16px] border transition-[border-color,background-color,color] duration-200",
                      isActive
                        ? "border-primary/14 bg-primary/10 text-primary"
                        : "border-sidebar-border/80 bg-background/70 text-muted-foreground group-hover:border-primary/16 group-hover:bg-background group-hover:text-primary",
                    )}
                  >
                    <Icon className="size-[18px]" />
                  </div>
                  {collapsed ? (
                    <span className="sr-only">{item.label}</span>
                  ) : (
                    <div className="min-w-0 space-y-0.5">
                      <div className="font-medium tracking-[-0.02em]">{item.label}</div>
                      <p className="text-sm leading-6 text-muted-foreground">{item.caption}</p>
                    </div>
                  )}
                </>
              );
            }}
          </NavLink>
        ))}
      </nav>
    </section>
  );
}
