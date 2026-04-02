import { LoaderCircle } from "lucide-react";

interface LoadingStateProps {
  title: string;
  description: string;
}

export function LoadingState({ title, description }: LoadingStateProps) {
  return (
    <div className="rounded-[22px] border border-border/75 bg-background/56 p-5">
      <div className="flex flex-col gap-3">
        <div className="flex size-12 items-center justify-center rounded-2xl border border-primary/12 bg-primary/8 text-primary">
          <LoaderCircle className="size-5 animate-spin" />
        </div>
        <div className="space-y-1">
          <div className="text-xs font-semibold uppercase tracking-[0.2em] text-primary/75">Loading</div>
          <h3 className="font-display text-[1rem] font-medium tracking-[-0.03em] text-foreground">{title}</h3>
          <p className="text-sm leading-6 text-muted-foreground">{description}</p>
        </div>
      </div>
    </div>
  );
}
