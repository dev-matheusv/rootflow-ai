import { AlertCircle, CheckCircle2, Info } from "lucide-react";

import { cn } from "@/lib/utils";

type FeedbackToastTone = "success" | "error" | "info";

interface FeedbackToastProps {
  title: string;
  description?: string | null;
  tone?: FeedbackToastTone;
  className?: string;
}

const toneStyles: Record<FeedbackToastTone, { container: string; icon: string }> = {
  success: {
    container: "border-primary/20 bg-card/96 text-foreground shadow-[0_18px_48px_-26px_rgba(15,99,236,0.42)]",
    icon: "border-primary/16 bg-primary/12 text-primary",
  },
  error: {
    container: "border-destructive/28 bg-card/96 text-foreground shadow-[0_18px_48px_-26px_rgba(217,68,68,0.3)]",
    icon: "border-destructive/20 bg-destructive/12 text-destructive",
  },
  info: {
    container: "border-border/84 bg-card/96 text-foreground shadow-[0_18px_48px_-26px_rgba(18,35,58,0.18)]",
    icon: "border-border/70 bg-background/84 text-muted-foreground",
  },
};

export function FeedbackToast({ title, description, tone = "info", className }: FeedbackToastProps) {
  const styles = toneStyles[tone];
  const Icon = tone === "success" ? CheckCircle2 : tone === "error" ? AlertCircle : Info;

  return (
    <div
      role="status"
      aria-live="polite"
      className={cn(
        "pointer-events-auto w-full max-w-sm rounded-[24px] border px-4 py-3.5 backdrop-blur-xl motion-safe:animate-[rf-fade-up_240ms_cubic-bezier(0.22,1,0.36,1)]",
        styles.container,
        className,
      )}
    >
      <div className="flex items-start gap-3">
        <div className={cn("mt-0.5 flex size-9 shrink-0 items-center justify-center rounded-[18px] border", styles.icon)}>
          <Icon className="size-[18px]" />
        </div>
        <div className="min-w-0 space-y-1">
          <div className="text-sm font-semibold tracking-[-0.02em] text-foreground">{title}</div>
          {description ? <p className="text-sm leading-6 text-muted-foreground">{description}</p> : null}
        </div>
      </div>
    </div>
  );
}
