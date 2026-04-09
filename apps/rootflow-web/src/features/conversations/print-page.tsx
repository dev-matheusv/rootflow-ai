import { useEffect } from "react";
import { useSearchParams } from "react-router-dom";

import { useI18n } from "@/app/providers/i18n-provider";
import { useConversationQuery } from "@/hooks/use-rootflow-data";
import { formatAbsoluteDate } from "@/lib/formatting/formatters";

export function ConversationPrintPage() {
  const { locale, t } = useI18n();
  const [searchParams] = useSearchParams();
  const conversationId = searchParams.get("id") ?? "";
  const conversationQuery = useConversationQuery(conversationId || null);
  const conversation = conversationQuery.data;

  useEffect(() => {
    if (!conversation) return;
    const timer = setTimeout(() => window.print(), 400);
    return () => clearTimeout(timer);
  }, [conversation]);

  if (!conversationId || conversationQuery.isError) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-white p-8 text-sm text-gray-500">
        {t("conversations.printNotFound")}
      </div>
    );
  }

  if (conversationQuery.isLoading || !conversation) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-white p-8 text-sm text-gray-500">
        {t("conversations.printLoading")}
      </div>
    );
  }

  const messages = conversation.messages ?? [];

  return (
    <div className="min-h-screen bg-white px-12 py-10 text-[#111] print:px-8 print:py-6">
      {/* Header */}
      <div className="mb-8 border-b border-gray-200 pb-6">
        <div className="mb-1 text-xs font-semibold uppercase tracking-widest text-gray-400">RootFlow</div>
        <h1 className="text-2xl font-bold text-gray-900">{conversation.title}</h1>
        <div className="mt-2 text-sm text-gray-500">
          {t("conversations.printExportedOn", {
            date: formatAbsoluteDate(new Date().toISOString(), locale),
          })}
        </div>
      </div>

      {/* Messages */}
      <div className="space-y-6">
        {messages.map((message) => {
          const isUser = message.role === 2;

          return (
            <div key={message.id} className={`${isUser ? "pl-4" : ""}`}>
              <div className="mb-1.5 flex items-center gap-3">
                <span className={`text-[10px] font-bold uppercase tracking-widest ${isUser ? "text-blue-600" : "text-gray-500"}`}>
                  {isUser ? t("common.labels.you") : "RootFlow"}
                </span>
                <span className="text-[11px] text-gray-400">
                  {formatAbsoluteDate(message.createdAtUtc, locale)}
                </span>
              </div>
              <div
                className={`rounded-lg p-4 text-[0.93rem] leading-7 ${
                  isUser
                    ? "bg-blue-50 text-gray-800"
                    : "border border-gray-200 bg-gray-50 text-gray-700"
                }`}
              >
                <p className="whitespace-pre-wrap">{message.content}</p>
              </div>
            </div>
          );
        })}
      </div>

      {/* Footer */}
      <div className="mt-12 border-t border-gray-200 pt-4 text-xs text-gray-400">
        RootFlow — {t("conversations.printFooter")}
      </div>

      {/* Print-only close hint */}
      <div className="mt-6 text-center text-sm text-gray-400 print:hidden">
        {t("conversations.printCloseHint")}
      </div>
    </div>
  );
}
