import { Coins } from "lucide-react";
import { useMemo } from "react";
import { Link } from "react-router-dom";

import { useI18n } from "@/app/providers/i18n-provider";
import { WorkspaceCreditProgress } from "@/components/billing/workspace-credit-progress";
import { Badge } from "@/components/ui/badge";
import { useWorkspaceBillingSummaryQuery } from "@/hooks/use-rootflow-data";
import { formatCredits, getWorkspaceCreditSnapshot } from "@/lib/billing/workspace-credits";
import { cn } from "@/lib/utils";

interface TopbarCreditSummaryProps {
  workspaceId?: string | null;
  className?: string;
}

export function TopbarCreditSummary({ workspaceId, className }: TopbarCreditSummaryProps) {
  const { locale, t } = useI18n();
  const billingSummaryQuery = useWorkspaceBillingSummaryQuery(workspaceId);
  const snapshot = useMemo(
    () => getWorkspaceCreditSnapshot(billingSummaryQuery.data),
    [billingSummaryQuery.data],
  );

  if (!workspaceId) {
    return null;
  }

  if (billingSummaryQuery.isLoading) {
    return (
      <div
        className={cn(
          "w-full overflow-hidden rounded-[18px] border border-border/78 bg-background/84 px-3.5 py-3 shadow-[0_16px_30px_-24px_rgba(16,36,71,0.14)] sm:w-[230px]",
          className,
        )}
      >
        <div className="animate-pulse space-y-2.5">
          <div className="h-3 w-20 rounded-full bg-border/70" />
          <div className="h-4 w-32 rounded-full bg-border/70" />
          <div className="h-2 rounded-full bg-border/70" />
        </div>
      </div>
    );
  }

  if (!snapshot) {
    return null;
  }

  const statusLabel =
    snapshot.tone === "healthy"
      ? t("billing.healthyState")
      : snapshot.tone === "low"
        ? t("billing.lowState")
        : snapshot.tone === "critical"
          ? t("billing.criticalState")
          : snapshot.tone === "inactive"
            ? t("billing.inactiveState")
            : t("billing.emptyState");
  const statusClassName =
    snapshot.tone === "healthy"
      ? "border-primary/24 bg-primary/[0.12] text-primary"
      : snapshot.tone === "low"
        ? "border-amber-500/24 bg-amber-500/[0.12] text-amber-700 dark:text-amber-300"
        : snapshot.tone === "inactive"
          ? "border-border/78 bg-background/84 text-muted-foreground"
          : "border-rose-500/24 bg-rose-500/[0.12] text-rose-700 dark:text-rose-300";

  return (
    <Link to="/billing" className={cn("block min-w-0 w-full sm:w-[230px]", className)}>
      <div className="rounded-[18px] border border-border/78 bg-background/84 px-3.5 py-3 shadow-[0_16px_30px_-24px_rgba(16,36,71,0.14)] transition-[transform,border-color,background-color,box-shadow] duration-200 hover:-translate-y-0.5 hover:border-primary/22 hover:bg-background/92 hover:shadow-[0_22px_40px_-26px_rgba(18,72,166,0.16)]">
        <div className="flex items-center justify-between gap-3">
          <div className="flex min-w-0 items-center gap-2.5">
            <div className="flex size-8 shrink-0 items-center justify-center rounded-2xl border border-primary/14 bg-primary/10 text-primary">
              <Coins className="size-4" />
            </div>
            <div className="min-w-0">
              <div className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
                {t("billing.shellLabel")}
              </div>
              <div className="truncate text-sm font-semibold tracking-[-0.02em] text-foreground">
                {t("billing.availableShort", { count: formatCredits(snapshot.availableCredits, locale) })}
              </div>
            </div>
          </div>
          <Badge className={statusClassName}>{statusLabel}</Badge>
        </div>

        <div className="mt-3 flex min-w-0 items-center justify-between gap-3 text-xs text-muted-foreground">
          <div className="min-w-0 truncate" title={snapshot.planName ?? undefined}>
            {snapshot.planName ?? t("billing.sharedHint")}
          </div>
          <div className="shrink-0">{t("billing.remainingShort", { percent: snapshot.remainingPercent })}</div>
        </div>

        <WorkspaceCreditProgress className="mt-2.5" ratio={snapshot.remainingRatio} tone={snapshot.tone} />
      </div>
    </Link>
  );
}
