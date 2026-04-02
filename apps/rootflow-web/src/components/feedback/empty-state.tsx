import type { LucideIcon } from "lucide-react";

interface EmptyStateProps {
  icon: LucideIcon;
  title: string;
  description: string;
}

export function EmptyState({ icon: Icon, title, description }: EmptyStateProps) {
  return (
    <div className="rounded-[22px] border border-dashed border-border/75 bg-background/54 p-5">
      <div className="flex flex-col gap-3">
        <div className="flex size-11 items-center justify-center rounded-2xl bg-primary/8 text-primary">
          <Icon className="size-5" />
        </div>
        <div className="space-y-1">
          <h3 className="font-display text-[1rem] font-medium tracking-[-0.03em] text-foreground">{title}</h3>
          <p className="text-sm leading-6 text-muted-foreground">{description}</p>
        </div>
      </div>
    </div>
  );
}
