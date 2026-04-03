import { LoaderCircle } from "lucide-react";

interface LoadingStateProps {
  title: string;
  description: string;
}

export function LoadingState({ title, description }: LoadingStateProps) {
  return (
    <div className="rounded-[22px] border border-border/80 bg-card/70 p-5 shadow-[0_16px_34px_-30px_rgba(16,36,71,0.16)]">
      <div className="flex flex-col gap-3">
        <div className="relative flex size-12 items-center justify-center rounded-2xl border border-primary/18 bg-primary/10 text-primary">
          <div className="absolute inset-0 rounded-2xl bg-primary/[0.08] animate-pulse" />
          <LoaderCircle className="size-5 animate-spin" />
        </div>
        <div className="space-y-1">
          <div className="text-xs font-semibold uppercase tracking-[0.2em] text-primary/75">Loading</div>
          <h3 className="font-display text-[1rem] font-semibold tracking-[-0.03em] text-foreground">{title}</h3>
          <p className="text-sm leading-6 text-muted-foreground">{description}</p>
        </div>
      </div>
    </div>
  );
}
