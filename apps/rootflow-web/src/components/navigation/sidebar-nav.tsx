import { NavLink } from "react-router-dom";

import { navigationItems } from "@/components/navigation/navigation-items";
import { cn } from "@/lib/utils";

interface SidebarNavProps {
  collapsed?: boolean;
  onNavigate?: () => void;
}

export function SidebarNav({ collapsed = false, onNavigate }: SidebarNavProps) {
  return (
    <section>
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
                collapsed ? "justify-center px-0 py-1.5" : "items-center gap-3 px-3 py-2.5",
                isActive
                  ? "border-primary/24 bg-primary/[0.09] text-sidebar-accent-foreground shadow-[0_14px_28px_-22px_rgba(18,72,166,0.18)]"
                  : "text-sidebar-foreground/82 hover:border-sidebar-border/80 hover:bg-sidebar-accent/78 hover:text-sidebar-foreground",
              )
            }
          >
            {({ isActive }) => {
              const Icon = item.icon;

              return (
                <>
                  <div
                    className={cn(
                      "absolute rounded-full transition-[background-color,opacity] duration-200",
                      collapsed
                        ? "bottom-1 left-1/2 h-1 w-8 -translate-x-1/2"
                        : "left-1 top-1/2 h-8 w-1 -translate-y-1/2",
                      isActive ? "bg-primary opacity-100" : "bg-transparent opacity-0 group-hover:opacity-48",
                    )}
                  />
                  <div
                    className={cn(
                      "flex size-10 shrink-0 items-center justify-center rounded-[16px] border transition-[border-color,background-color,color] duration-200",
                      isActive
                        ? "border-primary/22 bg-primary/[0.12] text-primary"
                        : "border-sidebar-border/85 bg-background/78 text-muted-foreground group-hover:border-primary/20 group-hover:bg-background/92 group-hover:text-primary",
                    )}
                  >
                    <Icon className="size-[18px]" />
                  </div>
                  {collapsed ? (
                    <span className="sr-only">{item.label}</span>
                  ) : (
                    <div className="min-w-0">
                      <div className="font-medium tracking-[-0.02em]">{item.label}</div>
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
