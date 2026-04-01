import { cva, type VariantProps } from "class-variance-authority";
import type * as React from "react";

import { cn } from "@/lib/utils";

const badgeVariants = cva(
  "inline-flex items-center gap-1 rounded-full border px-2.5 py-1 text-[11px] font-semibold tracking-[0.08em] uppercase",
  {
    variants: {
      variant: {
        default: "border-primary/16 bg-primary/[0.08] text-primary",
        secondary: "border-border/75 bg-secondary/70 text-secondary-foreground",
        success: "border-emerald-500/16 bg-emerald-500/[0.08] text-emerald-600 dark:text-emerald-300",
        warning: "border-amber-500/18 bg-amber-500/[0.08] text-amber-600 dark:text-amber-300",
      },
    },
    defaultVariants: {
      variant: "default",
    },
  },
);

export interface BadgeProps extends React.HTMLAttributes<HTMLDivElement>, VariantProps<typeof badgeVariants> {}

function Badge({ className, variant, ...props }: BadgeProps) {
  return <div className={cn(badgeVariants({ variant }), className)} {...props} />;
}

export { Badge };
