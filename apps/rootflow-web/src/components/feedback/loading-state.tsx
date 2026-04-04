import { LoaderCircle } from "lucide-react";

import { useI18n } from "@/app/providers/i18n-provider";

interface LoadingStateProps {
  title: string;
  description: string;
}

export function LoadingState({ title, description }: LoadingStateProps) {
  const { t } = useI18n();

  return (
    <div className="rounded-[24px] border border-border/85 bg-[linear-gradient(180deg,color-mix(in_srgb,var(--card)_96%,transparent),color-mix(in_srgb,var(--background)_74%,transparent))] p-5 shadow-[0_20px_40px_-32px_rgba(16,36,71,0.18)]">
      <div className="flex flex-col gap-3">
        <div className="relative flex size-12 items-center justify-center rounded-2xl border border-primary/18 bg-primary/10 text-primary">
          <div className="absolute inset-0 animate-pulse rounded-2xl bg-primary/[0.08]" />
          <LoaderCircle className="size-5 animate-spin" />
        </div>
        <div className="space-y-1">
          <div className="text-xs font-semibold uppercase tracking-[0.2em] text-primary/75">{t("common.labels.loading")}</div>
          <h3 className="font-display text-[1rem] font-semibold tracking-[-0.03em] text-foreground">{title}</h3>
          <p className="text-sm leading-6 text-muted-foreground/95">{description}</p>
        </div>
      </div>
    </div>
  );
}
