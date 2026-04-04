import { Slot } from "@radix-ui/react-slot";
import { cva, type VariantProps } from "class-variance-authority";
import * as React from "react";

import { cn } from "@/lib/utils";

const buttonVariants = cva(
  "inline-flex cursor-pointer items-center justify-center gap-2 whitespace-nowrap rounded-2xl border text-sm font-semibold tracking-[-0.01em] transition-[transform,box-shadow,background-color,border-color,color,opacity,filter] duration-200 outline-none focus-visible:ring-4 focus-visible:ring-ring/18 focus-visible:ring-offset-2 focus-visible:ring-offset-background disabled:pointer-events-none disabled:translate-y-0 disabled:scale-100 disabled:opacity-55 disabled:shadow-none motion-safe:hover:-translate-y-0.5 motion-safe:hover:scale-[1.01] active:translate-y-[1px] active:scale-[0.99] [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0",
  {
    variants: {
      variant: {
        default:
          "border-primary/80 bg-primary text-primary-foreground shadow-[0_18px_38px_-20px_color-mix(in_srgb,var(--primary)_44%,transparent)] hover:border-primary/90 hover:bg-primary/95 hover:shadow-[0_24px_46px_-20px_color-mix(in_srgb,var(--primary)_48%,transparent)] active:bg-primary/90",
        secondary:
          "border-border/85 bg-secondary/92 text-secondary-foreground shadow-[0_14px_30px_-24px_rgba(18,50,104,0.18)] hover:border-primary/24 hover:bg-secondary hover:text-foreground active:bg-secondary/88",
        ghost:
          "border-transparent bg-transparent text-foreground hover:border-border/72 hover:bg-secondary/78 hover:text-foreground active:bg-secondary/92",
        outline:
          "border-border/85 bg-background/92 text-foreground shadow-[0_14px_30px_-22px_rgba(18,38,74,0.18)] hover:border-primary/28 hover:bg-secondary/74 hover:shadow-[0_18px_38px_-22px_rgba(18,72,166,0.22)] active:bg-secondary/84",
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
