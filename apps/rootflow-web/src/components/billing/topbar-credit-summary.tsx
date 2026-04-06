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
          "w-full overflow-hidden rounded-[20px] border border-border/76 bg-background/76 px-3.5 py-2.5 shadow-[0_16px_28px_-26px_rgba(16,36,71,0.16)] sm:w-[224px]",
          className,
        )}
      >
        <div className="animate-pulse space-y-2">
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
    snapshot.isTrialUsageLimited
      ? t("billing.trialLimitReachedBadge")
      : snapshot.isTrial
      ? t("billing.trialBadge")
      : snapshot.tone === "healthy"
      ? t("billing.healthyState")
      : snapshot.tone === "low"
        ? t("billing.lowState")
        : snapshot.tone === "critical"
          ? t("billing.criticalState")
          : snapshot.tone === "inactive"
            ? t("billing.inactiveState")
            : t("billing.emptyState");
  const statusClassName =
    snapshot.isTrialUsageLimited
      ? "border-amber-500/24 bg-amber-500/[0.12] text-amber-700 dark:text-amber-300"
      : snapshot.isTrial
      ? "border-primary/24 bg-primary/[0.12] text-primary"
      : snapshot.tone === "healthy"
      ? "border-primary/24 bg-primary/[0.12] text-primary"
      : snapshot.tone === "low"
        ? "border-amber-500/24 bg-amber-500/[0.12] text-amber-700 dark:text-amber-300"
        : snapshot.tone === "inactive"
          ? "border-border/78 bg-background/84 text-muted-foreground"
          : "border-rose-500/24 bg-rose-500/[0.12] text-rose-700 dark:text-rose-300";

  const planLabel = snapshot.isTrial ? t("billing.trialBadge") : snapshot.planName ?? t("billing.sharedHint");
  const statusDetail = snapshot.isTrial
    ? snapshot.trialDaysRemaining && snapshot.trialDaysRemaining > 0
      ? t("billing.trialEndsInDays", { count: snapshot.trialDaysRemaining })
      : t("billing.trialEndsToday")
    : t("billing.remainingShort", { percent: snapshot.remainingPercent });
  const headline = snapshot.isTrial
    ? snapshot.isTrialUsageLimited
      ? t("billing.trialLimitReachedTitle")
      : t("billing.trialTopbarTitle")
    : t("billing.availableShort", { count: formatCredits(snapshot.availableCredits, locale) });

  return (
    <Link to="/billing" className={cn("block min-w-0 w-full sm:w-[224px]", className)}>
      <div className="rounded-[20px] border border-border/76 bg-background/76 px-3.5 py-2.5 shadow-[0_16px_28px_-26px_rgba(16,36,71,0.16)] transition-[transform,border-color,background-color,box-shadow] duration-200 hover:-translate-y-0.5 hover:border-primary/20 hover:bg-background/86 hover:shadow-[0_20px_36px_-24px_rgba(18,72,166,0.14)]">
        <div className="flex items-center justify-between gap-3">
          <div className="flex min-w-0 items-center gap-2.5">
            <div className="flex size-8 shrink-0 items-center justify-center rounded-[15px] border border-primary/14 bg-primary/10 text-primary">
              <Coins className="size-4" />
            </div>
            <div className="min-w-0">
              <div className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
                {t("billing.shellLabel")}
              </div>
              <div className="truncate text-sm font-semibold tracking-[-0.02em] text-foreground">
                {headline}
              </div>
            </div>
          </div>
          <Badge className={cn("px-2.5 py-0.5 text-[10px] tracking-[0.1em]", statusClassName)}>{statusLabel}</Badge>
        </div>

        <div className="mt-2 flex min-w-0 items-center justify-between gap-3 text-xs text-muted-foreground">
          <div className="min-w-0 truncate" title={planLabel}>
            {planLabel}
          </div>
          <div className="shrink-0">{statusDetail}</div>
        </div>

        {snapshot.isTrial ? (
          <div className="mt-2 text-xs text-muted-foreground">
            {snapshot.isTrialUsageLimited
              ? t("billing.trialLimitReachedDescription")
              : t("billing.trialUsageTrackedInternally")}
          </div>
        ) : (
          <WorkspaceCreditProgress className="mt-2" ratio={snapshot.remainingRatio} tone={snapshot.tone} />
        )}
      </div>
    </Link>
  );
}
