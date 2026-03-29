import { Clock3, MessageCircleMore, Pin, Sparkles } from "lucide-react";
import { useEffect, useMemo } from "react";
import { useSearchParams } from "react-router-dom";

import { EmptyState } from "@/components/feedback/empty-state";
import { ErrorState } from "@/components/feedback/error-state";
import { LoadingState } from "@/components/feedback/loading-state";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { useConversationsByIds, useConversationQuery } from "@/hooks/use-rootflow-data";
import { useRecentConversations } from "@/hooks/use-recent-conversations";
import { formatRelativeDate } from "@/lib/formatting/formatters";

export function ConversationsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const { items } = useRecentConversations();
  const histories = useConversationsByIds(items.map((item) => item.id));

  const selectedConversationId = searchParams.get("conversationId") ?? items[0]?.id ?? null;
  const conversationQuery = useConversationQuery(selectedConversationId);

  useEffect(() => {
    if (!searchParams.get("conversationId") && items[0]?.id) {
      setSearchParams({ conversationId: items[0].id }, { replace: true });
    }
  }, [items, searchParams, setSearchParams]);

  const historyMap = useMemo(
    () =>
      new Map(
        histories
          .filter((query) => query.data)
          .map((query) => [query.data!.conversationId, query.data!]),
      ),
    [histories],
  );

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
            <CardDescription>Recent conversations created from the live assistant flow on this device.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {items.length === 0 ? (
              <EmptyState
                icon={MessageCircleMore}
                title="No conversations yet"
                description="Ask a real question in the Assistant page and the resulting conversation will appear here automatically."
              />
            ) : (
              items.map((session) => {
                const history = historyMap.get(session.id);
                const isActive = session.id === selectedConversationId;

                return (
                  <button
                    key={session.id}
                    type="button"
                    onClick={() => setSearchParams({ conversationId: session.id })}
                    className={`w-full rounded-[24px] border p-4 text-left transition-colors ${
                      isActive ? "border-primary/18 bg-primary/8" : "border-border/70 bg-background/60 hover:bg-secondary/35"
                    }`}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="space-y-1">
                        <div className="text-sm font-semibold text-foreground">{history?.title ?? session.title}</div>
                        <p className="text-sm leading-6 text-muted-foreground">{session.preview}</p>
                      </div>
                      {isActive ? <Pin className="size-4 text-primary" /> : null}
                    </div>
                    <div className="mt-3 flex items-center gap-2 text-xs text-muted-foreground">
                      <Clock3 className="size-3.5" />
                      {formatRelativeDate(session.updatedAt)}
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
                description="The selected session could not be retrieved from the backend. Try another conversation or ask a new question."
                onRetry={() => conversationQuery.refetch()}
              />
            ) : (
              <>
                {conversationQuery.data?.messages.map((message) => {
                  const isUser = message.role === 2;

                  return (
                    <div key={message.id} className="flex gap-4 rounded-[28px] border border-border/70 bg-background/60 p-4">
                      <Avatar className="size-11">
                        <AvatarFallback>{isUser ? "U" : "AI"}</AvatarFallback>
                      </Avatar>
                      <div className="space-y-2">
                        <div className="flex items-center gap-2">
                          <div className="text-sm font-semibold text-foreground">{isUser ? "User" : "Assistant"}</div>
                          <Badge variant={isUser ? "secondary" : "default"}>{isUser ? "Question" : "Grounded"}</Badge>
                          <span className="text-xs text-muted-foreground">{formatRelativeDate(message.createdAtUtc)}</span>
                        </div>
                        <p className="text-sm leading-7 text-muted-foreground">{message.content}</p>
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
                        The detail view now comes from the existing backend history endpoint and uses local recent-session memory for navigation.
                      </p>
                    </div>
                  </div>
                </div>
              </>
            )}

            {items.length > 0 ? (
              <Button variant="outline" className="w-full" onClick={() => setSearchParams({ conversationId: items[0].id })}>
                Jump to latest session
              </Button>
            ) : null}
          </CardContent>
        </Card>
      </section>
    </div>
  );
}
