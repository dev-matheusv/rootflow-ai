import { zodResolver } from "@hookform/resolvers/zod";
import { Bot, CheckCircle2, CornerDownLeft, MessageSquareQuote, Microscope, SendHorizonal } from "lucide-react";
import { useMemo, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { Link, useSearchParams } from "react-router-dom";
import { z } from "zod";

import { EmptyState } from "@/components/feedback/empty-state";
import { ErrorState } from "@/components/feedback/error-state";
import { LoadingState } from "@/components/feedback/loading-state";
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
  const canReviewRetrieval = Boolean(activeLatestAnswer?.debug?.retrievedChunks.length);
  const isDebugVisible = showDebug && canReviewRetrieval;
  const canAsk = question.trim().length >= 3 && !askQuestionMutation.isPending && readyDocumentCount > 0;

  const onSubmit = form.handleSubmit(async (values) => {
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
        title="Make the answer experience feel calm, precise, and trustworthy."
        description="The assistant surface should feel like the center of the product: comfortable for daily business use, elegant for demos, and explicit about where each answer comes from."
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

      <section className="grid gap-4 xl:grid-cols-[1.2fr_0.8fr]">
        <Card className="overflow-hidden">
          <CardContent className="relative p-0">
            <div className="absolute inset-0 bg-[linear-gradient(135deg,rgba(32,110,255,0.12),transparent_38%,rgba(130,203,255,0.16))]" />
            <div className="relative flex flex-col gap-6 p-6 md:p-8">
              <div className="flex flex-wrap items-center gap-2">
                <Badge>Grounded chat</Badge>
                <Badge variant="secondary">Sources visible</Badge>
                <Badge variant="secondary">Debug-ready</Badge>
              </div>
              <div className="space-y-3">
                <h2 className="font-display text-3xl tracking-[-0.05em] text-foreground">A premium chat workspace built for confident business answers.</h2>
                <p className="max-w-2xl text-sm leading-7 text-muted-foreground sm:text-base">
                  Answer quality is only part of the product. The surrounding experience should make grounded responses feel believable, legible, and easy to inspect.
                </p>
              </div>

              <div className="grid gap-4">
                {documentsQuery.isLoading ? (
                  <LoadingState
                    title="Checking knowledge readiness"
                    description="RootFlow is verifying the current document base before it answers with grounded context."
                  />
                ) : documentsQuery.isError ? (
                  <ErrorState
                    title="Could not reach the knowledge base"
                    description="The assistant needs the RootFlow API and document endpoint available before it can answer with live data."
                    onRetry={() => documentsQuery.refetch()}
                  />
                ) : readyDocumentCount > 0 ? (
                  <>
                    {conversationId && conversationQuery.isLoading ? (
                      <LoadingState
                        title="Loading live conversation"
                        description="Rehydrating the stored conversation so the assistant can continue the current session."
                      />
                    ) : conversationId && conversationQuery.isError ? (
                      <ErrorState
                        title="Could not load the current conversation"
                        description="The assistant could not restore this session from the backend. Start a new one or try the existing conversation again."
                        onRetry={() => conversationQuery.refetch()}
                      />
                    ) : messages.length === 0 ? (
                      <EmptyState
                        icon={Bot}
                        title="Ask the first real question"
                        description={
                          conversationId
                            ? "This conversation is open and ready to continue. Ask the next question to keep the same session flowing."
                            : "The assistant is connected to the backend. Ask a business question and RootFlow will create a live conversation, return grounded answers, and save the history."
                        }
                      />
                    ) : (
                      messages.map((message) => {
                        const isUser = message.role === 2;

                        return (
                          <div
                            key={message.id}
                            className={`max-w-[86%] rounded-[28px] px-5 py-4 text-sm leading-7 shadow-[0_20px_44px_-32px_rgba(12,39,84,0.4)] ${
                              isUser
                                ? "ml-auto rounded-br-md border border-primary/12 bg-primary text-primary-foreground shadow-[0_18px_40px_-26px_rgba(21,91,255,0.72)]"
                                : "rounded-bl-md border border-border/70 bg-background/82 text-foreground"
                            }`}
                          >
                            {message.content}
                          </div>
                        );
                      })
                    )}

                    {askQuestionMutation.isPending ? (
                      <div className="max-w-[86%] rounded-[28px] rounded-bl-md border border-border/70 bg-background/82 px-5 py-4 text-sm leading-7 text-muted-foreground">
                        RootFlow is preparing a grounded answer...
                      </div>
                    ) : null}
                  </>
                ) : (
                  <EmptyState
                    icon={MessageSquareQuote}
                    title={documentsQuery.data?.length ? "Wait for processing to finish" : "Upload documents before chatting"}
                    description={
                      documentsQuery.data?.length
                        ? "The workspace has uploads in progress, but no processed knowledge is ready yet. RootFlow will enable grounded chat as soon as processing completes."
                        : "The assistant is connected, but the workspace has no documents yet. Add knowledge in the Knowledge Base page to activate grounded answers."
                    }
                  />
                )}
              </div>

              <form className="rounded-[28px] border border-border/70 bg-background/78 p-4" onSubmit={onSubmit}>
                <div className="mb-3 flex items-center justify-between">
                  <div className="text-sm font-semibold text-foreground">Ask a business question</div>
                  <div className="text-xs text-muted-foreground">
                    {conversationId ? "Continuing a live conversation" : "Creates a new live conversation"}
                  </div>
                </div>
                <Textarea
                  className="min-h-[120px] resize-none border-none bg-transparent px-0 py-0 shadow-none focus-visible:ring-0"
                  placeholder="What can I help your team answer today?"
                  {...form.register("question")}
                />
                {form.formState.errors.question ? (
                  <p className="mt-2 text-sm text-destructive">{form.formState.errors.question.message}</p>
                ) : null}
                {askQuestionMutation.isError ? (
                  <p className="mt-2 text-sm text-destructive">{askQuestionMutation.error.message}</p>
                ) : null}
                {readyDocumentCount === 0 && !documentsQuery.isLoading ? (
                  <p className="mt-2 text-sm text-muted-foreground">
                    RootFlow needs at least one processed document before it can return grounded answers.
                  </p>
                ) : null}
                <div className="mt-4 flex items-center justify-between gap-3">
                  <div className="flex items-center gap-2 text-sm text-muted-foreground">
                    <CornerDownLeft className="size-4" />
                    Compose with grounded context and citations
                  </div>
                  <Button type="submit" disabled={!canAsk}>
                    <SendHorizonal />
                    {askQuestionMutation.isPending ? "Thinking..." : "Send"}
                  </Button>
                </div>
              </form>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Supporting sources</CardTitle>
            <CardDescription>Live answer support stays visible without breaking the premium conversation flow.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {activeLatestAnswer ? (
              <div className="rounded-[24px] border border-emerald-500/20 bg-emerald-500/10 p-4">
                <div className="flex items-start gap-3">
                  <div className="mt-0.5 flex size-10 items-center justify-center rounded-2xl bg-emerald-500/14 text-emerald-600 dark:text-emerald-300">
                    <CheckCircle2 className="size-[18px]" />
                  </div>
                  <div className="space-y-1">
                    <div className="text-sm font-semibold text-foreground">Live answer stored</div>
                    <p className="text-sm leading-6 text-muted-foreground">
                      RootFlow saved this answer in the current conversation and returned {activeLatestAnswer.sources.length} source
                      {activeLatestAnswer.sources.length === 1 ? "" : "s"} for review.
                    </p>
                  </div>
                </div>
              </div>
            ) : null}

            {activeLatestAnswer?.sources.length ? (
              activeLatestAnswer.sources.map((source, index) => (
                <div key={source.chunkId} className="rounded-[24px] border border-border/70 bg-secondary/30 p-4">
                  <div className="flex items-center justify-between gap-3">
                    <Badge variant="secondary">[{index + 1}]</Badge>
                    <div className="text-xs uppercase tracking-[0.18em] text-muted-foreground">Score {source.score.toFixed(2)}</div>
                  </div>
                  <div className="mt-3 text-sm font-semibold text-foreground">{source.documentName}</div>
                  <div className="mt-1 text-sm text-primary">{source.sourceLabel}</div>
                  <p className="mt-3 text-sm leading-7 text-muted-foreground">{source.excerpt}</p>
                </div>
              ))
            ) : activeLatestAnswer ? (
              <EmptyState
                icon={MessageSquareQuote}
                title="No source blocks were returned"
                description="The latest live answer did not include supporting chunks. This usually means the model chose a low-confidence fallback."
              />
            ) : (
              <EmptyState
                icon={MessageSquareQuote}
                title="Sources will appear here"
                description={
                  conversationId
                    ? "When you ask the next question in this conversation, RootFlow will show the latest supporting source blocks here."
                    : "After a live answer, RootFlow will show the supporting source blocks and their retrieval score in this panel."
                }
              />
            )}

            {isDebugVisible && activeLatestAnswer?.debug?.retrievedChunks.length ? (
              <div className="rounded-[24px] border border-dashed border-border/80 bg-background/55 p-4">
                <div className="space-y-3">
                  <div className="text-sm font-semibold text-foreground">Retrieval review</div>
                  {activeLatestAnswer.debug.retrievedChunks.map((chunk) => (
                    <div key={chunk.chunkId} className="rounded-2xl border border-border/70 bg-background/70 p-3">
                      <div className="flex items-center justify-between gap-3">
                        <div className="text-sm font-semibold text-foreground">
                          #{chunk.rank} {chunk.documentName}
                        </div>
                        <Badge variant="secondary">{chunk.score.toFixed(2)}</Badge>
                      </div>
                      <div className="mt-1 text-sm text-primary">{chunk.sourceLabel}</div>
                      <p className="mt-2 text-sm leading-6 text-muted-foreground">{chunk.reason}</p>
                    </div>
                  ))}
                </div>
              </div>
            ) : null}

            {conversationId ? (
              <div className="rounded-[24px] border border-dashed border-border/80 bg-background/55 p-4">
                <div className="space-y-2">
                  <div className="text-sm font-semibold text-foreground">Current live conversation</div>
                  <p className="text-sm leading-6 text-muted-foreground">
                    {activeLatestAnswer
                      ? `Updated ${formatRelativeDate(new Date())} and ready to review in the conversation index.`
                      : "This session is restored from the backend and ready to continue."}
                  </p>
                  <Button variant="outline" className="w-full" asChild>
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
