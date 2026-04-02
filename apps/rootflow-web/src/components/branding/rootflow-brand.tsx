import { cn } from "@/lib/utils";

type RootFlowBrandVariant = "icon" | "logo" | "mark" | "dark-mark";
type RootFlowBrandSize = "sm" | "md" | "lg";

interface RootFlowBrandProps {
  className?: string;
  size?: RootFlowBrandSize;
  variant?: RootFlowBrandVariant;
  alt?: string;
}

const brandAssets: Record<
  RootFlowBrandVariant,
  {
    alt: string;
    sizes: Record<RootFlowBrandSize, string>;
    src: string;
  }
> = {
  icon: {
    src: "/rootflow-icon.png",
    alt: "RootFlow",
    sizes: {
      sm: "h-8 w-8",
      md: "h-9 w-9",
      lg: "h-11 w-11",
    },
  },
  logo: {
    src: "/rootflow-logo.png",
    alt: "RootFlow",
    sizes: {
      sm: "h-8",
      md: "h-10",
      lg: "h-16 sm:h-[4.5rem]",
    },
  },
  mark: {
    src: "/rootflow-mark.png",
    alt: "RootFlow mark",
    sizes: {
      sm: "h-7",
      md: "h-10",
      lg: "h-14",
    },
  },
  "dark-mark": {
    src: "/rootflow-dark-mark.png",
    alt: "RootFlow dark mark",
    sizes: {
      sm: "h-10",
      md: "h-14",
      lg: "h-20 sm:h-24",
    },
  },
};

export type { RootFlowBrandProps, RootFlowBrandSize, RootFlowBrandVariant };

export function RootFlowBrand({
  className,
  size = "md",
  variant = "logo",
  alt,
}: RootFlowBrandProps) {
  const asset = brandAssets[variant];

  return (
    <img
      src={asset.src}
      alt={alt ?? asset.alt}
      decoding="async"
      draggable={false}
      className={cn("block w-auto max-w-full shrink-0 select-none object-contain", asset.sizes[size], className)}
    />
  );
}
