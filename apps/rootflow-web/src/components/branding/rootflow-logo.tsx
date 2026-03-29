import { cn } from "@/lib/utils";

interface RootFlowLogoProps {
  className?: string;
  compact?: boolean;
}

export function RootFlowLogo({ className, compact = false }: RootFlowLogoProps) {
  return (
    <div className={cn("flex items-center gap-3", className)}>
      <div className="relative flex size-11 items-center justify-center overflow-hidden rounded-[18px] border border-white/30 bg-[linear-gradient(135deg,#0e48c9_0%,#2785ff_48%,#87c8ff_100%)] shadow-[0_20px_40px_-20px_rgba(20,92,255,0.8)]">
        <div className="absolute inset-[5px] rounded-[14px] bg-[radial-gradient(circle_at_top,_rgba(255,255,255,0.95),rgba(255,255,255,0.18)_50%,transparent_70%)]" />
        <div className="relative h-6 w-6 rounded-full border border-white/80 bg-white/25 backdrop-blur-md">
          <div className="absolute left-1/2 top-1/2 h-8 w-px -translate-x-1/2 -translate-y-1/2 bg-white/90" />
          <div className="absolute left-1/2 top-1/2 h-px w-8 -translate-x-1/2 -translate-y-1/2 bg-white/90" />
        </div>
      </div>

      {compact ? null : (
        <div className="space-y-0.5">
          <div className="font-display text-[1.08rem] font-semibold tracking-[-0.04em] text-foreground">RootFlow</div>
          <div className="text-xs font-medium tracking-[0.18em] text-muted-foreground uppercase">Knowledge Operating System</div>
        </div>
      )}
    </div>
  );
}
