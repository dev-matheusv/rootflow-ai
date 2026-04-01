import { cn } from "@/lib/utils";

interface RootFlowLogoProps {
  className?: string;
  tagline?: string;
  variant?: "icon" | "lockup" | "banner";
}

function RootFlowMark({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        "relative flex size-11 shrink-0 items-center justify-center overflow-hidden rounded-[18px] border border-white/55 bg-[linear-gradient(145deg,#0a3eaf_0%,#0f63ec_45%,#4cc8ff_100%)] shadow-[0_18px_44px_-22px_rgba(13,91,237,0.58)]",
        className,
      )}
    >
      <div className="absolute inset-0 bg-[radial-gradient(circle_at_20%_18%,rgba(255,255,255,0.52),transparent_38%),radial-gradient(circle_at_80%_85%,rgba(255,255,255,0.18),transparent_34%)]" />
      <svg
        aria-hidden="true"
        viewBox="0 0 48 48"
        className="relative z-10 size-[26px] text-white drop-shadow-[0_6px_16px_rgba(8,25,58,0.28)]"
      >
        <path
          d="M24 10v20M24 18l-7-5M24 18l7-5M24 26l-7 7M24 26l7 7"
          fill="none"
          stroke="currentColor"
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth="2.5"
        />
        <circle cx="24" cy="8" r="3.25" fill="currentColor" />
        <circle cx="24" cy="31" r="3.25" fill="currentColor" />
        <circle cx="14.5" cy="12.5" r="3" fill="currentColor" />
        <circle cx="33.5" cy="12.5" r="3" fill="currentColor" />
        <circle cx="14.5" cy="39" r="3" fill="currentColor" />
        <circle cx="33.5" cy="39" r="3" fill="currentColor" />
      </svg>
    </div>
  );
}

export function RootFlowLogo({
  className,
  tagline = "Grounded knowledge assistant",
  variant = "lockup",
}: RootFlowLogoProps) {
  if (variant === "icon") {
    return <RootFlowMark className={className} />;
  }

  if (variant === "banner") {
    return (
      <div
        className={cn(
          "relative overflow-hidden rounded-[28px] border border-white/55 bg-[linear-gradient(135deg,rgba(8,47,128,0.96),rgba(14,95,240,0.96)_52%,rgba(74,201,255,0.9))] px-5 py-4 text-white shadow-[0_28px_80px_-48px_rgba(11,79,214,0.72)]",
          className,
        )}
      >
        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_0%_0%,rgba(255,255,255,0.22),transparent_34%),radial-gradient(circle_at_100%_100%,rgba(255,255,255,0.14),transparent_28%)]" />
        <div className="relative flex items-center gap-4">
          <RootFlowMark className="size-14 rounded-[22px] border-white/45 bg-[linear-gradient(145deg,#08348d_0%,#0f63ec_48%,#64d8ff_100%)]" />
          <div className="min-w-0">
            <div className="font-display text-[1.48rem] font-semibold tracking-[-0.05em]">RootFlow</div>
            <div className="text-sm font-medium text-white/84">{tagline}</div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className={cn("flex items-center gap-3", className)}>
      <RootFlowMark />
      <div className="space-y-0.5">
        <div className="font-display text-[1.05rem] font-semibold tracking-[-0.05em] text-foreground">RootFlow</div>
        <div className="text-[11px] font-semibold uppercase tracking-[0.22em] text-muted-foreground">{tagline}</div>
      </div>
    </div>
  );
}
