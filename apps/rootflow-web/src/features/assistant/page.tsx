import { zodResolver } from "@hookform/resolvers/zod";
import { Bot, Coins, CornerDownLeft, LoaderCircle, Microscope, Quote, SendHorizonal, TriangleAlert } from "lucide-react";
import { type KeyboardEvent, useMemo, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { Link, useSearchParams } from "react-router-dom";
import { z } from "zod";

import { useI18n } from "@/app/providers/i18n-provider";
import { WorkspaceCreditProgress } from "@/components/billing/workspace-credit-progress";
import { ErrorState } from "@/components/feedback/error-state";
import { LoadingState } from "@/components/feedback/loading-state";
import { FormattedAnswer } from "@/components/chat/formatted-answer";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { Textarea } from "@/components/ui/textarea";
import { useAuth } from "@/features/auth/auth-provider";
import { useAskQuestionMutation, useConversationQuery, useDocumentsQuery, useWorkspaceBillingSummaryQuery } from "@/hooks/use-rootflow-data";
import type { ChatAnswer } from "@/lib/api/contracts";
import { getApiErrorCode } from "@/lib/api/client";
import { formatCredits, getWorkspaceCreditSnapshot } from "@/lib/billing/workspace-credits";
import { formatRelativeDate } from "@/lib/formatting/formatters";

type AssistantFormValues = {
  question: string;
};

export function AssistantPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const conversationId = searchParams.get("conversationId");
  const [latestAnswer, setLatestAnswer] = useState<ChatAnswer | null>(null);
  const [showDebug, setShowDebug] = useState(false);
  const { locale, t } = useI18n();
  const { session } = useAuth();
  const workspaceId = session?.workspace.id;
  const assistantSchema = useMemo(
    () =>
      z.object({
        question: z.string().trim().min(3, t("assistant.validationQuestionMin")),
      }),
    [t],
  );

  const documentsQuery = useDocumentsQuery({ autoRefreshProcessing: true });
  const conversationQuery = useConversationQuery(conversationId);
  const billingSummaryQuery = useWorkspaceBillingSummaryQuery(workspaceId);
  const askQuestionMutation = useAskQuestionMutation(workspaceId);

  const form = useForm<AssistantFormValues>({
    resolver: zodResolver(assistantSchema),
    mode: "onChange",
    defaultValues: {
      question: "",
    },
  });

  const question = useWatch({
    control: form.control,
    name: "question",
  }) ?? "";
  const messages = useMemo(() => conversationQuery.data?.messages ?? [], [conversationQuery.data?.messages]);
  const documents = useMemo(() => documentsQuery.data ?? [], [documentsQuery.data]);
  const readyDocumentCount = documentsQuery.data?.filter((document) => document.status === 3).length ?? 0;
  const latestReadyDocument = useMemo(
    () =>
      [...documents]
        .filter((document) => document.status === 3)
        .sort(
          (left, right) =>
            new Date(right.processedAtUtc ?? right.createdAtUtc).getTime() -
            new Date(left.processedAtUtc ?? left.createdAtUtc).getTime(),
        )[0],
    [documents],
  );
  const latestReadyUpdatedLabel = latestReadyDocument
    ? formatRelativeDate(latestReadyDocument.processedAtUtc ?? latestReadyDocument.createdAtUtc, locale)
    : null;
  const latestAssistantMessageId = useMemo(
    () => [...messages].reverse().find((message) => message.role !== 2)?.id ?? null,
    [messages],
  );
  const suggestedPrompts = latestReadyDocument
    ? [
        { label: t("assistant.summariseDocuments"), prompt: t("assistant.summariseDocuments") },
        { label: t("assistant.keyTopics"), prompt: t("assistant.keyTopics") },
        { label: t("assistant.explainLatestFile"), prompt: t("assistant.latestFilePrompt", { fileName: latestReadyDocument.originalFileName }) },
        { label: t("assistant.whatChanged"), prompt: t("assistant.changedPrompt", { fileName: latestReadyDocument.originalFileName }) },
      ]
    : [
        { label: t("assistant.summariseDocuments"), prompt: t("assistant.summariseDocuments") },
        { label: t("assistant.keyTopics"), prompt: t("assistant.keyTopics") },
        { label: t("assistant.explainThisDocument"), prompt: t("assistant.explainThisDocument") },
        { label: t("assistant.whatChanged"), prompt: t("assistant.whatChanged") },
      ];
  const activeLatestAnswer = latestAnswer?.conversationId === conversationId ? latestAnswer : null;
  const isSendingQuestion = askQuestionMutation.isPending || form.formState.isSubmitting;
  const canReviewRetrieval = Boolean(activeLatestAnswer?.debug?.retrievedChunks?.length);
  const isDebugVisible = showDebug && canReviewRetrieval;
  const creditSnapshot = useMemo(
    () => getWorkspaceCreditSnapshot(billingSummaryQuery.data),
    [billingSummaryQuery.data],
  );
  const billingErrorCode = getApiErrorCode(askQuestionMutation.error);
  const isInactiveSubscription = billingErrorCode === "inactive_subscription" || creditSnapshot?.tone === "inactive";
  const isOutOfCredits = billingErrorCode === "insufficient_credits" || creditSnapshot?.tone === "empty";
  const isBillingBlocked = isInactiveSubscription || isOutOfCredits;
  const inlineCreditWarning =
    !isBillingBlocked && creditSnapshot?.tone === "critical"
      ? t("billing.assistantCriticalHint")
      : !isBillingBlocked && creditSnapshot?.tone === "low"
        ? t("billing.assistantLowHint")
        : null;
  const creditPlanLabel = creditSnapshot?.isTrial
    ? t("billing.trialBadge")
    : creditSnapshot?.planName ?? t("billing.sharedHint");
  const canAsk = question.trim().length >= 3 && !isSendingQuestion && readyDocumentCount > 0 && !isBillingBlocked;

  const submitQuestion = form.handleSubmit(async (values) => {
    if (askQuestionMutation.isPending) {
      return;
    }

    const answer = await askQuestionMutation.mutateAsync({
      question: values.question,
      conversationId,
      maxContextChunks: 5,
    });

    setSearchParams({ conversationId: answer.conversationId });
    setLatestAnswer(answer);
    setShowDebug(false);
    form.reset();
  });

  const handleQuestionKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key !== "Enter" || event.shiftKey) {
      return;
    }

    event.preventDefault();

    if (!canAsk || event.nativeEvent.isComposing) {
      return;
    }

    void submitQuestion();
  };

  const handleNewSession = () => {
    setSearchParams({}, { replace: true });
    setLatestAnswer(null);
    setShowDebug(false);
    askQuestionMutation.reset();
    form.reset();
  };

  const handlePromptSelect = (prompt: string) => {
    form.setValue("question", prompt, { shouldDirty: true, shouldTouch: true, shouldValidate: true });
  };

  return (
    <div className="space-y-5">
      <PageHeader
        title={t("assistant.title")}
        description={t("assistant.description")}
        actions={
          <>
            <Button onClick={handleNewSession}>
              <Bot />
              {t("common.actions.newSession")}
            </Button>
            <Button
              variant="outline"
              onClick={() => setShowDebug((value) => !value)}
              disabled={!canReviewRetrieval}
            >
              <Microscope />
              {isDebugVisible ? t("common.actions.hideRetrieval") : t("common.actions.reviewRetrieval")}
            </Button>
          </>
        }
      />

      <section className="grid min-w-0 gap-3 xl:grid-cols-[1.22fr_0.78fr]">
        <div className="min-w-0 space-y-3">
          <Card className="border-border/90 bg-card/92">
            <CardHeader className="pb-3">
              <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                <div className="min-w-0 space-y-1.5">
                  <CardTitle>{conversationId ? t("assistant.sessionTitle") : t("assistant.newSessionTitle")}</CardTitle>
                  <p className="text-sm text-muted-foreground/95">
                    {readyDocumentCount > 0 ? t("assistant.readyHint") : t("assistant.uploadHint")}
                  </p>
                </div>
                <div className="flex min-w-0 flex-wrap items-center gap-2">
                  <Badge variant="secondary">{t("dashboard.readyChip", { count: readyDocumentCount })}</Badge>
                  {latestReadyDocument ? (
                    <Badge variant="secondary" className="min-w-0 max-w-full sm:max-w-[220px] truncate" title={latestReadyDocument.originalFileName}>
                      {latestReadyDocument.originalFileName}
                    </Badge>
                  ) : null}
                </div>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              {documentsQuery.isLoading ? (
                <LoadingState title={t("assistant.loadingDocumentsTitle")} description={t("assistant.loadingDocumentsDescription")} />
              ) : documentsQuery.isError ? (
                <ErrorState
                  title={t("assistant.documentsErrorTitle")}
                  description={t("assistant.documentsErrorDescription")}
                  onRetry={() => documentsQuery.refetch()}
                />
              ) : readyDocumentCount > 0 ? (
                <>
                  {conversationId && conversationQuery.isLoading ? (
                    <LoadingState title={t("assistant.loadingConversationTitle")} description={t("assistant.loadingConversationDescription")} />
                  ) : conversationId && conversationQuery.isError ? (
                    <ErrorState
                      title={t("assistant.conversationErrorTitle")}
                      description={t("assistant.conversationErrorDescription")}
                      onRetry={() => conversationQuery.refetch()}
                    />
                  ) : messages.length === 0 ? (
                    <div className="space-y-4 pb-1">
                      <div className="rounded-[24px] border border-border/88 bg-[linear-gradient(180deg,color-mix(in_srgb,var(--card)_96%,transparent),color-mix(in_srgb,var(--background)_74%,transparent))] p-4 shadow-[0_20px_42px_-32px_rgba(18,72,166,0.18)]">
                        <div className="flex flex-wrap items-center justify-between gap-3">
                          <div className="min-w-0">
                            <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-primary/76">{t("assistant.sessionContext")}</div>
                            <div className="mt-1 text-sm font-semibold text-foreground">
                              {conversationId ? t("common.helper.sessionRestored") : t("assistant.sessionReady")}
                            </div>
                            <div className="mt-1 text-sm text-muted-foreground/95">{t("assistant.sessionReadyHint")}</div>
                          </div>
                          {latestReadyUpdatedLabel ? (
                            <div className="text-sm font-medium text-foreground/88">{t("common.labels.lastUpdated", { time: latestReadyUpdatedLabel })}</div>
                          ) : null}
                        </div>
                        <div className="mt-4 flex min-w-0 flex-wrap gap-2">
                          <Badge variant="secondary" className="gap-1.5 px-3 py-1.5 text-[11px]">
                            {t("dashboard.readyChip", { count: readyDocumentCount })}
                          </Badge>
                          <Badge
                            variant="secondary"
                            className="min-w-0 max-w-full truncate gap-1.5 px-3 py-1.5 text-[11px]"
                            title={latestReadyDocument?.originalFileName}
                          >
                            {t("common.labels.last")}: {latestReadyDocument?.originalFileName ?? t("common.helper.none")}
                          </Badge>
                        </div>
                        <div className="mt-4 border-t border-border/75 pt-4">
                          <div className="mb-3 text-sm font-semibold text-foreground">{t("assistant.quickActions")}</div>
                          <div className="flex min-w-0 flex-wrap gap-2">
                            {suggestedPrompts.map((prompt) => (
                              <Button
                                key={prompt.label}
                                type="button"
                                variant="outline"
                                size="sm"
                                className="h-auto min-w-0 max-w-full overflow-hidden rounded-full border-border/85 bg-background/92 px-3.5 py-1.5 text-left"
                                onClick={() => handlePromptSelect(prompt.prompt)}
                                title={prompt.prompt}
                              >
                                <span className="truncate">{prompt.label}</span>
                              </Button>
                            ))}
                          </div>
                        </div>
                      </div>
                    </div>
                  ) : (
                    <div className="space-y-4">
                      {messages.map((message) => {
                        const isUser = message.role === 2;

                        return (
                          <div key={message.id} className={`flex ${isUser ? "justify-end" : "justify-start"}`}>
                            <article
                              className={`min-w-0 max-w-[76ch] motion-safe:animate-[rf-fade-up_260ms_cubic-bezier(0.22,1,0.36,1)] transition-[transform,box-shadow,border-color,background-color] duration-200 ${
                                isUser
                                  ? "rounded-[24px] rounded-br-lg border border-primary/15 bg-primary/10 px-5 py-4 text-foreground shadow-[0_18px_38px_-28px_rgba(66,116,194,0.22)] dark:border-primary/25 dark:bg-primary/20"
                                  : message.id === latestAssistantMessageId
                                    ? "rounded-[22px] border border-primary/24 bg-[linear-gradient(180deg,color-mix(in_srgb,var(--primary)_7%,var(--card)),color-mix(in_srgb,var(--primary)_4%,var(--background)))] px-5 py-4 text-foreground shadow-[0_20px_42px_-30px_rgba(37,99,235,0.2)]"
                                    : "rounded-[22px] border border-border/82 bg-[linear-gradient(180deg,color-mix(in_srgb,var(--card)_90%,transparent),color-mix(in_srgb,var(--background)_76%,transparent))] px-5 py-4 text-foreground shadow-[0_16px_32px_-30px_rgba(16,36,71,0.14)]"
                              }`}
                            >
                              <div
                                className={`mb-3 text-[11px] font-semibold uppercase tracking-[0.18em] ${
                                  isUser ? "text-primary/80" : "text-primary/75"
                                }`}
                              >
                                {isUser ? t("common.labels.you") : "RootFlow"}
                              </div>
                              {isUser ? (
                                <p className="whitespace-pre-wrap text-[0.96rem] leading-7 text-inherit [overflow-wrap:anywhere]">{message.content}</p>
                              ) : (
                                <FormattedAnswer content={message.content} className="max-w-[72ch] [overflow-wrap:anywhere]" />
                              )}
                            </article>
                          </div>
                        );
                      })}
                    </div>
                  )}

                  {askQuestionMutation.isPending ? (
                    <div className="rounded-[22px] border border-primary/18 bg-primary/[0.05] p-4 motion-safe:animate-[rf-fade-up_220ms_cubic-bezier(0.22,1,0.36,1)]">
                      <div className="mb-2 flex items-center gap-2 text-[11px] font-semibold uppercase tracking-[0.18em] text-primary/90">
                        <LoaderCircle className="size-3.5 animate-spin" />
                        {t("assistant.rootflow")}
                      </div>
                      <p className="text-sm text-muted-foreground">{t("assistant.searchInProgress")}</p>
                    </div>
                  ) : null}
                </>
              ) : (
                <div className="rounded-[20px] border border-dashed border-border/82 bg-card/60 px-4 py-3 text-sm text-muted-foreground">
                  {documentsQuery.data?.length
                    ? t("assistant.uploadsProcessing")
                    : t("assistant.uploadFirstDocument")}
                </div>
              )}

              <div className="space-y-3 border-t border-border/75 pt-4">
                {inlineCreditWarning && creditSnapshot ? (
                  <div className="rounded-[22px] border border-amber-500/18 bg-amber-500/[0.08] px-4 py-3 text-sm shadow-[0_18px_34px_-28px_rgba(245,158,11,0.24)]">
                    <div className="flex min-w-0 flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                      <div className="flex min-w-0 items-start gap-3">
                        <div className="mt-0.5 flex size-9 shrink-0 items-center justify-center rounded-2xl border border-amber-500/18 bg-background/80 text-amber-700 dark:text-amber-300">
                          <TriangleAlert className="size-4" />
                        </div>
                        <div className="min-w-0">
                          <div className="text-sm font-semibold text-foreground">{inlineCreditWarning}</div>
                          <div className="mt-1 text-sm text-muted-foreground">
                            {t("billing.availableShort", { count: formatCredits(creditSnapshot.availableCredits, locale) })}
                          </div>
                        </div>
                      </div>
                      <div className="min-w-0 w-full max-w-[220px] space-y-2">
                        <div className="flex items-center justify-between gap-3 text-xs text-muted-foreground">
                          <span>{creditPlanLabel}</span>
                          <span>{t("billing.remainingShort", { percent: creditSnapshot.remainingPercent })}</span>
                        </div>
                        <WorkspaceCreditProgress ratio={creditSnapshot.remainingRatio} tone={creditSnapshot.tone} />
                      </div>
                    </div>
                  </div>
                ) : null}

                {isBillingBlocked ? (
                  <div className="space-y-4 rounded-[28px] border border-primary/18 bg-[linear-gradient(180deg,color-mix(in_srgb,var(--primary)_5%,var(--card)),color-mix(in_srgb,var(--background)_80%,transparent))] p-5 shadow-[0_24px_48px_-32px_rgba(37,99,235,0.18)]">
                    <div className="flex min-w-0 flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                      <div className="flex min-w-0 items-start gap-3">
                        <div className="flex size-11 shrink-0 items-center justify-center rounded-[22px] border border-primary/14 bg-primary/10 text-primary">
                          <Coins className="size-5" />
                        </div>
                        <div className="min-w-0">
                          <div className="text-sm font-semibold text-foreground">
                            {isInactiveSubscription ? t("billing.assistantInactiveTitle") : t("billing.assistantBlockedTitle")}
                          </div>
                          <p className="mt-1 text-sm text-muted-foreground/95">
                            {isInactiveSubscription ? t("billing.assistantInactiveDescription") : t("billing.assistantBlockedDescription")}
                          </p>
                        </div>
                      </div>
                      <Badge
                        className={
                          isInactiveSubscription
                            ? "border-border/78 bg-background/84 text-muted-foreground"
                            : "border-rose-500/24 bg-rose-500/[0.12] text-rose-700 dark:text-rose-300"
                        }
                      >
                        {isInactiveSubscription ? t("billing.inactiveState") : t("billing.emptyState")}
                      </Badge>
                    </div>

                    {creditSnapshot ? (
                      <div className="rounded-[24px] border border-border/80 bg-background/82 p-4">
                        <div className="flex min-w-0 flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                          <div className="min-w-0">
                            <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">{t("billing.shellLabel")}</div>
                            <div className="mt-1 text-base font-semibold tracking-[-0.02em] text-foreground">
                              {t("billing.availableShort", { count: formatCredits(creditSnapshot.availableCredits, locale) })}
                            </div>
                          </div>
                          <div className="text-sm text-muted-foreground">{t("billing.remainingDetail", { percent: creditSnapshot.remainingPercent })}</div>
                        </div>
                        <WorkspaceCreditProgress className="mt-3" ratio={creditSnapshot.remainingRatio} tone={creditSnapshot.tone} />
                        {creditSnapshot.isTrial ? (
                          <p className="mt-3 text-sm font-medium text-primary">
                            {creditSnapshot.trialDaysRemaining && creditSnapshot.trialDaysRemaining > 0
                              ? t("billing.trialEndsInDays", { count: creditSnapshot.trialDaysRemaining })
                              : t("billing.trialEndsToday")}
                          </p>
                        ) : null}
                      </div>
                    ) : null}

                    <div className="flex flex-col gap-2 sm:flex-row">
                      <Button asChild className="sm:flex-1">
                        <Link to="/billing">{isInactiveSubscription ? t("common.actions.upgradePlan") : t("common.actions.buyCredits")}</Link>
                      </Button>
                      <Button variant="outline" asChild className="sm:flex-1">
                        <Link to="/billing">{t("common.actions.openBilling")}</Link>
                      </Button>
                    </div>
                  </div>
                ) : (
                  <form
                    className="space-y-4 rounded-[28px] border border-primary/20 bg-[linear-gradient(180deg,color-mix(in_srgb,var(--primary)_4%,var(--card)),color-mix(in_srgb,var(--background)_78%,transparent))] p-4 shadow-[0_24px_48px_-32px_rgba(37,99,235,0.18)]"
                    onSubmit={submitQuestion}
                  >
                    <div className="flex min-w-0 flex-wrap items-center justify-between gap-3">
                      <div className="min-w-0 space-y-1">
                        <div className="flex items-center gap-2">
                          <div className="flex size-8 items-center justify-center rounded-2xl border border-primary/14 bg-primary/10 text-primary">
                            <Bot className="size-4" />
                          </div>
                          <div className="truncate text-sm font-semibold text-foreground">{t("assistant.askRootFlow")}</div>
                        </div>
                        <div className="text-sm text-muted-foreground/95">{t("assistant.askRootFlowHint")}</div>
                      </div>
                      <div className="flex min-w-0 flex-wrap items-center gap-2">
                        <Badge variant="secondary">{t("dashboard.readyChip", { count: readyDocumentCount })}</Badge>
                        {latestReadyUpdatedLabel ? <div className="text-sm font-medium text-foreground/86">{t("common.labels.updated", { time: latestReadyUpdatedLabel })}</div> : null}
                      </div>
                      {activeLatestAnswer?.sources.length ? (
                        <Badge variant="secondary">{activeLatestAnswer.sources.length} {t("assistant.sourcesTitle").toLowerCase()}</Badge>
                      ) : null}
                    </div>
                    {readyDocumentCount > 0 && question.trim().length === 0 ? (
                      <div className="flex min-w-0 flex-wrap gap-2">
                        {suggestedPrompts.slice(0, 3).map((prompt) => (
                          <button
                            key={prompt.label}
                            type="button"
                            className="min-w-0 max-w-full overflow-hidden rounded-full border border-border/85 bg-background/90 px-3.5 py-1.5 text-sm font-medium text-foreground/82 transition-[transform,border-color,background-color,color,box-shadow] duration-200 hover:-translate-y-0.5 hover:border-primary/28 hover:bg-secondary/72 hover:text-foreground hover:shadow-[0_14px_28px_-24px_rgba(18,72,166,0.18)]"
                            onClick={() => handlePromptSelect(prompt.prompt)}
                            title={prompt.prompt}
                          >
                            <span className="block truncate">{prompt.label}</span>
                          </button>
                        ))}
                      </div>
                    ) : null}
                    <div className="rounded-[24px] border border-border/88 bg-background/96 p-3.5 transition-[border-color,background-color,box-shadow] duration-200 focus-within:border-primary/40 focus-within:bg-background focus-within:shadow-[0_22px_40px_-22px_rgba(37,99,235,0.24)]">
                      <Textarea
                        className="min-h-[120px] resize-none border-none bg-transparent px-2 py-2 text-[0.96rem] leading-7 shadow-none focus-visible:ring-0"
                        placeholder={t("assistant.inputPlaceholder")}
                        disabled={isSendingQuestion}
                        onKeyDown={handleQuestionKeyDown}
                        {...form.register("question")}
                      />
                    </div>
                    {form.formState.errors.question ? (
                      <p className="text-sm text-destructive">{form.formState.errors.question.message}</p>
                    ) : null}
                    {askQuestionMutation.isError && !billingErrorCode ? (
                      <p className="text-sm text-destructive">
                        {t("assistant.answerError")}
                      </p>
                    ) : null}
                    {readyDocumentCount === 0 && !documentsQuery.isLoading ? (
                      <p className="text-sm text-muted-foreground">{t("assistant.uploadProcessedDocumentFirst")}</p>
                    ) : null}
                    <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                      <div className="flex items-center gap-2 text-sm text-muted-foreground">
                        <CornerDownLeft className="size-4" />
                        {isSendingQuestion ? t("common.helper.searching") : t("common.helper.enterSends")}
                      </div>
                      <Button type="submit" disabled={!canAsk} aria-busy={isSendingQuestion} className="min-w-[152px]">
                        {isSendingQuestion ? <LoaderCircle className="animate-spin" /> : <SendHorizonal />}
                        {isSendingQuestion ? t("assistant.sending") : t("common.actions.send")}
                      </Button>
                    </div>
                  </form>
                )}
              </div>
            </CardContent>
          </Card>
        </div>

        <Card className="min-w-0 border-border/86 bg-card/88">
          <CardHeader className="pb-3">
            <div className="space-y-1">
              <CardTitle>{t("assistant.sourcesTitle")}</CardTitle>
              <p className="text-sm text-muted-foreground/95">{t("assistant.sourcesDescription")}</p>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            {activeLatestAnswer?.sources.length ? (
              activeLatestAnswer.sources.map((source, index) => (
                <article
                  key={source.chunkId}
                  className="space-y-3 rounded-[22px] border border-border/78 bg-[linear-gradient(180deg,color-mix(in_srgb,var(--card)_88%,transparent),color-mix(in_srgb,var(--background)_76%,transparent))] p-4 transition-[transform,border-color,box-shadow,background-color] duration-200 hover:-translate-y-0.5 hover:border-primary/22 hover:shadow-[0_18px_38px_-30px_rgba(18,72,166,0.18)]"
                >
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge>{t("common.labels.source", { index: index + 1 })}</Badge>
                    <div className="rounded-full border border-border/70 bg-background/70 px-2.5 py-1 text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
                      {t("common.labels.score", { value: source.score.toFixed(2) })}
                    </div>
                  </div>

                  <div className="flex min-w-0 items-start gap-3">
                    <div className="flex size-10 shrink-0 items-center justify-center rounded-2xl bg-primary/8 text-primary">
                      <Quote className="size-4" />
                    </div>
                    <div className="min-w-0 space-y-1.5">
                      <div className="truncate text-sm font-semibold tracking-[-0.01em] text-foreground" title={source.documentName}>
                        {source.documentName}
                      </div>
                      <div className="truncate text-xs font-semibold uppercase tracking-[0.16em] text-primary/80" title={source.sourceLabel}>
                        {source.sourceLabel}
                      </div>
                    </div>
                  </div>

                  <p className="overflow-hidden text-sm leading-6 text-foreground/84 [display:-webkit-box] [-webkit-box-orient:vertical] [-webkit-line-clamp:6] [overflow-wrap:anywhere]">
                    {source.excerpt}
                  </p>
                </article>
              ))
            ) : activeLatestAnswer ? (
              <div className="rounded-[20px] border border-dashed border-border/82 bg-card/60 px-4 py-3 text-sm text-muted-foreground">
                {t("assistant.narrowerQuestionHint")}
              </div>
            ) : (
              <div className="rounded-[20px] border border-dashed border-border/82 bg-card/60 px-4 py-3 text-sm text-muted-foreground">
                {conversationId ? t("assistant.nextQuestionSourcesHint") : t("assistant.firstGroundedAnswerSourcesHint")}
              </div>
            )}

            {isDebugVisible && activeLatestAnswer?.debug?.retrievedChunks.length ? (
              <div className="rounded-[22px] border border-dashed border-border/84 bg-card/68 p-4">
                <div className="space-y-3">
                  <div className="text-sm font-semibold text-foreground">{t("common.labels.retrieval")}</div>
                  {activeLatestAnswer.debug.retrievedChunks.map((chunk) => (
                    <div key={chunk.chunkId} className="border-b border-border/70 pb-3 last:border-b-0 last:pb-0">
                      <div className="flex min-w-0 items-center justify-between gap-3">
                        <div className="min-w-0 truncate text-sm font-semibold text-foreground" title={chunk.documentName}>
                          #{chunk.rank} {chunk.documentName}
                        </div>
                        <Badge variant="secondary">{chunk.score.toFixed(2)}</Badge>
                      </div>
                      <div className="mt-1 truncate text-xs font-semibold uppercase tracking-[0.16em] text-primary/80" title={chunk.sourceLabel}>
                        {chunk.sourceLabel}
                      </div>
                      <p className="mt-3 text-sm leading-6 text-muted-foreground [overflow-wrap:anywhere]">{chunk.reason}</p>
                    </div>
                  ))}
                </div>
              </div>
            ) : null}

            {conversationId ? (
              <div className="rounded-[22px] border border-dashed border-border/84 bg-card/68 p-4">
                <div className="space-y-2">
                  <div className="text-sm font-semibold text-foreground">{t("common.labels.conversationHistory")}</div>
                  <p className="text-sm text-muted-foreground">
                    {activeLatestAnswer ? `${t("common.labels.updated", { time: formatRelativeDate(new Date(), locale) })}.` : t("common.helper.sessionRestored")}
                  </p>
                  <Button variant="outline" className="w-full justify-between" asChild>
                    <Link to={`/conversations?conversationId=${conversationId}`}>{t("common.actions.openConversationHistory")}</Link>
                  </Button>
                </div>
              </div>
            ) : null}
          </CardContent>
        </Card>
      </section>
    </div>
  );
}
