import { Coins, Sparkles } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";

import { useI18n } from "@/app/providers/i18n-provider";
import { WorkspaceCreditProgress } from "@/components/billing/workspace-credit-progress";
import { ErrorState } from "@/components/feedback/error-state";
import { FeedbackToast } from "@/components/feedback/feedback-toast";
import { LoadingState } from "@/components/feedback/loading-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { useAuth } from "@/features/auth/auth-provider";
import {
  useBillingCheckoutMutation,
  useBillingCreditPacksQuery,
  useBillingPlansQuery,
  useWorkspaceBillingSummaryQuery,
} from "@/hooks/use-rootflow-data";
import { formatCredits, getWorkspaceCreditSnapshot } from "@/lib/billing/workspace-credits";

interface BillingFeedbackToast {
  id: number;
  tone: "success" | "error" | "info";
  title: string;
  description: string;
}

export function BillingPage() {
  const { session } = useAuth();
  const { locale, t } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const [activeCheckoutKey, setActiveCheckoutKey] = useState<string | null>(null);
  const [feedbackToast, setFeedbackToast] = useState<BillingFeedbackToast | null>(null);
  const [isCheckoutSyncing, setIsCheckoutSyncing] = useState(false);
  const workspaceId = session?.workspace.id;
  const billingSummaryQuery = useWorkspaceBillingSummaryQuery(workspaceId);
  const billingPlansQuery = useBillingPlansQuery();
  const creditPacksQuery = useBillingCreditPacksQuery();
  const billingCheckoutMutation = useBillingCheckoutMutation();
  const snapshot = getWorkspaceCreditSnapshot(billingSummaryQuery.data);
  const currentPlanCode = billingSummaryQuery.data?.billingPlan?.code?.toLowerCase() ?? null;
  const currentSubscriptionStatus = billingSummaryQuery.data?.subscription?.status ?? null;
  const checkoutStatus = searchParams.get("checkout");
  const [checkoutError, setCheckoutError] = useState<{ surface: "plans" | "credits"; message: string } | null>(null);

  const showToast = useCallback((tone: BillingFeedbackToast["tone"], title: string, description: string) => {
    setFeedbackToast({
      id: Date.now(),
      tone,
      title,
      description,
    });
  }, []);

  useEffect(() => {
    if (!feedbackToast) {
      return undefined;
    }

    const timeoutId = window.setTimeout(() => {
      setFeedbackToast((currentToast) => (currentToast?.id === feedbackToast.id ? null : currentToast));
    }, 4200);

    return () => window.clearTimeout(timeoutId);
  }, [feedbackToast]);

  useEffect(() => {
    if (checkoutStatus !== "success" && checkoutStatus !== "cancel") {
      return;
    }

    if (checkoutStatus === "success") {
      setIsCheckoutSyncing(true);
      void billingSummaryQuery.refetch();
      showToast("success", t("billing.checkoutSuccessTitle"), t("billing.checkoutSuccessDescription"));
    } else {
      showToast("info", t("billing.checkoutCanceledTitle"), t("billing.checkoutCanceledDescription"));
    }

    const nextSearchParams = new URLSearchParams(searchParams);
    nextSearchParams.delete("checkout");
    nextSearchParams.delete("session_id");
    setSearchParams(nextSearchParams, { replace: true });
  }, [billingSummaryQuery, checkoutStatus, searchParams, setSearchParams, showToast, t]);

  useEffect(() => {
    if (!isCheckoutSyncing || !workspaceId) {
      return undefined;
    }

    let attemptsRemaining = 6;
    const intervalId = window.setInterval(() => {
      attemptsRemaining -= 1;

      void billingSummaryQuery.refetch().then((result) => {
        const subscription = result.data?.subscription;
        const hasPaidSubscription = subscription?.status === "Active" && !subscription.trialEndsAtUtc;

        if (hasPaidSubscription || attemptsRemaining <= 0) {
          setIsCheckoutSyncing(false);
        }
      });

      if (attemptsRemaining <= 0) {
        window.clearInterval(intervalId);
      }
    }, 2500);

    return () => window.clearInterval(intervalId);
  }, [billingSummaryQuery, isCheckoutSyncing, workspaceId]);

  const handlePlanCheckout = async (planCode: string, priceId?: string | null) => {
    setActiveCheckoutKey(`plan:${planCode}`);
    setCheckoutError(null);

    try {
      const checkoutSession = await billingCheckoutMutation.mutateAsync({ priceId: priceId ?? "" });
      window.location.assign(checkoutSession.url);
    } catch (error) {
      const message = error instanceof Error ? error.message : t("billing.sharedHint");
      setCheckoutError({ surface: "plans", message });
      showToast(
        "error",
        t("common.labels.somethingWentWrong"),
        message,
      );
    } finally {
      setActiveCheckoutKey(null);
    }
  };

  const handleCreditCheckout = async (creditPackCode: string, priceId?: string | null) => {
    setActiveCheckoutKey(`credits:${creditPackCode}`);
    setCheckoutError(null);

    try {
      const checkoutSession = await billingCheckoutMutation.mutateAsync({ priceId: priceId ?? "" });
      window.location.assign(checkoutSession.url);
    } catch (error) {
      const message = error instanceof Error ? error.message : t("billing.sharedHint");
      setCheckoutError({ surface: "credits", message });
      showToast(
        "error",
        t("common.labels.somethingWentWrong"),
        message,
      );
    } finally {
      setActiveCheckoutKey(null);
    }
  };

  return (
    <div className="space-y-5">
      <div className="pointer-events-none fixed inset-x-4 top-4 z-50 flex justify-end sm:inset-x-6">
        {feedbackToast ? (
          <FeedbackToast
            tone={feedbackToast.tone}
            title={feedbackToast.title}
            description={feedbackToast.description}
          />
        ) : null}
      </div>

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

      <section className="grid gap-3 xl:grid-cols-[1.05fr_0.95fr]">
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
            <CardHeader>
              <div className="space-y-1">
                <CardTitle>{t("billing.plansTitle")}</CardTitle>
                <p className="text-sm text-muted-foreground/95">{t("billing.plansDescription")}</p>
              </div>
            </CardHeader>
            <CardContent className="space-y-3">
              {billingPlansQuery.isLoading ? (
                <LoadingState title={t("common.labels.loading")} description={t("billing.plansDescription")} />
              ) : billingPlansQuery.isError ? (
                <ErrorState
                  title={t("common.labels.somethingWentWrong")}
                  description={t("billing.plansDescription")}
                  onRetry={() => billingPlansQuery.refetch()}
                />
              ) : (
                <>
                  <div className="grid gap-3">
                    {billingPlansQuery.data?.map((plan) => {
                      const isCurrentPlan =
                        currentSubscriptionStatus === "Active" &&
                        currentPlanCode === plan.code.toLowerCase();
                      const isRedirecting = activeCheckoutKey === `plan:${plan.code}`;

                      return (
                        <div
                          key={plan.id}
                          className={`rounded-[22px] border p-4 ${
                            isCurrentPlan
                              ? "border-primary/20 bg-primary/[0.05]"
                              : "border-border/78 bg-background/80"
                          }`}
                        >
                          <div className="flex flex-wrap items-start justify-between gap-3">
                            <div className="space-y-2">
                              <div className="flex flex-wrap items-center gap-2">
                                <div className="text-base font-semibold tracking-[-0.03em] text-foreground">{plan.name}</div>
                                {isCurrentPlan ? <Badge>{t("billing.currentPlanBadge")}</Badge> : null}
                              </div>
                              <div className="text-2xl font-semibold tracking-[-0.04em] text-foreground">
                                {formatCurrency(plan.monthlyPrice, plan.currencyCode, locale)}
                              </div>
                              <div className="flex min-w-0 flex-wrap gap-2 text-sm text-muted-foreground">
                                <span>{t("billing.includedCreditsLabel", { count: formatCredits(plan.includedCredits, locale) })}</span>
                                <span>{t("billing.maxUsersLabel", { count: plan.maxUsers })}</span>
                              </div>
                            </div>

                            <Button
                              variant={isCurrentPlan ? "outline" : "default"}
                              disabled={isCurrentPlan || isRedirecting}
                              onClick={() => void handlePlanCheckout(plan.code, plan.priceId)}
                            >
                              {isRedirecting
                                ? t("billing.redirecting")
                                : isCurrentPlan
                                  ? t("billing.currentPlanBadge")
                                  : t("billing.choosePlan")}
                            </Button>
                          </div>
                        </div>
                      );
                    })}
                  </div>

                  {checkoutError?.surface === "plans" ? (
                    <p className="text-sm text-rose-600 dark:text-rose-300">{checkoutError.message}</p>
                  ) : null}
                </>
              )}
            </CardContent>
          </Card>

          <Card className="border-border/82 bg-card/88">
            <CardHeader>
              <div className="space-y-1">
                <CardTitle>{t("billing.creditPacksTitle")}</CardTitle>
                <p className="text-sm text-muted-foreground/95">{t("billing.creditPacksDescription")}</p>
              </div>
            </CardHeader>
            <CardContent className="space-y-3">
              {creditPacksQuery.isLoading ? (
                <LoadingState title={t("common.labels.loading")} description={t("billing.creditPacksDescription")} />
              ) : creditPacksQuery.isError ? (
                <ErrorState
                  title={t("common.labels.somethingWentWrong")}
                  description={t("billing.creditPacksDescription")}
                  onRetry={() => creditPacksQuery.refetch()}
                />
              ) : (
                <>
                  {creditPacksQuery.data?.map((creditPack) => {
                    const isRedirecting = activeCheckoutKey === `credits:${creditPack.code}`;

                    return (
                      <div key={creditPack.code} className="rounded-[22px] border border-border/78 bg-background/80 p-4">
                        <div className="flex items-start gap-3">
                          <div className="flex size-11 shrink-0 items-center justify-center rounded-[20px] border border-primary/14 bg-primary/10 text-primary">
                            <Coins className="size-5" />
                          </div>
                          <div className="min-w-0 flex-1 space-y-2">
                            <div className="flex flex-wrap items-center gap-2">
                              <div className="text-sm font-semibold text-foreground">{creditPack.name}</div>
                              {!creditPack.isConfigured ? (
                                <Badge variant="secondary">{t("billing.comingSoon")}</Badge>
                              ) : null}
                            </div>
                            <p className="text-sm text-muted-foreground">{creditPack.description}</p>
                            <div className="flex min-w-0 flex-wrap gap-2 text-sm text-muted-foreground">
                              <span>{formatCredits(creditPack.credits, locale)}</span>
                              <span>{formatCurrency(creditPack.amount, creditPack.currencyCode, locale)}</span>
                            </div>
                            <Button
                              disabled={!creditPack.isConfigured || isRedirecting}
                              onClick={() => void handleCreditCheckout(creditPack.code, creditPack.priceId)}
                            >
                              {isRedirecting
                                ? t("billing.redirecting")
                                : creditPack.isConfigured
                                  ? t("common.actions.buyCredits")
                                  : t("billing.unavailableCta")}
                            </Button>
                          </div>
                        </div>
                      </div>
                    );
                  })}

                  {checkoutError?.surface === "credits" ? (
                    <p className="text-sm text-rose-600 dark:text-rose-300">{checkoutError.message}</p>
                  ) : null}
                </>
              )}
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
                  <div className="flex flex-wrap gap-2">
                    <Button variant="outline" asChild>
                      <Link to="/assistant">{t("common.actions.askAssistant")}</Link>
                    </Button>
                    <Button variant="ghost" asChild>
                      <Link to="/dashboard">{t("common.actions.backToDashboard")}</Link>
                    </Button>
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
      </section>
    </div>
  );
}

function formatCurrency(value: number, currencyCode: string, locale: string) {
  return new Intl.NumberFormat(locale, {
    style: "currency",
    currency: currencyCode,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value);
}
