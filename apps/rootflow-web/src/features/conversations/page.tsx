import { Clock3, Pin, Sparkles } from "lucide-react";
import { useEffect } from "react";
import { Link, useSearchParams } from "react-router-dom";

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
  const selectedConversationSummary = conversations.find((conversation) => conversation.conversationId === selectedConversationId) ?? null;
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
    <div className="space-y-5">
      <PageHeader
        title="Conversations"
      />

      <section className="grid gap-3 xl:grid-cols-[0.82fr_1.18fr]">
        <Card className="border-border/80 bg-card/86">
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <CardTitle>Sessions</CardTitle>
              <Badge variant="secondary">{conversations.length}</Badge>
            </div>
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
              <div className="rounded-[18px] border border-dashed border-border/75 bg-card/56 px-4 py-3 text-sm text-muted-foreground">
                No conversations yet. Ask a question in Assistant to create one.
              </div>
            ) : (
              <div className="overflow-hidden rounded-[22px] border border-border/75 bg-card/72">
                {conversations.map((conversation) => {
                  const isActive = conversation.conversationId === selectedConversationId;

                  return (
                    <button
                      key={conversation.conversationId}
                      type="button"
                      onClick={() => setSearchParams({ conversationId: conversation.conversationId })}
                      className={`w-full px-4 py-3.5 text-left transition-colors [&:not(:last-child)]:border-b [&:not(:last-child)]:border-border/70 ${
                        isActive ? "bg-primary/[0.08]" : "hover:bg-secondary/24"
                      }`}
                    >
                      <div className="flex items-start justify-between gap-4">
                        <div className="min-w-0 space-y-1">
                          <div className="truncate text-sm font-semibold text-foreground">{conversation.title}</div>
                          <p className="line-clamp-2 text-sm leading-6 text-muted-foreground">{conversation.lastMessagePreview ?? "No preview"}</p>
                        </div>
                        <div className="flex items-center gap-2">
                          <Badge variant="secondary">{conversation.messageCount}</Badge>
                          {isActive ? <Pin className="size-4 text-primary" /> : null}
                        </div>
                      </div>
                      <div className="mt-2 flex flex-wrap items-center gap-3 text-xs text-muted-foreground">
                        <Clock3 className="size-3.5" />
                        <span>Updated {formatRelativeDate(conversation.updatedAtUtc)}</span>
                        <span>{conversation.messageCount} messages</span>
                      </div>
                    </button>
                  );
                })}
              </div>
            )}
          </CardContent>
        </Card>

        <Card className="border-border/80 bg-card/86">
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
              <div className="rounded-[18px] border border-dashed border-border/75 bg-card/56 px-4 py-3 text-sm text-muted-foreground">
                Select a conversation to review stored messages.
              </div>
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
                {selectedConversationSummary ? (
                  <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border/75 pb-4">
                    <div className="min-w-0">
                      <div className="truncate text-sm font-semibold text-foreground">{selectedConversationSummary.title}</div>
                      <div className="mt-1 text-sm text-muted-foreground">
                        Updated {formatRelativeDate(selectedConversationSummary.updatedAtUtc)}
                      </div>
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge variant="secondary">{selectedConversationSummary.messageCount} messages</Badge>
                      <Button variant="outline" size="sm" asChild>
                        <Link to={`/assistant?conversationId=${selectedConversationId}`}>Continue</Link>
                      </Button>
                    </div>
                  </div>
                ) : null}
                <div className="space-y-5">
                  {conversationQuery.data?.messages.map((message) => {
                    const isUser = message.role === 2;

                    return (
                      <div key={message.id} className={`flex ${isUser ? "justify-end" : "justify-start"}`}>
                        <div className={`flex max-w-[76ch] gap-4 ${isUser ? "items-start" : "items-start"}`}>
                          {!isUser ? (
                            <Avatar className="mt-0.5 size-10">
                              <AvatarFallback>AI</AvatarFallback>
                            </Avatar>
                          ) : null}
                          <div
                            className={`min-w-0 ${
                              isUser
                                ? "rounded-[24px] rounded-br-lg border border-[#bad4ff] bg-[#eaf2ff] px-5 py-4 text-[#16345f] shadow-[0_14px_32px_-28px_rgba(66,116,194,0.32)] dark:border-[#395476] dark:bg-[#22314a] dark:text-[#edf4ff]"
                                : "rounded-[20px] border border-border/75 bg-background/76 px-5 py-4"
                            }`}
                          >
                            <div className="mb-3 flex flex-wrap items-center gap-2">
                              <div className={`text-[11px] font-semibold uppercase tracking-[0.18em] ${isUser ? "text-[#3f669e] dark:text-[#a9c8ff]" : "text-primary/75"}`}>
                                {isUser ? "You" : "RootFlow"}
                              </div>
                              <span className="text-xs text-muted-foreground">{formatRelativeDate(message.createdAtUtc)}</span>
                            </div>
                            {isUser ? (
                              <p className="whitespace-pre-wrap text-sm leading-7 text-inherit">{message.content}</p>
                            ) : (
                              <FormattedAnswer content={message.content} className="max-w-[72ch]" />
                            )}
                          </div>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </>
            )}
          </CardContent>
        </Card>
      </section>
    </div>
  );
}
