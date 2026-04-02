import { zodResolver } from "@hookform/resolvers/zod";
import { Bot, CornerDownLeft, LoaderCircle, MessageSquareQuote, Microscope, Quote, SendHorizonal } from "lucide-react";
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
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
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

  return (
    <div className="space-y-5">
      <PageHeader
        title="Assistant"
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

      <section className="grid gap-3 xl:grid-cols-[1.22fr_0.78fr]">
        <div className="space-y-3">
          <Card className="border-border/70 bg-background/72 shadow-none">
            <CardHeader className="pb-3">
              <div className="flex items-center justify-between gap-3">
                <CardTitle>{conversationId ? "Conversation" : "New session"}</CardTitle>
                <Badge variant="secondary">{readyDocumentCount} ready</Badge>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
                {documentsQuery.isLoading ? (
                  <LoadingState title="Loading documents" description="Checking what is ready." />
                ) : documentsQuery.isError ? (
                  <ErrorState
                    title="Could not load documents"
                    description="Check the connection and try again."
                    onRetry={() => documentsQuery.refetch()}
                  />
                ) : readyDocumentCount > 0 ? (
                  <>
                    {conversationId && conversationQuery.isLoading ? (
                      <LoadingState title="Loading conversation" description="Restoring messages." />
                    ) : conversationId && conversationQuery.isError ? (
                      <ErrorState
                        title="Could not restore conversation"
                        description="Start a new session or try again."
                        onRetry={() => conversationQuery.refetch()}
                      />
                    ) : messages.length === 0 ? (
                      <EmptyState
                        icon={Bot}
                        title="Ask a question"
                        description={
                          conversationId ? "This session is ready." : "Answers will use processed documents."
                        }
                      />
                    ) : (
                      <div className="rounded-[24px] border border-border/75 bg-background/66 p-4 sm:p-5">
                        <div className="space-y-6">
                          {messages.map((message) => {
                            const isUser = message.role === 2;

                            return (
                              <div key={message.id} className={`flex ${isUser ? "justify-end" : "justify-start"}`}>
                                <article
                                  className={`max-w-[72ch] rounded-[26px] border px-5 py-4 ${
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
                                    {isUser ? "You" : "RootFlow"}
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
                      <div className="rounded-[22px] border border-border/75 bg-white/92 px-5 py-4 dark:bg-[#1a2230]">
                        <div className="mb-3 flex items-center gap-2 text-[11px] font-semibold uppercase tracking-[0.18em] text-primary/90">
                          <LoaderCircle className="size-3.5 animate-spin" />
                          RootFlow
                        </div>
                        <p className="text-sm text-muted-foreground">Searching documents and preparing an answer.</p>
                      </div>
                    ) : null}
                  </>
                ) : (
                  <EmptyState
                    icon={MessageSquareQuote}
                    title="No processed documents"
                    description={
                      documentsQuery.data?.length
                        ? "Uploads are still processing."
                        : "Upload a document first."
                    }
                  />
                )}
            </CardContent>
          </Card>

          <Card className="border-border/70 bg-background/72 shadow-none">
            <CardHeader className="pb-3">
              <div className="flex items-center justify-between gap-3">
                <CardTitle>Ask</CardTitle>
                <div className="text-xs text-muted-foreground">{conversationId ? "Current session" : "New session"}</div>
              </div>
            </CardHeader>
            <CardContent>
              <form
                className="rounded-[24px] border border-border/75 bg-background/74 p-4"
                onSubmit={submitQuestion}
              >
                <div className="rounded-[24px] border border-border/75 bg-background/88 p-3 transition-[border-color,background-color,box-shadow] duration-200 focus-within:border-primary/28 focus-within:bg-background focus-within:shadow-[0_16px_32px_-26px_rgba(37,99,235,0.18)]">
                  <Textarea
                    className="min-h-[136px] resize-none border-none bg-transparent px-2 py-2 shadow-none focus-visible:ring-0"
                    placeholder="Ask about a document, process, policy, or record..."
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
                  <p className="mt-2 text-sm text-muted-foreground">Upload a processed document first.</p>
                ) : null}
                <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <div className="flex items-center gap-2 text-sm text-muted-foreground">
                    <CornerDownLeft className="size-4" />
                    {isSendingQuestion ? "Searching" : "Enter sends. Shift+Enter adds a line."}
                  </div>
                  <Button type="submit" disabled={!canAsk} aria-busy={isSendingQuestion} className="min-w-[152px]">
                    {isSendingQuestion ? <LoaderCircle className="animate-spin" /> : <SendHorizonal />}
                    {isSendingQuestion ? "Sending..." : "Send"}
                  </Button>
                </div>
              </form>
            </CardContent>
          </Card>
        </div>

        <Card className="border-border/70 bg-background/72 shadow-none">
          <CardHeader className="pb-3">
            <CardTitle>Sources</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {activeLatestAnswer?.sources.length ? (
              activeLatestAnswer.sources.map((source, index) => (
                <article
                  key={source.chunkId}
                  className="rounded-[24px] border border-border/80 bg-card/94 p-4"
                >
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge>Source {index + 1}</Badge>
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
                    <div className="mb-2 text-[11px] font-semibold uppercase tracking-[0.18em] text-primary/75">Excerpt</div>
                    <p className="overflow-hidden text-sm leading-6 text-foreground/84 [display:-webkit-box] [-webkit-box-orient:vertical] [-webkit-line-clamp:6]">
                      {source.excerpt}
                    </p>
                  </div>
                </article>
              ))
            ) : activeLatestAnswer ? (
              <EmptyState
                icon={MessageSquareQuote}
                title="No sources returned"
                description="Ask again after checking that documents are processed."
              />
            ) : (
              <EmptyState
                icon={MessageSquareQuote}
                title="No sources yet"
                description={conversationId ? "Ask the next question to review sources." : "Sources appear after the first answer."}
              />
            )}

            {isDebugVisible && activeLatestAnswer?.debug?.retrievedChunks.length ? (
              <div className="rounded-[24px] border border-dashed border-border/80 bg-background/60 p-4">
                <div className="space-y-3">
                  <div className="text-sm font-semibold text-foreground">Retrieval</div>
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
                  <div className="text-sm font-semibold text-foreground">Conversation history</div>
                  <p className="text-sm text-muted-foreground">
                    {activeLatestAnswer ? `Updated ${formatRelativeDate(new Date())}.` : "Session restored."}
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
