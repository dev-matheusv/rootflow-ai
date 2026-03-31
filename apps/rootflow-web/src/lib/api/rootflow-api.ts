import type {
  AskQuestionPayload,
  AuthResponse,
  ChatAnswer,
  ConversationHistory,
  ConversationSummary,
  DocumentSummary,
  HealthResponse,
  LoginPayload,
  SessionInfo,
  SignupPayload,
  UploadDocumentPayload,
} from "@/lib/api/contracts";
import { apiRequest } from "@/lib/api/client";

export const rootflowApi = {
  getHealth: () => apiRequest<HealthResponse>("/health"),
  signup: (payload: SignupPayload) =>
    apiRequest<AuthResponse>("/api/auth/signup", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(payload),
    }),
  login: (payload: LoginPayload) =>
    apiRequest<AuthResponse>("/api/auth/login", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(payload),
    }),
  getCurrentSession: () => apiRequest<SessionInfo>("/api/auth/me"),
  listDocuments: () => apiRequest<DocumentSummary[]>("/api/documents"),
  listConversations: () => apiRequest<ConversationSummary[]>("/api/conversations"),
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
