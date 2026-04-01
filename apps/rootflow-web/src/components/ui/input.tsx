import * as React from "react";

import { cn } from "@/lib/utils";

const Input = React.forwardRef<HTMLInputElement, React.ComponentProps<"input">>(({ className, ...props }, ref) => (
  <input
    ref={ref}
    className={cn(
      "block h-11 w-full rounded-2xl border border-border/75 bg-input px-4 py-2.5 text-[0.95rem] text-foreground shadow-[inset_0_1px_0_rgba(255,255,255,0.32),0_12px_26px_-24px_rgba(14,31,65,0.12)] outline-none transition-[border-color,background-color,box-shadow,color] duration-200 placeholder:text-muted-foreground/90 hover:border-primary/18 hover:bg-background/94 focus-visible:border-primary/26 focus-visible:bg-background focus-visible:ring-4 focus-visible:ring-ring/16 disabled:cursor-not-allowed disabled:opacity-60 dark:shadow-[inset_0_1px_0_rgba(255,255,255,0.04),0_14px_28px_-24px_rgba(0,0,0,0.3)]",
      className,
    )}
    {...props}
  />
));
Input.displayName = "Input";

export { Input };
