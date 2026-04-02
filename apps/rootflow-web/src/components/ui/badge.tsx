import { cva, type VariantProps } from "class-variance-authority";
import type * as React from "react";

import { cn } from "@/lib/utils";

const badgeVariants = cva(
  "inline-flex items-center gap-1 rounded-full border px-2.5 py-1 text-[11px] font-semibold tracking-[0.08em] uppercase",
  {
    variants: {
      variant: {
        default: "border-primary/24 bg-primary/[0.11] text-primary shadow-[0_8px_18px_-16px_rgba(37,99,235,0.34)]",
        secondary: "border-border/80 bg-secondary/82 text-foreground/88",
        success: "border-emerald-500/24 bg-emerald-500/[0.12] text-emerald-700 dark:text-emerald-300",
        warning: "border-amber-500/24 bg-amber-500/[0.12] text-amber-700 dark:text-amber-300",
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
