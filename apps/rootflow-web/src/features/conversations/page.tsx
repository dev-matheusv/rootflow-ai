import { Clock3, FileDown, Pin, Sparkles } from "lucide-react";
import { useEffect } from "react";
import { Link, useSearchParams } from "react-router-dom";

import { useI18n } from "@/app/providers/i18n-provider";
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
  const { locale, t } = useI18n();
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
        title={t("conversations.title")}
        description={t("conversations.description")}
      />

      <section className="grid gap-3 xl:grid-cols-[0.82fr_1.18fr]">
        <Card className="min-w-0 border-border/80 bg-card/86">
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <CardTitle>{t("conversations.sessionsTitle")}</CardTitle>
              <Badge variant="secondary">{conversations.length}</Badge>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {conversationsQuery.isLoading ? (
              <LoadingState title={t("conversations.loadingTitle")} description={t("conversations.loadingDescription")} />
            ) : conversationsQuery.isError ? (
              <ErrorState
                title={t("conversations.errorTitle")}
                description={t("conversations.errorDescription")}
                onRetry={() => conversationsQuery.refetch()}
              />
            ) : conversations.length === 0 ? (
              <div className="rounded-[18px] border border-dashed border-border/75 bg-card/56 px-4 py-3 text-sm text-muted-foreground">
                {t("conversations.emptyHint")}
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
                      className={`w-full px-4 py-3.5 text-left transition-[transform,background-color,border-color,box-shadow] duration-200 [&:not(:last-child)]:border-b [&:not(:last-child)]:border-border/70 ${
                        isActive ? "bg-primary/[0.08]" : "hover:-translate-y-0.5 hover:bg-secondary/34 hover:shadow-[inset_0_1px_0_rgba(255,255,255,0.2)]"
                      }`}
                    >
                      <div className="flex items-start justify-between gap-4">
                        <div className="min-w-0 space-y-1">
                          <div className="truncate text-sm font-semibold text-foreground">{conversation.title}</div>
                          <p className="line-clamp-2 text-sm leading-6 text-muted-foreground [overflow-wrap:anywhere]">
                            {conversation.lastMessagePreview ?? t("common.helper.noPreview")}
                          </p>
                        </div>
                        <div className="flex items-center gap-2">
                          <Badge variant="secondary">{conversation.messageCount}</Badge>
                          {isActive ? <Pin className="size-4 text-primary" /> : null}
                        </div>
                      </div>
                      <div className="mt-2 flex flex-wrap items-center gap-3 text-xs text-muted-foreground">
                        <Clock3 className="size-3.5" />
                        <span>{t("common.labels.updated", { time: formatRelativeDate(conversation.updatedAtUtc, locale) })}</span>
                        <span>{t("conversations.messagesCount", { count: conversation.messageCount })}</span>
                      </div>
                    </button>
                  );
                })}
              </div>
            )}
          </CardContent>
        </Card>

        <Card className="min-w-0 border-border/80 bg-card/86">
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <CardTitle>{t("common.labels.detail")}</CardTitle>
              <Badge>
                <Sparkles className="size-3.5" />
                {t("conversations.stored")}
              </Badge>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            {!selectedConversationId ? (
              <div className="rounded-[18px] border border-dashed border-border/75 bg-card/56 px-4 py-3 text-sm text-muted-foreground">
                {t("conversations.selectConversationHint")}
              </div>
            ) : conversationQuery.isLoading ? (
              <LoadingState title={t("conversations.loadingConversationTitle")} description={t("conversations.loadingConversationDescription")} />
            ) : conversationQuery.isError ? (
              <ErrorState
                title={t("conversations.conversationErrorTitle")}
                description={t("conversations.conversationErrorDescription")}
                onRetry={() => conversationQuery.refetch()}
              />
            ) : (
              <>
                {selectedConversationSummary ? (
                  <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border/75 pb-4">
                    <div className="min-w-0">
                      <div className="truncate text-sm font-semibold text-foreground">{selectedConversationSummary.title}</div>
                      <div className="mt-1 text-sm text-muted-foreground">
                        {t("common.labels.updated", { time: formatRelativeDate(selectedConversationSummary.updatedAtUtc, locale) })}
                      </div>
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge variant="secondary">{t("conversations.messagesCount", { count: selectedConversationSummary.messageCount })}</Badge>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => window.open(`/conversations/print?id=${selectedConversationId}`, "_blank")}
                      >
                        <FileDown className="size-3.5" />
                        {t("common.actions.exportPdf")}
                      </Button>
                      <Button variant="outline" size="sm" asChild>
                        <Link to={`/assistant?conversationId=${selectedConversationId}`}>{t("common.actions.continueConversation")}</Link>
                      </Button>
                    </div>
                  </div>
                ) : null}
                <div className="space-y-5">
                  {conversationQuery.data?.messages.map((message) => {
                    const isUser = message.role === 2;

                    return (
                      <div key={message.id} className={`flex ${isUser ? "justify-end" : "justify-start"}`}>
                        <div className={`flex min-w-0 max-w-[76ch] gap-4 motion-safe:animate-[rf-fade-up_260ms_cubic-bezier(0.22,1,0.36,1)] ${isUser ? "items-start" : "items-start"}`}>
                          {!isUser ? (
                            <Avatar className="mt-0.5 size-10">
                              <AvatarFallback>AI</AvatarFallback>
                            </Avatar>
                          ) : null}
                          <div
                            className={`min-w-0 ${
                              isUser
                                ? "rounded-[24px] rounded-br-lg border border-primary/15 bg-primary/10 px-5 py-4 text-foreground shadow-[0_18px_38px_-28px_rgba(66,116,194,0.22)] dark:border-primary/25 dark:bg-primary/20"
                                : "rounded-[22px] border border-border/82 bg-[linear-gradient(180deg,color-mix(in_srgb,var(--card)_90%,transparent),color-mix(in_srgb,var(--background)_76%,transparent))] px-5 py-4 shadow-[0_16px_32px_-30px_rgba(16,36,71,0.14)]"
                            }`}
                          >
                            <div className="mb-3 flex flex-wrap items-center gap-2">
                              <div className={`text-[11px] font-semibold uppercase tracking-[0.18em] ${isUser ? "text-primary/80" : "text-primary/75"}`}>
                                {isUser ? t("common.labels.you") : "RootFlow"}
                              </div>
                              <span className="text-xs text-muted-foreground">{formatRelativeDate(message.createdAtUtc, locale)}</span>
                            </div>
                            {isUser ? (
                              <p className="whitespace-pre-wrap text-sm leading-7 text-inherit [overflow-wrap:anywhere]">{message.content}</p>
                            ) : (
                              <FormattedAnswer content={message.content} className="max-w-[72ch] [overflow-wrap:anywhere]" />
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
