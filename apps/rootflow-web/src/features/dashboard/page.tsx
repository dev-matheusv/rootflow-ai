import { BookOpenText, Bot, Coins, CreditCard, DatabaseZap, MessagesSquare } from "lucide-react";
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
import { useConversationsQuery, useDocumentsQuery, useHealthQuery, useWorkspaceBillingSummaryQuery } from "@/hooks/use-rootflow-data";
import { formatCredits, getWorkspaceCreditSnapshot } from "@/lib/billing/workspace-credits";
import { formatRelativeDate } from "@/lib/formatting/formatters";

export function DashboardPage() {
  const { session } = useAuth();
  const { locale, t } = useI18n();
  const healthQuery = useHealthQuery();
  const documentsQuery = useDocumentsQuery({ autoRefreshProcessing: true });
  const conversationsQuery = useConversationsQuery();
  const billingSummaryQuery = useWorkspaceBillingSummaryQuery(session?.workspace.id);

  const documents = documentsQuery.data ?? [];
  const conversations = conversationsQuery.data ?? [];
  const processedCount = documents.filter((document) => document.status === 3).length;
  const processingCount = documents.filter((document) => document.status === 2).length;
  const failedCount = documents.filter((document) => document.status === 4).length;
  const latestConversation = conversations[0];
  const latestDocument = [...documents].sort(
    (left, right) =>
      new Date(right.processedAtUtc ?? right.createdAtUtc).getTime() -
      new Date(left.processedAtUtc ?? left.createdAtUtc).getTime(),
  )[0];
  const creditSnapshot = getWorkspaceCreditSnapshot(billingSummaryQuery.data);
  const creditPlanLabel = creditSnapshot?.isTrial
    ? t("billing.trialBadge")
    : creditSnapshot?.planName ?? null;
  const creditStatusLabel = creditSnapshot?.isTrial
    ? t("billing.trialBadge")
    : creditSnapshot?.tone === "healthy"
      ? t("billing.healthyState")
      : creditSnapshot?.tone === "low"
        ? t("billing.lowState")
        : creditSnapshot?.tone === "critical"
          ? t("billing.criticalState")
          : creditSnapshot?.tone === "inactive"
            ? t("billing.inactiveState")
            : t("billing.emptyState");
  const trialStatusText = creditSnapshot?.isTrial
    ? creditSnapshot.trialDaysRemaining && creditSnapshot.trialDaysRemaining > 0
      ? t("billing.trialEndsInDays", { count: creditSnapshot.trialDaysRemaining })
      : t("billing.trialEndsToday")
    : null;
  const creditStatusClassName =
    creditSnapshot?.isTrial
      ? "border-primary/24 bg-primary/[0.12] text-primary"
      : creditSnapshot?.tone === "healthy"
      ? undefined
      : creditSnapshot?.tone === "low"
        ? undefined
        : creditSnapshot?.tone === "inactive"
          ? "border-border/78 bg-background/84 text-muted-foreground"
          : "border-rose-500/24 bg-rose-500/[0.12] text-rose-700 dark:text-rose-300";

  const metrics = [
    { label: t("dashboard.documentsMetric"), value: String(documents.length), note: t("dashboard.total"), hint: t("common.helper.librarySize"), icon: BookOpenText },
    { label: t("dashboard.readyMetric"), value: String(processedCount), note: t("dashboard.processed"), hint: t("common.helper.groundedAnswers"), icon: Bot },
    { label: t("dashboard.conversationsMetric"), value: String(conversations.length), note: t("dashboard.saved"), hint: t("common.helper.reusableSessions"), icon: MessagesSquare },
    {
      label: t("dashboard.apiMetric"),
      value: healthQuery.data?.status === "healthy" ? t("dashboard.healthy") : t("dashboard.checking"),
      note: t("dashboard.status"),
      hint: t("common.helper.searchPipeline"),
      icon: DatabaseZap,
    },
  ] as const;
  const roleLabel =
    session?.role === "Owner"
      ? t("common.labels.owner")
      : session?.role === "Admin"
        ? t("common.labels.admin")
        : t("common.labels.member");

  return (
    <div className="space-y-5">
      <PageHeader
        title={t("dashboard.title")}
        description={t("dashboard.description")}
        actions={
          <>
            <Button asChild>
              <Link to="/assistant">{t("common.actions.openAssistant")}</Link>
            </Button>
            <Button variant="outline" asChild>
              <Link to="/knowledge-base">{t("common.actions.reviewDocuments")}</Link>
            </Button>
          </>
        }
      />

      <section className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        {documentsQuery.isLoading || healthQuery.isLoading || conversationsQuery.isLoading ? (
          <div className="md:col-span-2 xl:col-span-4">
            <LoadingState
              title={t("dashboard.loadingTitle")}
              description={t("dashboard.loadingDescription")}
            />
          </div>
        ) : documentsQuery.isError || healthQuery.isError || conversationsQuery.isError ? (
          <div className="md:col-span-2 xl:col-span-4">
            <ErrorState
              title={t("dashboard.errorTitle")}
              description={t("dashboard.errorDescription")}
              onRetry={() => {
                void documentsQuery.refetch();
                void healthQuery.refetch();
                void conversationsQuery.refetch();
              }}
            />
          </div>
        ) : (
          metrics.map((metric) => {
            const Icon = metric.icon;
            const isPrimaryMetric = metric.label === t("dashboard.readyMetric");

            return (
              <Card
                key={metric.label}
                className={isPrimaryMetric ? "border-primary/18 bg-primary/[0.06]" : "border-border/80 bg-card/86"}
              >
                <CardContent className="space-y-3 p-4">
                  <div className="flex items-center justify-between gap-4">
                    <div className={`flex size-9 items-center justify-center rounded-2xl ${isPrimaryMetric ? "bg-primary/14 text-primary" : "bg-primary/10 text-primary"}`}>
                      <Icon className="size-5" />
                    </div>
                    <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">{metric.note}</div>
                  </div>
                  <div className="space-y-1.5">
                    <div className="text-sm font-medium text-foreground/82">{metric.label}</div>
                    <div className="font-display text-[1.9rem] font-semibold tracking-[-0.05em] text-foreground">{metric.value}</div>
                    <div className="text-xs text-muted-foreground">
                      {metric.label === t("dashboard.documentsMetric")
                        ? latestDocument
                          ? t("common.labels.latest", { time: formatRelativeDate(latestDocument.processedAtUtc ?? latestDocument.createdAtUtc, locale) })
                          : t("common.helper.noUploadsYet")
                        : metric.label === t("dashboard.readyMetric")
                          ? processingCount > 0
                            ? t("dashboard.processingCount", { count: processingCount })
                            : t("common.helper.upToDate")
                          : metric.label === t("dashboard.conversationsMetric")
                            ? latestConversation
                              ? t("common.labels.updated", { time: formatRelativeDate(latestConversation.updatedAtUtc, locale) })
                              : t("common.helper.noSessionsYet")
                            : healthQuery.data?.status === "healthy"
                              ? t("common.helper.connected")
                              : t("common.helper.waitingOnCheck")}
                    </div>
                    <div className="text-[12px] font-medium text-muted-foreground/90">{metric.hint}</div>
                  </div>
                </CardContent>
              </Card>
            );
          })
        )}
      </section>

      <div className="flex min-w-0 flex-wrap items-center gap-2 rounded-[20px] border border-border/78 bg-card/70 px-4 py-3 text-sm text-muted-foreground shadow-[0_18px_34px_-30px_rgba(16,36,71,0.12)]">
        <span className="font-medium text-foreground">{t("dashboard.pipeline")}</span>
        <Badge variant="secondary">{t("dashboard.upload")}</Badge>
        <Badge variant="secondary">{t("common.actions.askAssistant")}</Badge>
        <Badge variant="secondary">{t("dashboard.reviewSources")}</Badge>
      </div>

      <section className="grid gap-3 xl:grid-cols-[1.05fr_0.95fr]">
        <Card className="border-border/80 bg-card/86">
          <CardHeader>
            <div className="space-y-1">
              <CardTitle>{t("dashboard.workspaceTitle")}</CardTitle>
              <p className="text-sm text-muted-foreground/95">{t("dashboard.workspaceDescription")}</p>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex min-w-0 flex-wrap items-center gap-2">
              <Badge variant="secondary" className="min-w-0 truncate" title={session?.workspace.name}>
                {session?.workspace.name ?? t("common.labels.workspace")}
              </Badge>
              <Badge variant="secondary" className="min-w-0 max-w-full truncate" title={session?.workspace.slug}>
                @{session?.workspace.slug ?? "workspace"}
              </Badge>
              <Badge variant="secondary">{roleLabel}</Badge>
            </div>

            <div className="rounded-[22px] border border-border/82 bg-card/76">
              <div className="flex items-center justify-between gap-3 px-4 py-3">
                <div className="text-sm font-semibold text-foreground">{t("common.labels.status")}</div>
                <Badge variant={failedCount > 0 ? "warning" : healthQuery.data?.status === "healthy" ? "success" : "secondary"}>
                  {failedCount > 0 ? t("dashboard.failedLabel", { count: failedCount }) : healthQuery.data?.status === "healthy" ? t("dashboard.healthy") : t("dashboard.checking")}
                </Badge>
              </div>
              <div className="divide-y divide-border/70">
                <div className="flex min-w-0 items-center justify-between gap-3 px-4 py-3 text-sm">
                  <span className="text-muted-foreground">{t("dashboard.latestDocument")}</span>
                  <span className="min-w-0 max-w-[12rem] truncate text-right text-foreground" title={latestDocument?.originalFileName}>
                    {latestDocument?.originalFileName ?? t("common.helper.none")}
                  </span>
                </div>
                <div className="flex min-w-0 items-center justify-between gap-3 px-4 py-3 text-sm">
                  <span className="text-muted-foreground">{t("dashboard.latestConversation")}</span>
                  <span className="min-w-0 max-w-[12rem] truncate text-right text-foreground" title={latestConversation?.title}>
                    {latestConversation?.title ?? t("common.helper.none")}
                  </span>
                </div>
                <div className="flex min-w-0 items-center justify-between gap-3 px-4 py-3 text-sm">
                  <span className="text-muted-foreground">{t("common.labels.updatedLabel")}</span>
                  <span className="text-foreground">
                    {latestConversation ? formatRelativeDate(latestConversation.updatedAtUtc, locale) : t("common.helper.noActivity")}
                  </span>
                </div>
                <div className="flex min-w-0 items-center justify-between gap-3 px-4 py-3 text-sm">
                  <span className="text-muted-foreground">{t("dashboard.processingState")}</span>
                  <span className="text-foreground">
                    {processingCount === 0 ? t("common.helper.clear") : t("dashboard.activeProcessing", { count: processingCount })}
                  </span>
                </div>
              </div>
            </div>

            {documents.length === 0 ? (
              <div className="rounded-[20px] border border-dashed border-border/82 bg-card/60 px-4 py-3 text-sm text-muted-foreground">
                {t("dashboard.uploadFirstDocumentHint")}
              </div>
            ) : null}
          </CardContent>
        </Card>

        <div className="space-y-3">
          <Card className="border-border/80 bg-card/86">
            <CardHeader className="pb-3">
              <div className="space-y-1">
                <CardTitle>{t("billing.summaryTitle")}</CardTitle>
                <p className="text-sm text-muted-foreground/95">{t("billing.summaryDescription")}</p>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              {billingSummaryQuery.isLoading ? (
                <div className="animate-pulse space-y-3 rounded-[22px] border border-border/80 bg-card/76 p-4">
                  <div className="h-3 w-28 rounded-full bg-border/70" />
                  <div className="h-7 w-36 rounded-full bg-border/70" />
                  <div className="h-2 rounded-full bg-border/70" />
                </div>
              ) : !creditSnapshot ? (
                <ErrorState
                  title={t("common.labels.somethingWentWrong")}
                  description={t("billing.sharedHint")}
                  onRetry={() => billingSummaryQuery.refetch()}
                />
              ) : (
                <>
                  <div className="rounded-[22px] border border-border/82 bg-card/76 p-4">
                    <div className="flex min-w-0 flex-wrap items-center justify-between gap-3">
                      <div className="min-w-0">
                        <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">{t("billing.shellLabel")}</div>
                        <div className="mt-1 text-[1.65rem] font-semibold tracking-[-0.05em] text-foreground">
                          {formatCredits(creditSnapshot.availableCredits, locale)}
                        </div>
                        <div className="mt-1 text-sm text-muted-foreground">{t("billing.availableShort", { count: formatCredits(creditSnapshot.availableCredits, locale) })}</div>
                      </div>
                      <div className="flex min-w-0 flex-wrap items-center gap-2">
                        {creditPlanLabel ? <Badge variant="secondary">{creditPlanLabel}</Badge> : null}
                        <Badge
                          variant={creditSnapshot.tone === "healthy" ? "success" : creditSnapshot.tone === "low" ? "warning" : "secondary"}
                          className={creditStatusClassName}
                        >
                          {creditStatusLabel}
                        </Badge>
                      </div>
                    </div>

                    <div className="mt-4 grid gap-3 sm:grid-cols-3">
                      <div className="rounded-[18px] border border-border/76 bg-background/84 px-3 py-2.5">
                        <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">{t("billing.available")}</div>
                        <div className="mt-1 text-base font-semibold tracking-[-0.03em] text-foreground">{formatCredits(creditSnapshot.availableCredits, locale)}</div>
                      </div>
                      <div className="rounded-[18px] border border-border/76 bg-background/84 px-3 py-2.5">
                        <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">{t("billing.used")}</div>
                        <div className="mt-1 text-base font-semibold tracking-[-0.03em] text-foreground">{formatCredits(creditSnapshot.consumedCredits, locale)}</div>
                      </div>
                      <div className="rounded-[18px] border border-border/76 bg-background/84 px-3 py-2.5">
                        <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">{t("billing.tracked")}</div>
                        <div className="mt-1 text-base font-semibold tracking-[-0.03em] text-foreground">{formatCredits(creditSnapshot.totalTrackedCredits, locale)}</div>
                      </div>
                    </div>

                    <div className="mt-4 flex items-center justify-between gap-3 text-sm text-muted-foreground">
                      <span>{t("billing.remainingShort", { percent: creditSnapshot.remainingPercent })}</span>
                      <span>{creditPlanLabel ?? t("billing.currentPlan")}</span>
                    </div>
                    <WorkspaceCreditProgress className="mt-2.5" ratio={creditSnapshot.remainingRatio} tone={creditSnapshot.tone} />
                    {trialStatusText ? <p className="mt-3 text-sm font-medium text-primary">{trialStatusText}</p> : null}
                    <p className="mt-3 text-sm text-muted-foreground">
                      {creditSnapshot.tone === "low"
                        ? t("billing.lowWarning")
                        : creditSnapshot.tone === "critical"
                          ? t("billing.criticalWarning")
                          : creditSnapshot.tone === "inactive"
                            ? t("billing.inactiveWarning")
                            : creditSnapshot.tone === "empty"
                              ? t("billing.emptyWarning")
                              : t("billing.dashboardHint")}
                    </p>
                  </div>

                  <div className="grid gap-2 sm:grid-cols-2">
                    <Button className="justify-between" asChild>
                      <Link to="/billing">
                        <span>{t("common.actions.buyCredits")}</span>
                        <Coins className="size-4" />
                      </Link>
                    </Button>
                    <Button variant="outline" className="justify-between" asChild>
                      <Link to="/billing">
                        <span>{t("common.actions.upgradePlan")}</span>
                        <CreditCard className="size-4" />
                      </Link>
                    </Button>
                  </div>
                </>
              )}
            </CardContent>
          </Card>

          <Card className="border-border/80 bg-card/86">
            <CardHeader>
              <div className="space-y-1">
                <CardTitle>{t("common.labels.next")}</CardTitle>
                <p className="text-sm text-muted-foreground/95">{t("dashboard.nextDescription")}</p>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="rounded-[22px] border border-border/82 bg-card/76 p-3.5">
                <div className="flex min-w-0 flex-wrap gap-2">
                  <Badge variant="secondary">{t("dashboard.docsChip", { count: documents.length })}</Badge>
                  <Badge variant="secondary">{t("dashboard.sessionsChip", { count: conversations.length })}</Badge>
                  <Badge variant="secondary">{t("dashboard.readyChip", { count: processedCount })}</Badge>
                </div>
              </div>
              <div className="flex min-w-0 flex-wrap gap-2">
                <Badge variant="secondary">{t("dashboard.uploadFreshDocs")}</Badge>
                <Badge variant="secondary">{t("dashboard.askSpecificQuestions")}</Badge>
                <Badge variant="secondary">{t("dashboard.shareCitedAnswers")}</Badge>
              </div>
              <Button variant="outline" className="w-full justify-between" asChild>
                <Link to="/knowledge-base">{t("common.actions.openDocuments")}</Link>
              </Button>
              <Button variant="outline" className="w-full justify-between" asChild>
                <Link to="/assistant">{t("common.actions.askAssistant")}</Link>
              </Button>
              <Button variant="outline" className="w-full justify-between" asChild>
                <Link to="/conversations">{t("common.actions.openConversations")}</Link>
              </Button>
            </CardContent>
          </Card>
        </div>
      </section>
    </div>
  );
}
