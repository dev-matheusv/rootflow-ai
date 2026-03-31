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
      <div className="space-y-3.5">
        {eyebrow ? <p className="text-xs font-semibold uppercase tracking-[0.28em] text-primary/75">{eyebrow}</p> : null}
        <div className="space-y-3">
          <h1 className="font-display text-3xl tracking-[-0.05em] text-foreground sm:text-[2.6rem]">{title}</h1>
          <p className="max-w-3xl text-sm leading-7 text-muted-foreground sm:text-base">{description}</p>
        </div>
      </div>
      {actions ? <div className="flex flex-wrap items-center gap-3">{actions}</div> : null}
    </div>
  );
}
