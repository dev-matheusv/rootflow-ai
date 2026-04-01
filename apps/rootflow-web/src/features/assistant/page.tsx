import { zodResolver } from "@hookform/resolvers/zod";
import { Bot, CheckCircle2, CornerDownLeft, LoaderCircle, MessageSquareQuote, Microscope, Quote, SendHorizonal } from "lucide-react";
import { type KeyboardEvent, useMemo, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { Link, useSearchParams } from "react-router-dom";
import { z } from "zod";

import { EmptyState } from "@/components/feedback/empty-state";
import { ErrorState } from "@/components/feedback/error-state";
import { LoadingState } from "@/components/feedback/loading-state";
import { FormattedAnswer } from "@/components/chat/formatted-answer";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { Textarea } from "@/components/ui/textarea";
import { useAskQuestionMutation, useConversationQuery, useDocumentsQuery } from "@/hooks/use-rootflow-data";
import type { ChatAnswer } from "@/lib/api/contracts";
import { formatRelativeDate } from "@/lib/formatting/formatters";

const assistantSchema = z.object({
  question: z.string().trim().min(3, "Ask a fuller question so RootFlow can retrieve the right context."),
});

type AssistantFormValues = z.infer<typeof assistantSchema>;

export function AssistantPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const conversationId = searchParams.get("conversationId");
  const [latestAnswer, setLatestAnswer] = useState<ChatAnswer | null>(null);
  const [showDebug, setShowDebug] = useState(false);

  const documentsQuery = useDocumentsQuery({ autoRefreshProcessing: true });
  const conversationQuery = useConversationQuery(conversationId);
  const askQuestionMutation = useAskQuestionMutation();

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
  const readyDocumentCount = documentsQuery.data?.filter((document) => document.status === 3).length ?? 0;
  const activeLatestAnswer = latestAnswer?.conversationId === conversationId ? latestAnswer : null;
  const isSendingQuestion = askQuestionMutation.isPending || form.formState.isSubmitting;
  const canReviewRetrieval = Boolean(activeLatestAnswer?.debug?.retrievedChunks?.length);
  const isDebugVisible = showDebug && canReviewRetrieval;
  const canAsk = question.trim().length >= 3 && !isSendingQuestion && readyDocumentCount > 0;

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
    if (!((event.ctrlKey || event.metaKey) && event.key === "Enter")) {
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

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Assistant"
        title="Grounded answers should feel polished enough for customer-facing work."
        description="RootFlow now presents each answer in a calmer, more readable workspace with clear spacing, trusted sources, and conversation flow that feels ready for real teams."
        actions={
          <>
            <Button onClick={handleNewSession}>
              <Bot />
              New session
            </Button>
            <Button
              variant="outline"
              onClick={() => setShowDebug((value) => !value)}
              disabled={!canReviewRetrieval}
            >
              <Microscope />
              {isDebugVisible ? "Hide retrieval" : "Review retrieval"}
            </Button>
          </>
        }
      />

      <section className="grid gap-4 xl:grid-cols-[1.22fr_0.78fr]">
        <Card className="overflow-hidden">
          <CardContent className="relative p-0">
            <div className="absolute inset-0 bg-[linear-gradient(135deg,rgba(37,99,235,0.08),transparent_42%,rgba(188,215,255,0.18))] dark:bg-[linear-gradient(135deg,rgba(71,110,177,0.14),transparent_42%,rgba(45,67,98,0.18))]" />
            <div className="relative flex flex-col gap-8 p-6 md:p-8">
              <div className="flex flex-wrap items-center gap-2">
                <Badge>Grounded responses</Badge>
                <Badge variant="secondary">Readable sources</Badge>
                <Badge variant="secondary">Premium chat UX</Badge>
              </div>
              <div className="space-y-3">
                <h2 className="max-w-3xl font-display text-3xl tracking-[-0.05em] text-foreground">
                  A premium chat workspace for precise answers, cleaner reading, and better source trust.
                </h2>
                <p className="max-w-2xl text-sm leading-7 text-muted-foreground sm:text-base">
                  The assistant should feel calm, structured, and reliable. Messages stay readable, supporting evidence stays visible, and every new answer fits cleanly into an ongoing conversation.
                </p>
              </div>

              <div className="mx-auto flex w-full max-w-3xl flex-col gap-6">
                {documentsQuery.isLoading ? (
                  <LoadingState
                    title="Analyzing your documents..."
                    description="Checking what is ready in the knowledge base so RootFlow can answer with grounded context."
                  />
                ) : documentsQuery.isError ? (
                  <ErrorState
                    title="We couldn't load your documents right now"
                    description="Please check the API connection and try again. RootFlow needs access to the knowledge base before it can answer confidently."
                    onRetry={() => documentsQuery.refetch()}
                  />
                ) : readyDocumentCount > 0 ? (
                  <>
                    {conversationId && conversationQuery.isLoading ? (
                      <LoadingState
                        title="Loading your conversation..."
                        description="Restoring the latest messages and answer trail so you can continue exactly where you left off."
                      />
                    ) : conversationId && conversationQuery.isError ? (
                      <ErrorState
                        title="We couldn't restore this conversation"
                        description="Try refreshing the session or start a new one. Your documents are still available for new questions."
                        onRetry={() => conversationQuery.refetch()}
                      />
                    ) : messages.length === 0 ? (
                      <EmptyState
                        icon={Bot}
                        title="Ask your first grounded question"
                        description={
                          conversationId
                            ? "This conversation is ready to continue. Ask the next question and RootFlow will keep the answer trail in the same session."
                            : "Start with any business question. RootFlow will search your uploaded knowledge, write a grounded answer, and keep the conversation history organized."
                        }
                      />
                    ) : (
                      <div className="rounded-[30px] border border-border/80 bg-background/70 p-4 shadow-[0_18px_36px_-30px_rgba(16,36,71,0.14)] dark:shadow-[0_18px_36px_-30px_rgba(0,0,0,0.34)] sm:p-5">
                        <div className="space-y-6">
                        {messages.map((message) => {
                          const isUser = message.role === 2;

                          return (
                            <div key={message.id} className={`flex ${isUser ? "justify-end" : "justify-start"}`}>
                              <article
                                className={`max-w-[72ch] rounded-[28px] border px-5 py-4 shadow-[0_18px_34px_-28px_rgba(16,36,71,0.16)] dark:shadow-[0_18px_34px_-28px_rgba(0,0,0,0.34)] ${
                                  isUser
                                    ? "rounded-br-lg border-[#cadeff] bg-[#eaf2ff] text-[#14315c] dark:border-[#314662] dark:bg-[#22314a] dark:text-[#edf4ff]"
                                    : "rounded-bl-lg border-border/80 bg-white text-foreground dark:bg-[#1a2230]"
                                }`}
                              >
                                <div
                                  className={`mb-3 text-[11px] font-semibold uppercase tracking-[0.18em] ${
                                    isUser ? "text-[#3f669e] dark:text-[#a9c8ff]" : "text-primary/75"
                                  }`}
                                >
                                  {isUser ? "You" : "RootFlow assistant"}
                                </div>
                                {isUser ? (
                                  <p className="whitespace-pre-wrap text-[0.96rem] leading-7 text-inherit">{message.content}</p>
                                ) : (
                                  <FormattedAnswer content={message.content} />
                                )}
                              </article>
                            </div>
                          );
                        })}
                        </div>
                      </div>
                    )}

                    {askQuestionMutation.isPending ? (
                      <div className="rounded-[28px] border border-border/80 bg-white px-5 py-4 shadow-[0_18px_34px_-28px_rgba(16,36,71,0.16)] dark:bg-[#1a2230] dark:shadow-[0_18px_34px_-28px_rgba(0,0,0,0.34)]">
                        <div className="mb-3 flex items-center gap-2 text-[11px] font-semibold uppercase tracking-[0.18em] text-primary/90">
                          <LoaderCircle className="size-3.5 animate-spin" />
                          RootFlow assistant
                        </div>
                        <div className="space-y-3">
                          <p className="text-sm font-semibold text-foreground">Searching for relevant answers...</p>
                          <p className="text-sm leading-7 text-muted-foreground">
                            Analyzing your documents and assembling the most useful grounded response with aligned sources.
                          </p>
                        </div>
                      </div>
                    ) : null}
                  </>
                ) : (
                  <EmptyState
                    icon={MessageSquareQuote}
                    title="Upload documents to start asking intelligent questions"
                    description={
                      documentsQuery.data?.length
                        ? "Your uploads are still processing. As soon as at least one document is ready, RootFlow will enable grounded chat."
                        : "Add documents in the Knowledge Base first. Once they are processed, RootFlow can answer with grounded context and source-backed responses."
                    }
                  />
                )}
              </div>

              <form
                className="rounded-[30px] border border-border/80 bg-background/78 p-4 shadow-[0_18px_38px_-32px_rgba(16,36,71,0.16)] dark:shadow-[0_18px_36px_-30px_rgba(0,0,0,0.34)]"
                onSubmit={submitQuestion}
              >
                <div className="mb-3 flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
                  <div className="text-sm font-semibold text-foreground">Ask a grounded question</div>
                  <div className="text-xs text-muted-foreground">
                    {conversationId ? "Continuing this live conversation" : "Starts a new conversation"}
                  </div>
                </div>
                <div className="rounded-[24px] border border-border/75 bg-background/88 p-3 transition-[border-color,background-color,box-shadow] duration-200 focus-within:border-primary/28 focus-within:bg-background focus-within:shadow-[0_16px_32px_-26px_rgba(37,99,235,0.18)]">
                  <Textarea
                    className="min-h-[136px] resize-none border-none bg-transparent px-2 py-2 shadow-none focus-visible:ring-0"
                    placeholder="Ask about a document, process, resume, policy, or any grounded business question..."
                    disabled={isSendingQuestion}
                    onKeyDown={handleQuestionKeyDown}
                    {...form.register("question")}
                  />
                </div>
                {form.formState.errors.question ? (
                  <p className="mt-2 text-sm text-destructive">{form.formState.errors.question.message}</p>
                ) : null}
                {askQuestionMutation.isError ? (
                  <p className="mt-2 text-sm text-destructive">
                    We couldn't generate an answer right now. Please try again in a moment.
                  </p>
                ) : null}
                {readyDocumentCount === 0 && !documentsQuery.isLoading ? (
                  <p className="mt-2 text-sm text-muted-foreground">
                    Upload at least one processed document before asking grounded questions.
                  </p>
                ) : null}
                <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <div className="flex items-center gap-2 text-sm text-muted-foreground">
                    <CornerDownLeft className="size-4" />
                    {isSendingQuestion ? "Searching with grounded context" : "Ctrl+Enter sends. Enter keeps a new line."}
                  </div>
                  <Button type="submit" disabled={!canAsk} aria-busy={isSendingQuestion} className="min-w-[152px]">
                    {isSendingQuestion ? <LoaderCircle className="animate-spin" /> : <SendHorizonal />}
                    {isSendingQuestion ? "Sending..." : "Send question"}
                  </Button>
                </div>
              </form>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Supporting sources</CardTitle>
            <CardDescription>Review the exact document snippets that supported the latest answer without leaving the conversation.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {activeLatestAnswer ? (
              <div className="rounded-[26px] border border-emerald-500/22 bg-emerald-500/8 p-4 shadow-[0_16px_30px_-28px_rgba(16,133,95,0.22)]">
                <div className="flex items-start gap-3">
                  <div className="mt-0.5 flex size-10 items-center justify-center rounded-2xl bg-emerald-500/14 text-emerald-700 dark:text-emerald-300">
                    <CheckCircle2 className="size-[18px]" />
                  </div>
                  <div className="space-y-1">
                    <div className="text-sm font-semibold text-foreground">Latest answer captured</div>
                    <p className="text-sm leading-6 text-muted-foreground">
                      RootFlow stored this answer in the live conversation and linked {activeLatestAnswer.sources.length} supporting source
                      {activeLatestAnswer.sources.length === 1 ? "" : "s"} for review.
                    </p>
                  </div>
                </div>
              </div>
            ) : null}

            {activeLatestAnswer?.sources.length ? (
              activeLatestAnswer.sources.map((source, index) => (
                <article
                  key={source.chunkId}
                  className="rounded-[26px] border border-border/80 bg-card/94 p-4 shadow-[0_18px_36px_-30px_rgba(16,36,71,0.14)] dark:shadow-[0_18px_34px_-30px_rgba(0,0,0,0.34)]"
                >
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge>Referenced in answer</Badge>
                    <Badge variant="secondary">Source {index + 1}</Badge>
                    <div className="rounded-full border border-border/70 bg-background/70 px-2.5 py-1 text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
                      Score {source.score.toFixed(2)}
                    </div>
                  </div>

                  <div className="mt-4 flex items-start gap-3">
                    <div className="flex size-10 shrink-0 items-center justify-center rounded-2xl bg-primary/8 text-primary">
                      <Quote className="size-4" />
                    </div>
                    <div className="min-w-0 space-y-1.5">
                      <div className="text-sm font-semibold tracking-[-0.01em] text-foreground">{source.documentName}</div>
                      <div className="text-xs font-semibold uppercase tracking-[0.16em] text-primary/80">{source.sourceLabel}</div>
                    </div>
                  </div>

                  <div className="mt-4 rounded-[22px] border border-primary/12 bg-primary/[0.05] p-4 dark:bg-primary/[0.08]">
                    <div className="mb-2 text-[11px] font-semibold uppercase tracking-[0.18em] text-primary/75">Referenced snippet</div>
                    <p className="overflow-hidden text-sm leading-6 text-foreground/84 [display:-webkit-box] [-webkit-box-orient:vertical] [-webkit-line-clamp:6]">
                      {source.excerpt}
                    </p>
                  </div>
                </article>
              ))
            ) : activeLatestAnswer ? (
              <EmptyState
                icon={MessageSquareQuote}
                title="No supporting snippets were returned"
                description="The latest answer came back without source blocks. Ask another grounded question after verifying your uploaded documents are processed."
              />
            ) : (
              <EmptyState
                icon={MessageSquareQuote}
                title="Source cards will appear here"
                description={
                  conversationId
                    ? "Ask the next question in this conversation to review the snippets used in the latest answer."
                    : "After the first grounded answer, RootFlow will show the supporting source cards in this panel."
                }
              />
            )}

            {isDebugVisible && activeLatestAnswer?.debug?.retrievedChunks.length ? (
              <div className="rounded-[24px] border border-dashed border-border/80 bg-background/60 p-4">
                <div className="space-y-3">
                  <div className="text-sm font-semibold text-foreground">Retrieval review</div>
                  {activeLatestAnswer.debug.retrievedChunks.map((chunk) => (
                    <div key={chunk.chunkId} className="rounded-[20px] border border-border/80 bg-background/82 p-4">
                      <div className="flex items-center justify-between gap-3">
                        <div className="text-sm font-semibold text-foreground">
                          #{chunk.rank} {chunk.documentName}
                        </div>
                        <Badge variant="secondary">{chunk.score.toFixed(2)}</Badge>
                      </div>
                      <div className="mt-1 text-xs font-semibold uppercase tracking-[0.16em] text-primary/80">{chunk.sourceLabel}</div>
                      <p className="mt-3 text-sm leading-6 text-muted-foreground">{chunk.reason}</p>
                    </div>
                  ))}
                </div>
              </div>
            ) : null}

            {conversationId ? (
              <div className="rounded-[24px] border border-dashed border-border/80 bg-background/60 p-4">
                <div className="space-y-2">
                  <div className="text-sm font-semibold text-foreground">Current live conversation</div>
                  <p className="text-sm leading-6 text-muted-foreground">
                    {activeLatestAnswer
                      ? `Updated ${formatRelativeDate(new Date())} and ready to review in the conversation index.`
                      : "This session is restored from the backend and ready to continue."}
                  </p>
                  <Button variant="outline" className="w-full justify-between" asChild>
                    <Link to={`/conversations?conversationId=${conversationId}`}>Open conversation history</Link>
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
