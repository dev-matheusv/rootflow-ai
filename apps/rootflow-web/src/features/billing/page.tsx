import { Coins, LoaderCircle, Sparkles } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useLocation, useSearchParams } from "react-router-dom";

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
import type { WorkspaceBillingSummary } from "@/lib/api/contracts";
import { ApiError } from "@/lib/api/client";
import {
  clearBillingCheckoutContext,
  readBillingCheckoutContext,
  storeBillingCheckoutContext,
  type BillingCheckoutContext,
} from "@/lib/billing/checkout-session";
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
  const location = useLocation();
  const [, setSearchParams] = useSearchParams();
  const [activeCheckoutKey, setActiveCheckoutKey] = useState<string | null>(null);
  const [feedbackToast, setFeedbackToast] = useState<BillingFeedbackToast | null>(null);
  const handledCheckoutReturnRef = useRef<string | null>(null);
  const checkoutSyncTimeoutRef = useRef<number | null>(null);
  const checkoutSyncStateRef = useRef({ attempts: 0, consecutiveErrors: 0 });
  const checkoutContextRef = useRef<BillingCheckoutContext | null>(null);
  const workspaceId = session?.workspace.id;
  const [checkoutSyncStatus, setCheckoutSyncStatus] = useState<"idle" | "syncing" | "pending">("idle");
  const billingSummaryQuery = useWorkspaceBillingSummaryQuery(workspaceId, { retry: false });
  const refetchBillingSummary = billingSummaryQuery.refetch;
  const billingPlansQuery = useBillingPlansQuery();
  const creditPacksQuery = useBillingCreditPacksQuery();
  const billingCheckoutMutation = useBillingCheckoutMutation();
  const snapshot = getWorkspaceCreditSnapshot(billingSummaryQuery.data);
  const isBillingSummaryDegraded = Boolean(billingSummaryQuery.data?.isDegraded);
  const currentPlanCode = billingSummaryQuery.data?.billingPlan?.code?.toLowerCase() ?? null;
  const currentSubscriptionStatus =
    billingSummaryQuery.data?.subscriptionStatus ?? billingSummaryQuery.data?.subscription?.status ?? null;
  const checkoutSearchParams = new URLSearchParams(location.search);
  const checkoutStatus = checkoutSearchParams.get("checkout");
  const checkoutSessionId = checkoutSearchParams.get("session_id");
  const checkoutReturnKey = checkoutStatus ? `${checkoutStatus}:${checkoutSessionId ?? ""}` : null;
  const [checkoutError, setCheckoutError] = useState<{ surface: "plans" | "credits"; message: string } | null>(null);
  const planDisplayName = snapshot?.isTrial ? t("billing.trialBadge") : snapshot?.planName ?? t("billing.sharedHint");
  const subscriptionStatusLabel = getSubscriptionStatusLabel(snapshot?.subscriptionStatus, t);
  const trialStatusText = snapshot ? getTrialStatusText(snapshot.trialDaysRemaining, t) : null;

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

  const stopCheckoutSync = useCallback(() => {
    if (checkoutSyncTimeoutRef.current !== null) {
      window.clearTimeout(checkoutSyncTimeoutRef.current);
      checkoutSyncTimeoutRef.current = null;
    }

    checkoutSyncStateRef.current = { attempts: 0, consecutiveErrors: 0 };
  }, []);

  const clearCheckoutSyncContext = useCallback(() => {
    checkoutContextRef.current = null;
    clearBillingCheckoutContext();
  }, []);

  const runCheckoutSyncAttempt = useCallback(async () => {
    if (!workspaceId) {
      stopCheckoutSync();
      setCheckoutSyncStatus("idle");
      return;
    }

    checkoutSyncStateRef.current.attempts += 1;

    const result = await refetchBillingSummary();
    const checkoutContext = checkoutContextRef.current;
    if (hasCheckoutSynced(result.data, checkoutContext)) {
      stopCheckoutSync();
      clearCheckoutSyncContext();
      setCheckoutSyncStatus("idle");
      return;
    }

    if (result.error) {
      checkoutSyncStateRef.current.consecutiveErrors += 1;
      const isServerError = result.error instanceof ApiError && result.error.status >= 500;
      const maxAttempts = checkoutContext ? 4 : 2;

      if (
        isServerError ||
        checkoutSyncStateRef.current.consecutiveErrors >= 2 ||
        checkoutSyncStateRef.current.attempts >= maxAttempts
      ) {
        stopCheckoutSync();
        setCheckoutSyncStatus("pending");
        showToast("info", t("billing.checkoutPendingTitle"), t("billing.checkoutPendingDescription"));
        return;
      }
    } else {
      checkoutSyncStateRef.current.consecutiveErrors = 0;
    }

    if (!checkoutContext && result.data) {
      stopCheckoutSync();
      clearCheckoutSyncContext();
      setCheckoutSyncStatus("idle");
      return;
    }

    if (
      checkoutSyncStateRef.current.attempts >= (checkoutContext ? 4 : 2) ||
      checkoutSyncTimeoutRef.current !== null
    ) {
      stopCheckoutSync();
      setCheckoutSyncStatus("pending");
      showToast("info", t("billing.checkoutPendingTitle"), t("billing.checkoutPendingDescription"));
      return;
    }

    checkoutSyncTimeoutRef.current = window.setTimeout(() => {
      checkoutSyncTimeoutRef.current = null;
      void runCheckoutSyncAttempt();
    }, 2200);
  }, [clearCheckoutSyncContext, refetchBillingSummary, showToast, stopCheckoutSync, t, workspaceId]);

  useEffect(() => {
    if (!checkoutReturnKey || handledCheckoutReturnRef.current === checkoutReturnKey) {
      return;
    }

    handledCheckoutReturnRef.current = checkoutReturnKey;
    const storedCheckoutContext = readBillingCheckoutContext();
    checkoutContextRef.current =
      storedCheckoutContext &&
      storedCheckoutContext.workspaceId === workspaceId &&
      (!checkoutSessionId || storedCheckoutContext.sessionId === checkoutSessionId)
        ? storedCheckoutContext
        : null;

    if (checkoutStatus === "success") {
      stopCheckoutSync();
      checkoutSyncStateRef.current = { attempts: 0, consecutiveErrors: 0 };
      setCheckoutSyncStatus("syncing");
      showToast("success", t("billing.checkoutSuccessTitle"), t("billing.checkoutSuccessDescription"));
      void runCheckoutSyncAttempt();
    } else {
      stopCheckoutSync();
      clearCheckoutSyncContext();
      setCheckoutSyncStatus("idle");
      showToast("info", t("billing.checkoutCanceledTitle"), t("billing.checkoutCanceledDescription"));
    }

    const nextSearchParams = new URLSearchParams(location.search);
    nextSearchParams.delete("checkout");
    nextSearchParams.delete("session_id");
    setSearchParams(nextSearchParams, { replace: true });
  }, [
    checkoutReturnKey,
    checkoutSessionId,
    checkoutStatus,
    clearCheckoutSyncContext,
    location.search,
    runCheckoutSyncAttempt,
    setSearchParams,
    showToast,
    stopCheckoutSync,
    t,
    workspaceId,
  ]);

  useEffect(() => {
    return () => {
      stopCheckoutSync();
    };
  }, [stopCheckoutSync]);

  useEffect(() => {
    if (checkoutSyncStatus !== "pending" || !checkoutContextRef.current) {
      return;
    }

    if (hasCheckoutSynced(billingSummaryQuery.data, checkoutContextRef.current)) {
      clearCheckoutSyncContext();
      setCheckoutSyncStatus("idle");
    }
  }, [billingSummaryQuery.data, checkoutSyncStatus, clearCheckoutSyncContext]);

  const handlePlanCheckout = async (planCode: string, priceId?: string | null) => {
    setActiveCheckoutKey(`plan:${planCode}`);
    setCheckoutError(null);
    clearBillingCheckoutContext();

    try {
      const checkoutSession = await billingCheckoutMutation.mutateAsync({ priceId: priceId ?? "" });
      if (workspaceId) {
        storeBillingCheckoutContext({
          workspaceId,
          sessionId: checkoutSession.sessionId,
          kind: "plan",
          targetCode: planCode,
          baselinePlanCode: currentPlanCode,
          baselineAvailableCredits: billingSummaryQuery.data?.balance.availableCredits ?? null,
          baselineBalanceUpdatedAtUtc: billingSummaryQuery.data?.balance.updatedAtUtc ?? null,
          createdAtUtc: new Date().toISOString(),
        });
      }
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
    clearBillingCheckoutContext();

    try {
      const checkoutSession = await billingCheckoutMutation.mutateAsync({ priceId: priceId ?? "" });
      if (workspaceId) {
        storeBillingCheckoutContext({
          workspaceId,
          sessionId: checkoutSession.sessionId,
          kind: "credits",
          targetCode: creditPackCode,
          baselinePlanCode: currentPlanCode,
          baselineAvailableCredits: billingSummaryQuery.data?.balance.availableCredits ?? null,
          baselineBalanceUpdatedAtUtc: billingSummaryQuery.data?.balance.updatedAtUtc ?? null,
          createdAtUtc: new Date().toISOString(),
        });
      }
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

      {checkoutSyncStatus !== "idle" ? (
        <Card className={checkoutSyncStatus === "syncing" ? "border-primary/18 bg-primary/[0.05]" : "border-amber-500/20 bg-amber-500/[0.06]"}>
          <CardContent className="flex items-start gap-3 p-4">
            <div className={`mt-0.5 flex size-10 shrink-0 items-center justify-center rounded-[18px] border ${checkoutSyncStatus === "syncing" ? "border-primary/14 bg-primary/10 text-primary" : "border-amber-500/18 bg-background/82 text-amber-700 dark:text-amber-300"}`}>
              {checkoutSyncStatus === "syncing" ? <LoaderCircle className="size-4 animate-spin" /> : <Sparkles className="size-4" />}
            </div>
            <div className="space-y-1">
              <div className="text-sm font-semibold text-foreground">
                {checkoutSyncStatus === "syncing" ? t("billing.checkoutSyncTitle") : t("billing.checkoutPendingTitle")}
              </div>
              <p className="text-sm text-muted-foreground">
                {checkoutSyncStatus === "syncing" ? t("billing.checkoutSyncDescription") : t("billing.checkoutPendingDescription")}
              </p>
            </div>
          </CardContent>
        </Card>
      ) : null}

      {isBillingSummaryDegraded ? (
        <Card className="border-amber-500/20 bg-amber-500/[0.06]">
          <CardContent className="flex items-start gap-3 p-4">
            <div className="mt-0.5 flex size-10 shrink-0 items-center justify-center rounded-[18px] border border-amber-500/18 bg-background/82 text-amber-700 dark:text-amber-300">
              <Sparkles className="size-4" />
            </div>
            <div className="space-y-1">
              <div className="text-sm font-semibold text-foreground">{t("billing.billingDegradedTitle")}</div>
              <p className="text-sm text-muted-foreground">{t("billing.billingDegradedDescription")}</p>
            </div>
          </CardContent>
        </Card>
      ) : null}

      <section className="grid gap-3 xl:grid-cols-[1.05fr_0.95fr]">
        <Card className="border-border/82 bg-card/88">
          <CardHeader>
            <div className="space-y-1">
              <CardTitle>{t("billing.summaryTitle")}</CardTitle>
              <p className="text-sm text-muted-foreground/95">{t("billing.summaryDescription")}</p>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            {billingSummaryQuery.isLoading && !snapshot ? (
              <LoadingState title={t("common.labels.loading")} description={t("billing.sharedHint")} />
            ) : !snapshot ? (
              <ErrorState
                title={t("common.labels.somethingWentWrong")}
                description={t("billing.sharedHint")}
                onRetry={() => billingSummaryQuery.refetch()}
              />
            ) : (
              <>
                <div className="flex min-w-0 flex-wrap items-center gap-2">
                  <Badge variant="secondary">{planDisplayName}</Badge>
                  <Badge variant="secondary">{subscriptionStatusLabel}</Badge>
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

                  <div className="mt-4 grid gap-3 sm:grid-cols-2">
                    <div className="rounded-[18px] border border-border/75 bg-background/84 px-3 py-2.5">
                      <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">{t("billing.currentPlan")}</div>
                      <div className="mt-1 text-base font-semibold tracking-[-0.03em] text-foreground">{planDisplayName}</div>
                    </div>
                    <div className="rounded-[18px] border border-border/75 bg-background/84 px-3 py-2.5">
                      <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">{t("billing.subscriptionStatusLabel")}</div>
                      <div className="mt-1 text-base font-semibold tracking-[-0.03em] text-foreground">{subscriptionStatusLabel}</div>
                    </div>
                  </div>
                  <WorkspaceCreditProgress className="mt-3" ratio={snapshot.remainingRatio} tone={snapshot.tone} />
                  {snapshot.isTrial && trialStatusText ? (
                    <p className="mt-3 text-sm font-medium text-primary">{trialStatusText}</p>
                  ) : null}
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
                      const isTrialPlan =
                        currentSubscriptionStatus === "Trial" &&
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
                                {!isCurrentPlan && isTrialPlan ? <Badge variant="secondary">{t("billing.trialBadge")}</Badge> : null}
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

function hasCheckoutSynced(summary: WorkspaceBillingSummary | undefined, checkoutContext: BillingCheckoutContext | null) {
  if (!summary) {
    return false;
  }

  if (!checkoutContext) {
    return true;
  }

  if (checkoutContext.kind === "plan") {
    const planCode = summary.billingPlan?.code?.toLowerCase() ?? null;
    const subscriptionStatus = summary.subscriptionStatus ?? summary.subscription?.status ?? null;
    const trialEndsAtUtc = summary.trialEndsAtUtc ?? summary.subscription?.trialEndsAtUtc ?? null;

    return subscriptionStatus === "Active" &&
      !trialEndsAtUtc &&
      planCode === checkoutContext.targetCode.toLowerCase();
  }

  if (
    typeof checkoutContext.baselineAvailableCredits === "number" &&
    summary.balance.availableCredits > checkoutContext.baselineAvailableCredits
  ) {
    return true;
  }

  if (
    checkoutContext.baselineBalanceUpdatedAtUtc &&
    summary.balance.updatedAtUtc !== checkoutContext.baselineBalanceUpdatedAtUtc
  ) {
    return true;
  }

  return checkoutContext.baselineAvailableCredits === null && checkoutContext.baselineBalanceUpdatedAtUtc === null;
}

function getSubscriptionStatusLabel(
  status: string | null | undefined,
  t: ReturnType<typeof useI18n>["t"],
) {
  switch (status?.trim().toLowerCase()) {
    case "trial":
      return t("billing.trialBadge");
    case "active":
      return t("billing.activeState");
    case "canceled":
      return t("billing.canceledState");
    case "expired":
      return t("billing.expiredState");
    default:
      return status ?? t("common.helper.none");
  }
}

function getTrialStatusText(
  remainingDays: number | null | undefined,
  t: ReturnType<typeof useI18n>["t"],
) {
  if (remainingDays === null || remainingDays === undefined) {
    return t("billing.trialActiveDescription");
  }

  if (remainingDays <= 0) {
    return t("billing.trialEndsToday");
  }

  return t("billing.trialEndsInDays", { count: remainingDays });
}
