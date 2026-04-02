import type { ReactNode } from "react";

import { cn } from "@/lib/utils";

interface PageHeaderProps {
  eyebrow?: string;
  title: string;
  description: string;
  actions?: ReactNode;
  className?: string;
}

export function PageHeader({ eyebrow, title, description, actions, className }: PageHeaderProps) {
  return (
    <div className={cn("flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between", className)}>
      <div className="max-w-3xl space-y-3">
        {eyebrow ? <p className="text-[11px] font-semibold uppercase tracking-[0.28em] text-primary/74">{eyebrow}</p> : null}
        <div className="space-y-2">
          <h1 className="font-display text-[2rem] leading-none tracking-[-0.06em] text-foreground sm:text-[2.8rem]">{title}</h1>
          <p className="max-w-2xl text-sm leading-6 text-muted-foreground sm:text-[1.01rem]">{description}</p>
        </div>
      </div>
      {actions ? <div className="flex flex-wrap items-center gap-3 lg:justify-end">{actions}</div> : null}
    </div>
  );
}
