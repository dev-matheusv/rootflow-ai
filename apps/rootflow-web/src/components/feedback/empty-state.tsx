import type { LucideIcon } from "lucide-react";

interface EmptyStateProps {
  icon: LucideIcon;
  title: string;
  description: string;
}

export function EmptyState({ icon: Icon, title, description }: EmptyStateProps) {
  return (
    <div className="rounded-[24px] border border-dashed border-border/85 bg-[linear-gradient(180deg,color-mix(in_srgb,var(--card)_92%,transparent),color-mix(in_srgb,var(--background)_72%,transparent))] p-5 shadow-[0_20px_40px_-34px_rgba(16,36,71,0.16)]">
      <div className="flex flex-col gap-3">
        <div className="flex size-11 items-center justify-center rounded-2xl border border-primary/14 bg-primary/10 text-primary">
          <Icon className="size-5" />
        </div>
        <div className="space-y-1">
          <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-primary/72">Nothing here yet</div>
          <h3 className="font-display text-[1.02rem] font-semibold tracking-[-0.03em] text-foreground">{title}</h3>
          <p className="text-sm leading-6 text-muted-foreground/95">{description}</p>
        </div>
      </div>
    </div>
  );
}
