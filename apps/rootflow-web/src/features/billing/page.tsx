import { Coins, CreditCard, Sparkles } from "lucide-react";
import { Link } from "react-router-dom";

import { useI18n } from "@/app/providers/i18n-provider";
import { WorkspaceCreditProgress } from "@/components/billing/workspace-credit-progress";
import { ErrorState } from "@/components/feedback/error-state";
import { LoadingState } from "@/components/feedback/loading-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { useAuth } from "@/features/auth/auth-provider";
import { useWorkspaceBillingSummaryQuery } from "@/hooks/use-rootflow-data";
import { formatCredits, getWorkspaceCreditSnapshot } from "@/lib/billing/workspace-credits";

export function BillingPage() {
  const { session } = useAuth();
  const { locale, t } = useI18n();
  const workspaceId = session?.workspace.id;
  const billingSummaryQuery = useWorkspaceBillingSummaryQuery(workspaceId);
  const snapshot = getWorkspaceCreditSnapshot(billingSummaryQuery.data);

  return (
    <div className="space-y-5">
      <PageHeader
        title={t("billing.title")}
        description={t("billing.description")}
        actions={(
          <>
            <Button asChild>
              <Link to="/assistant">{t("common.actions.askAssistant")}</Link>
            </Button>
            <Button variant="outline" asChild>
              <Link to="/dashboard">{t("common.actions.backToDashboard")}</Link>
            </Button>
          </>
        )}
      />

      <section className="grid gap-3 xl:grid-cols-[1.1fr_0.9fr]">
        <Card className="border-border/82 bg-card/88">
          <CardHeader>
            <div className="space-y-1">
              <CardTitle>{t("billing.summaryTitle")}</CardTitle>
              <p className="text-sm text-muted-foreground/95">{t("billing.summaryDescription")}</p>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            {billingSummaryQuery.isLoading ? (
              <LoadingState title={t("common.labels.loading")} description={t("billing.sharedHint")} />
            ) : billingSummaryQuery.isError || !snapshot ? (
              <ErrorState
                title={t("common.labels.somethingWentWrong")}
                description={t("billing.sharedHint")}
                onRetry={() => billingSummaryQuery.refetch()}
              />
            ) : (
              <>
                <div className="flex min-w-0 flex-wrap items-center gap-2">
                  {billingSummaryQuery.data?.billingPlan ? (
                    <Badge variant="secondary">{billingSummaryQuery.data.billingPlan.name}</Badge>
                  ) : null}
                  <Badge variant="secondary">{t("billing.remainingDetail", { percent: snapshot.remainingPercent })}</Badge>
                </div>

                <div className="rounded-[22px] border border-border/78 bg-card/72 p-4">
                  <div className="grid gap-3 sm:grid-cols-3">
                    <div className="rounded-[18px] border border-border/75 bg-background/84 px-3 py-2.5">
                      <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">{t("billing.available")}</div>
                      <div className="mt-1 text-lg font-semibold tracking-[-0.03em] text-foreground">{formatCredits(snapshot.availableCredits, locale)}</div>
                    </div>
                    <div className="rounded-[18px] border border-border/75 bg-background/84 px-3 py-2.5">
                      <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">{t("billing.used")}</div>
                      <div className="mt-1 text-lg font-semibold tracking-[-0.03em] text-foreground">{formatCredits(snapshot.consumedCredits, locale)}</div>
                    </div>
                    <div className="rounded-[18px] border border-border/75 bg-background/84 px-3 py-2.5">
                      <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">{t("billing.tracked")}</div>
                      <div className="mt-1 text-lg font-semibold tracking-[-0.03em] text-foreground">{formatCredits(snapshot.totalTrackedCredits, locale)}</div>
                    </div>
                  </div>

                  <div className="mt-4 flex items-center justify-between gap-3 text-sm">
                    <div className="font-medium text-foreground">{t("billing.currentPlan")}</div>
                    <div className="min-w-0 truncate text-right text-muted-foreground" title={snapshot.planName ?? undefined}>
                      {snapshot.planName ?? t("billing.sharedHint")}
                    </div>
                  </div>
                  <WorkspaceCreditProgress className="mt-3" ratio={snapshot.remainingRatio} tone={snapshot.tone} />
                  <p className="mt-3 text-sm text-muted-foreground">
                    {snapshot.tone === "low"
                      ? t("billing.lowWarning")
                      : snapshot.tone === "critical"
                        ? t("billing.criticalWarning")
                        : snapshot.tone === "inactive"
                          ? t("billing.inactiveWarning")
                          : snapshot.tone === "empty"
                            ? t("billing.emptyWarning")
                            : t("billing.dashboardHint")}
                  </p>
                </div>
              </>
            )}
          </CardContent>
        </Card>

        <div className="space-y-3">
          <Card className="border-border/82 bg-card/88">
            <CardContent className="p-5">
              <div className="flex items-start gap-3">
                <div className="flex size-11 shrink-0 items-center justify-center rounded-[20px] border border-primary/14 bg-primary/10 text-primary">
                  <Coins className="size-5" />
                </div>
                <div className="min-w-0 space-y-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <div className="text-sm font-semibold text-foreground">{t("billing.buyCreditsPlaceholderTitle")}</div>
                    <Badge variant="secondary">{t("billing.comingSoon")}</Badge>
                  </div>
                  <p className="text-sm text-muted-foreground">{t("billing.buyCreditsPlaceholderDescription")}</p>
                </div>
              </div>
            </CardContent>
          </Card>

          <Card className="border-border/82 bg-card/88">
            <CardContent className="p-5">
              <div className="flex items-start gap-3">
                <div className="flex size-11 shrink-0 items-center justify-center rounded-[20px] border border-primary/14 bg-primary/10 text-primary">
                  <CreditCard className="size-5" />
                </div>
                <div className="min-w-0 space-y-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <div className="text-sm font-semibold text-foreground">{t("billing.upgradePlaceholderTitle")}</div>
                    <Badge variant="secondary">{t("billing.comingSoon")}</Badge>
                  </div>
                  <p className="text-sm text-muted-foreground">{t("billing.upgradePlaceholderDescription")}</p>
                </div>
              </div>
            </CardContent>
          </Card>

          <Card className="border-primary/18 bg-primary/[0.05]">
            <CardContent className="p-5">
              <div className="flex items-start gap-3">
                <div className="flex size-11 shrink-0 items-center justify-center rounded-[20px] border border-primary/16 bg-background/80 text-primary">
                  <Sparkles className="size-5" />
                </div>
                <div className="space-y-3">
                  <div>
                    <div className="text-sm font-semibold text-foreground">{t("billing.sharedHint")}</div>
                    <p className="mt-1 text-sm text-muted-foreground">{t("billing.dashboardHint")}</p>
                  </div>
                  <Button variant="outline" asChild>
                    <Link to="/assistant">{t("common.actions.askAssistant")}</Link>
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
      </section>
    </div>
  );
}
