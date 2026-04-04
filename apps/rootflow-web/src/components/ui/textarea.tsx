import * as React from "react";

import { cn } from "@/lib/utils";

const Textarea = React.forwardRef<HTMLTextAreaElement, React.ComponentProps<"textarea">>(
  ({ className, ...props }, ref) => (
    <textarea
      ref={ref}
      className={cn(
        "block min-h-[120px] w-full rounded-[24px] border border-border/88 bg-[linear-gradient(180deg,color-mix(in_srgb,var(--input)_98%,transparent),color-mix(in_srgb,var(--background)_72%,transparent))] px-4 py-3 text-[0.95rem] leading-7 text-foreground shadow-[inset_0_1px_0_rgba(255,255,255,0.3),0_16px_32px_-26px_rgba(14,31,65,0.16)] outline-none transition-[transform,border-color,background-color,box-shadow,color] duration-200 placeholder:text-muted-foreground/90 hover:border-primary/24 hover:bg-background/98 focus-visible:border-primary/34 focus-visible:bg-background focus-visible:ring-4 focus-visible:ring-ring/18 focus-visible:shadow-[0_18px_34px_-22px_rgba(18,72,166,0.18)] disabled:cursor-not-allowed disabled:opacity-60 dark:shadow-[inset_0_1px_0_rgba(255,255,255,0.04),0_14px_28px_-24px_rgba(0,0,0,0.32)]",
        className,
      )}
      {...props}
    />
  ),
);
Textarea.displayName = "Textarea";

export { Textarea };
