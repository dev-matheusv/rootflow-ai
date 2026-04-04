import { cn } from "@/lib/utils";
import type { WorkspaceCreditTone } from "@/lib/billing/workspace-credits";

interface WorkspaceCreditProgressProps {
  ratio: number;
  tone: WorkspaceCreditTone;
  className?: string;
}

export function WorkspaceCreditProgress({ ratio, tone, className }: WorkspaceCreditProgressProps) {
  const clampedRatio = Math.min(1, Math.max(0, ratio));
  const percentage = clampedRatio * 100;
  const width = percentage > 0 && percentage < 6 ? 6 : percentage;

  return (
    <div className={cn("h-2 w-full overflow-hidden rounded-full bg-border/72", className)}>
      <div
        className={cn(
          "h-full rounded-full transition-[width,background-color,box-shadow] duration-300",
          tone === "healthy"
            ? "bg-primary shadow-[0_10px_18px_-12px_color-mix(in_srgb,var(--primary)_58%,transparent)]"
            : tone === "low"
              ? "bg-amber-500 shadow-[0_10px_18px_-12px_rgba(245,158,11,0.5)]"
              : tone === "inactive"
                ? "bg-muted-foreground/35"
                : "bg-rose-500 shadow-[0_10px_18px_-12px_rgba(244,63,94,0.48)]",
        )}
        style={{ width: `${width}%` }}
      />
    </div>
  );
}
