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
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
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
        title="Conversations"
      />

      <section className="grid gap-4 xl:grid-cols-[0.82fr_1.18fr]">
        <Card className="border-border/70 bg-background/72 shadow-none">
          <CardHeader>
            <CardTitle>Sessions</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {conversationsQuery.isLoading ? (
              <LoadingState title="Loading conversations" description="Fetching sessions." />
            ) : conversationsQuery.isError ? (
              <ErrorState
                title="Could not load conversations"
                description="Try again."
                onRetry={() => conversationsQuery.refetch()}
              />
            ) : conversations.length === 0 ? (
              <EmptyState
                icon={MessageCircleMore}
                title="No conversations yet"
                description="Ask a question in Assistant to create one."
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
                        <p className="line-clamp-2 text-sm leading-6 text-muted-foreground">{conversation.lastMessagePreview ?? "No preview"}</p>
                      </div>
                      <div className="flex items-center gap-2">
                        <Badge variant="secondary">{conversation.messageCount}</Badge>
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

        <Card className="border-border/70 bg-background/72 shadow-none">
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <CardTitle>Detail</CardTitle>
              <Badge>
                <Sparkles className="size-3.5" />
                Stored
              </Badge>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            {!selectedConversationId ? (
              <EmptyState
                icon={Sparkles}
                title="Select a conversation"
                description="Stored messages appear here."
              />
            ) : conversationQuery.isLoading ? (
              <LoadingState title="Loading conversation" description="Fetching messages." />
            ) : conversationQuery.isError ? (
              <ErrorState
                title="Could not load conversation"
                description="Try another session or retry."
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
