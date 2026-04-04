import { cva, type VariantProps } from "class-variance-authority";
import type * as React from "react";

import { cn } from "@/lib/utils";

const badgeVariants = cva(
  "inline-flex items-center gap-1 rounded-full border px-3 py-1 text-[10.5px] font-semibold tracking-[0.12em] uppercase transition-[background-color,border-color,color,box-shadow] duration-200",
  {
    variants: {
      variant: {
        default: "border-primary/26 bg-primary/[0.12] text-primary shadow-[0_12px_24px_-18px_rgba(37,99,235,0.34)]",
        secondary: "border-border/85 bg-background/82 text-foreground/88 shadow-[inset_0_1px_0_rgba(255,255,255,0.28)]",
        success: "border-emerald-500/24 bg-emerald-500/[0.12] text-emerald-700 shadow-[0_10px_22px_-18px_rgba(16,185,129,0.3)] dark:text-emerald-300",
        warning: "border-amber-500/24 bg-amber-500/[0.12] text-amber-700 shadow-[0_10px_22px_-18px_rgba(245,158,11,0.28)] dark:text-amber-300",
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
