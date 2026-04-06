import type {
  AskQuestionPayload,
  AcceptWorkspaceInvitePayload,
  AuthResponse,
  BillingCheckoutRedirect,
  BillingCheckoutSession,
  BillingCreditPackSummary,
  BillingPlanSummary,
  PlatformAdminDashboard,
  PlatformAdminBillingMonitoringRunResult,
  PlatformAdminReplayStripeWebhooksResult,
  WorkspaceBillingSummary,
  ChatAnswer,
  CreateBillingCheckoutPayload,
  CreateWorkspaceCreditPurchaseCheckoutPayload,
  CreateWorkspaceSubscriptionCheckoutPayload,
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
  listBillingPlans: () => apiRequest<BillingPlanSummary[]>("/api/billing/plans"),
  listBillingCreditPacks: () => apiRequest<BillingCreditPackSummary[]>("/api/billing/credit-packs"),
  getPlatformAdminDashboard: () => apiRequest<PlatformAdminDashboard>("/api/admin/dashboard"),
  replayStripeWebhooks: () =>
    apiRequest<PlatformAdminReplayStripeWebhooksResult>("/api/admin/billing/replay-webhooks", {
      method: "POST",
    }),
  runBillingMonitoring: () =>
    apiRequest<PlatformAdminBillingMonitoringRunResult>("/api/admin/billing/run-monitoring", {
      method: "POST",
    }),
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
  getWorkspaceBillingSummary: (workspaceId: string) =>
    apiRequest<WorkspaceBillingSummary>(`/api/workspaces/${workspaceId}/billing/summary`),
  createSubscriptionCheckout: (payload: CreateWorkspaceSubscriptionCheckoutPayload) =>
    apiRequest<BillingCheckoutSession>("/api/billing/checkout/subscription", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(payload),
    }),
  createCreditPurchaseCheckout: (payload: CreateWorkspaceCreditPurchaseCheckoutPayload) =>
    apiRequest<BillingCheckoutSession>("/api/billing/checkout/credits", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(payload),
    }),
  createBillingCheckout: (payload: CreateBillingCheckoutPayload) =>
    apiRequest<BillingCheckoutRedirect>("/api/billing/checkout", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(payload),
    }),
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
