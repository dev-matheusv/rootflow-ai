import type {
  AskQuestionPayload,
  AcceptWorkspaceInvitePayload,
  AuthResponse,
  ChatAnswer,
  ConversationHistory,
  ConversationSummary,
  DocumentSummary,
  ForgotPasswordPayload,
  HealthResponse,
  InviteWorkspaceMemberPayload,
  LoginPayload,
  MessageResponse,
  ResetPasswordPayload,
  SessionInfo,
  SignupPayload,
  UploadDocumentPayload,
  WorkspaceInvitationResult,
  WorkspaceMember,
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
  forgotPassword: (payload: ForgotPasswordPayload) =>
    apiRequest<MessageResponse>("/api/auth/forgot-password", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(payload),
    }),
  resetPassword: (payload: ResetPasswordPayload) =>
    apiRequest<MessageResponse>("/api/auth/reset-password", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(payload),
    }),
  inviteWorkspaceMember: (workspaceId: string, payload: InviteWorkspaceMemberPayload) =>
    apiRequest<WorkspaceInvitationResult>(`/api/workspaces/${workspaceId}/invites`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(payload),
    }),
  acceptWorkspaceInvite: (payload: AcceptWorkspaceInvitePayload) =>
    apiRequest<AuthResponse>("/api/workspaces/invites/accept", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(payload),
    }),
  getCurrentSession: () => apiRequest<SessionInfo>("/api/auth/me"),
  listWorkspaceMembers: (workspaceId: string) => apiRequest<WorkspaceMember[]>(`/api/workspaces/${workspaceId}/members`),
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
