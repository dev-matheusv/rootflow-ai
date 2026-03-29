import type {
  AskQuestionPayload,
  ChatAnswer,
  ConversationHistory,
  DocumentSummary,
  HealthResponse,
  UploadDocumentPayload,
} from "@/lib/api/contracts";
import { apiRequest } from "@/lib/api/client";

export const rootflowApi = {
  getHealth: () => apiRequest<HealthResponse>("/health"),
  listDocuments: () => apiRequest<DocumentSummary[]>("/api/documents"),
  uploadDocument: async ({ file }: UploadDocumentPayload) => {
    const formData = new FormData();
    formData.append("file", file);

    return apiRequest<DocumentSummary>("/api/documents", {
      method: "POST",
      body: formData,
    });
  },
  askQuestion: (payload: AskQuestionPayload) =>
    apiRequest<ChatAnswer>("/api/chat", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(payload),
    }),
  getConversationHistory: (conversationId: string) =>
    apiRequest<ConversationHistory>(`/api/conversations/${conversationId}`),
};
