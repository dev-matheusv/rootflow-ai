import * as React from "react";

import { cn } from "@/lib/utils";

const Textarea = React.forwardRef<HTMLTextAreaElement, React.ComponentProps<"textarea">>(
  ({ className, ...props }, ref) => (
    <textarea
      ref={ref}
      className={cn(
        "block min-h-[120px] w-full rounded-[24px] border border-border/75 bg-input px-4 py-3 text-[0.95rem] leading-7 text-foreground shadow-[inset_0_1px_0_rgba(255,255,255,0.28),0_14px_30px_-26px_rgba(14,31,65,0.14)] outline-none transition-[border-color,background-color,box-shadow,color] duration-200 placeholder:text-muted-foreground/90 hover:border-primary/18 hover:bg-background/94 focus-visible:border-primary/26 focus-visible:bg-background focus-visible:ring-4 focus-visible:ring-ring/16 disabled:cursor-not-allowed disabled:opacity-60 dark:shadow-[inset_0_1px_0_rgba(255,255,255,0.04),0_14px_28px_-24px_rgba(0,0,0,0.28)]",
        className,
      )}
      {...props}
    />
  ),
);
Textarea.displayName = "Textarea";

export { Textarea };
