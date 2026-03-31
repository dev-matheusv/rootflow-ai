import { Clock3, MessageCircleMore, Pin, Sparkles } from "lucide-react";
import { useEffect } from "react";
import { Link, useSearchParams } from "react-router-dom";

import { EmptyState } from "@/components/feedback/empty-state";
import { ErrorState } from "@/components/feedback/error-state";
import { LoadingState } from "@/components/feedback/loading-state";
import { FormattedAnswer } from "@/components/chat/formatted-answer";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { useConversationQuery, useConversationsQuery } from "@/hooks/use-rootflow-data";
import { formatRelativeDate } from "@/lib/formatting/formatters";

const emptyConversations: Array<{
  conversationId: string;
  title: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  messageCount: number;
  lastMessagePreview?: string | null;
}> = [];

export function ConversationsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const conversationsQuery = useConversationsQuery();
  const conversations = conversationsQuery.data ?? emptyConversations;
  const requestedConversationId = searchParams.get("conversationId");
  const selectedConversationId =
    requestedConversationId && conversations.some((conversation) => conversation.conversationId === requestedConversationId)
      ? requestedConversationId
      : conversations[0]?.conversationId ?? null;
  const conversationQuery = useConversationQuery(selectedConversationId);

  useEffect(() => {
    if (!conversations.length) {
      return;
    }

    if (!requestedConversationId || !conversations.some((conversation) => conversation.conversationId === requestedConversationId)) {
      setSearchParams({ conversationId: conversations[0].conversationId }, { replace: true });
    }
  }, [conversations, requestedConversationId, setSearchParams]);

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Conversations"
        title="Keep every answer trail readable and easy to revisit."
        description="A serious product needs session history that feels polished for teams, not a raw log viewer. This area should support review, trust, and handoff."
      />

      <section className="grid gap-4 xl:grid-cols-[0.82fr_1.18fr]">
        <Card>
          <CardHeader>
            <CardTitle>Recent sessions</CardTitle>
            <CardDescription>Live conversation summaries from the RootFlow backend, ordered by most recent activity.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {conversationsQuery.isLoading ? (
              <LoadingState
                title="Loading conversations"
                description="Fetching the stored session list so navigation reflects the real backend state."
              />
            ) : conversationsQuery.isError ? (
              <ErrorState
                title="Could not load conversation list"
                description="The frontend could not reach the RootFlow conversation summary endpoint."
                onRetry={() => conversationsQuery.refetch()}
              />
            ) : conversations.length === 0 ? (
              <EmptyState
                icon={MessageCircleMore}
                title="No conversations yet"
                description="Ask a real question in the Assistant page and RootFlow will store the conversation here automatically."
              />
            ) : (
              conversations.map((conversation) => {
                const isActive = conversation.conversationId === selectedConversationId;

                return (
                  <button
                    key={conversation.conversationId}
                    type="button"
                    onClick={() => setSearchParams({ conversationId: conversation.conversationId })}
                    className={`w-full rounded-[24px] border p-4 text-left transition-colors ${
                      isActive ? "border-primary/18 bg-primary/8" : "border-border/70 bg-background/60 hover:bg-secondary/35"
                    }`}
                  >
                    <div className="flex items-start justify-between gap-4">
                      <div className="space-y-1">
                        <div className="text-sm font-semibold text-foreground">{conversation.title}</div>
                        <p className="text-sm leading-6 text-muted-foreground">
                          {conversation.lastMessagePreview ?? "This conversation is stored and ready to inspect."}
                        </p>
                      </div>
                      <div className="flex items-center gap-2">
                        <Badge variant="secondary">{conversation.messageCount} messages</Badge>
                        {isActive ? <Pin className="size-4 text-primary" /> : null}
                      </div>
                    </div>
                    <div className="mt-3 flex flex-wrap items-center gap-3 text-xs text-muted-foreground">
                      <Clock3 className="size-3.5" />
                      <span>Updated {formatRelativeDate(conversation.updatedAtUtc)}</span>
                      <span>Created {formatRelativeDate(conversation.createdAtUtc)}</span>
                    </div>
                  </button>
                );
              })
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <div>
                <CardTitle>Conversation detail</CardTitle>
                <CardDescription>Structured to show both the answer and the decision trail behind it.</CardDescription>
              </div>
              <Badge>
                <Sparkles className="size-3.5" />
                Source-backed
              </Badge>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            {!selectedConversationId ? (
              <EmptyState
                icon={Sparkles}
                title="Select a conversation"
                description="When you create real assistant sessions, this page will show the stored history from the RootFlow backend."
              />
            ) : conversationQuery.isLoading ? (
              <LoadingState
                title="Loading conversation history"
                description="Fetching the selected session from the RootFlow conversation endpoint."
              />
            ) : conversationQuery.isError ? (
              <ErrorState
                title="Could not load this conversation"
                description="The selected session could not be retrieved from the backend. Try another conversation or continue from the Assistant page."
                onRetry={() => conversationQuery.refetch()}
              />
            ) : (
              <>
                {conversationQuery.data?.messages.map((message) => {
                  const isUser = message.role === 2;

                  return (
                    <div
                      key={message.id}
                      className={`flex gap-4 rounded-[28px] border p-4 shadow-[0_16px_30px_-30px_rgba(16,36,71,0.18)] dark:shadow-[0_18px_32px_-30px_rgba(0,0,0,0.34)] ${
                        isUser
                          ? "border-[#cadeff] bg-[#eaf2ff] dark:border-[#314662] dark:bg-[#22314a]"
                          : "border-border/75 bg-background/72"
                      }`}
                    >
                      <Avatar className="size-11">
                        <AvatarFallback>{isUser ? "U" : "AI"}</AvatarFallback>
                      </Avatar>
                      <div className="min-w-0 space-y-3">
                        <div className="flex items-center gap-2">
                          <div className="text-sm font-semibold text-foreground">{isUser ? "User" : "Assistant"}</div>
                          <Badge variant={isUser ? "secondary" : "default"}>{isUser ? "Question" : "Grounded"}</Badge>
                          <span className="text-xs text-muted-foreground">{formatRelativeDate(message.createdAtUtc)}</span>
                        </div>
                        {isUser ? (
                          <p className="whitespace-pre-wrap text-sm leading-7 text-[#16345f] dark:text-[#edf4ff]">{message.content}</p>
                        ) : (
                          <FormattedAnswer content={message.content} className="max-w-[72ch]" />
                        )}
                      </div>
                    </div>
                  );
                })}

                <div className="rounded-[24px] border border-dashed border-border/80 bg-secondary/25 p-4">
                  <div className="flex items-start gap-3">
                    <div className="flex size-10 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                      <MessageCircleMore className="size-[18px]" />
                    </div>
                    <div className="space-y-1">
                      <div className="text-sm font-semibold text-foreground">Live conversation loaded</div>
                      <p className="text-sm leading-6 text-muted-foreground">
                        This detail view and the session list both come from live RootFlow endpoints, so the conversation trail stays consistent across navigation.
                      </p>
                    </div>
                  </div>
                </div>
              </>
            )}

            {selectedConversationId ? (
              <Button variant="outline" className="w-full" asChild>
                <Link to={`/assistant?conversationId=${selectedConversationId}`}>Continue in assistant</Link>
              </Button>
            ) : null}
          </CardContent>
        </Card>
      </section>
    </div>
  );
}
