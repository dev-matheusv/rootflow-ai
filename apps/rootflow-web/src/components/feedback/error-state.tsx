import { AlertCircle } from "lucide-react";

import { Button } from "@/components/ui/button";

interface ErrorStateProps {
  title: string;
  description: string;
  onRetry?: () => void;
}

export function ErrorState({ title, description, onRetry }: ErrorStateProps) {
  return (
    <div className="rounded-[22px] border border-destructive/24 bg-destructive/8 p-5 shadow-[0_16px_34px_-30px_rgba(120,34,34,0.16)]">
      <div className="flex flex-col gap-3">
        <div className="flex size-12 items-center justify-center rounded-2xl border border-destructive/18 bg-destructive/10 text-destructive">
          <AlertCircle className="size-5" />
        </div>
        <div className="space-y-1">
          <div className="text-xs font-semibold uppercase tracking-[0.2em] text-destructive/80">Something went wrong</div>
          <h3 className="font-display text-[1rem] font-semibold tracking-[-0.03em] text-foreground">{title}</h3>
          <p className="text-sm leading-6 text-muted-foreground">{description}</p>
        </div>
      </div>
      <div className="mt-4">
        {onRetry ? (
          <Button variant="outline" onClick={onRetry}>
            Try again
          </Button>
        ) : null}
      </div>
    </div>
  );
}
