import type { ReactNode } from "react";

import { cn } from "@/lib/utils";

interface PageHeaderProps {
  eyebrow?: string;
  title: string;
  description?: string;
  actions?: ReactNode;
  className?: string;
}

export function PageHeader({ eyebrow, title, description, actions, className }: PageHeaderProps) {
  return (
    <div className={cn("flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between", className)}>
      <div className="max-w-3xl space-y-2">
        {eyebrow ? <p className="text-[11px] font-semibold uppercase tracking-[0.24em] text-primary/74">{eyebrow}</p> : null}
        <div className="space-y-1.5">
          <h1 className="font-display text-[1.55rem] leading-none tracking-[-0.05em] text-foreground sm:text-[2rem]">{title}</h1>
          {description ? <p className="max-w-2xl text-sm leading-6 text-muted-foreground">{description}</p> : null}
        </div>
      </div>
      {actions ? <div className="flex flex-wrap items-center gap-3 lg:justify-end">{actions}</div> : null}
    </div>
  );
}
