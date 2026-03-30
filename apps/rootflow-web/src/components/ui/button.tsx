import { Slot } from "@radix-ui/react-slot";
import { cva, type VariantProps } from "class-variance-authority";
import * as React from "react";

import { cn } from "@/lib/utils";

const buttonVariants = cva(
  "inline-flex cursor-pointer items-center justify-center gap-2 whitespace-nowrap rounded-2xl border text-sm font-semibold transition-[transform,box-shadow,background-color,border-color,color,opacity] duration-200 outline-none focus-visible:ring-2 focus-visible:ring-ring/70 focus-visible:ring-offset-2 focus-visible:ring-offset-background disabled:pointer-events-none disabled:translate-y-0 disabled:scale-100 disabled:opacity-55 disabled:shadow-none motion-safe:hover:-translate-y-px active:translate-y-[1px] active:scale-[0.985] [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0",
  {
    variants: {
      variant: {
        default:
          "border-primary/70 bg-primary text-primary-foreground shadow-[0_18px_42px_-24px_color-mix(in_srgb,var(--primary)_58%,transparent)] hover:border-primary/80 hover:bg-primary/92 hover:shadow-[0_22px_46px_-22px_color-mix(in_srgb,var(--primary)_66%,transparent)] active:bg-primary/84",
        secondary:
          "border-border/75 bg-secondary text-secondary-foreground shadow-[0_14px_34px_-28px_rgba(18,50,104,0.28)] hover:border-primary/18 hover:bg-secondary/88 hover:text-foreground active:bg-secondary/78",
        ghost:
          "border-transparent bg-transparent text-foreground hover:border-border/60 hover:bg-secondary/75 hover:text-foreground active:bg-secondary",
        outline:
          "border-border/80 bg-background/84 text-foreground shadow-[0_12px_28px_-24px_rgba(18,38,74,0.4)] hover:border-primary/25 hover:bg-secondary/68 hover:shadow-[0_18px_36px_-24px_rgba(18,72,166,0.34)] active:bg-secondary/88",
      },
      size: {
        default: "h-11 px-4 py-2.5",
        sm: "h-9 rounded-xl px-3",
        lg: "h-12 rounded-2xl px-5 text-[0.95rem]",
        icon: "size-10 rounded-2xl",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  },
);

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean;
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, asChild = false, ...props }, ref) => {
    const Comp = asChild ? Slot : "button";

    return <Comp className={cn(buttonVariants({ variant, size, className }))} ref={ref} {...props} />;
  },
);
Button.displayName = "Button";

export { Button };
