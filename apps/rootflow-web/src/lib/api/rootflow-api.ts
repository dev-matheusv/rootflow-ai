import type {
  AskQuestionPayload,
  AcceptWorkspaceInvitePayload,
  AuthResponse,
  BillingCheckoutRedirect,
  BillingCheckoutSession,
  BillingPortalSession,
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
  DocumentTemplateDetail,
  DocumentTemplateSummary,
  CreateDocumentTemplatePayload,
  GenerateDocumentPayload,
  ForgotPasswordPayload,
  HealthResponse,
  InviteLookupResult,
  InviteWorkspaceMemberPayload,
  LoginPayload,
  MessageResponse,
  ResetPasswordPayload,
  SessionInfo,
  SignupPayload,
  SignupViaInvitePayload,
  UploadDocumentPayload,
  WorkspaceInvitationResult,
  WorkspaceMember,
  // Training Mode — admin
  TrainingProgramSummary,
  TrainingModuleSummary,
  TrainingProgramDetail,
  TrainingQuestion,
  CreateTrainingProgramPayload,
  UpdateTrainingProgramPayload,
  AddTrainingModulePayload,
  UpdateTrainingModulePayload,
  GenerateTrainingQuizPayload,
  UpdateTrainingQuestionPayload,
  // Training Mode — consumer
  AvailableTrainingProgram,
  AvailableTrainingProgramDetail,
  StartAttemptResult,
  SubmitTrainingAnswerPayload,
  AttemptResult,
  TrainingCertificateSummary,
  PublicCertificateVerification,
} from "@/lib/api/contracts";
import { apiRequest, apiRequestBlob } from "@/lib/api/client";

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
  syncSubscriptionTransaction: (transactionId: string) =>
    apiRequest<MessageResponse>(`/api/admin/billing/transactions/${transactionId}/sync`, {
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
  lookupInvite: (token: string) =>
    apiRequest<InviteLookupResult>(`/api/workspaces/invites/lookup?token=${encodeURIComponent(token)}`),
  signupViaInvite: (payload: SignupViaInvitePayload) =>
    apiRequest<AuthResponse>("/api/workspaces/invites/signup", {
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
  createBillingPortalSession: () =>
    apiRequest<BillingPortalSession>("/api/billing/portal", {
      method: "POST",
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
  listDocumentTemplates: () =>
    apiRequest<DocumentTemplateSummary[]>("/api/document-templates"),
  getDocumentTemplate: (templateId: string) =>
    apiRequest<DocumentTemplateDetail>(`/api/document-templates/${templateId}`),
  createDocumentTemplate: (payload: CreateDocumentTemplatePayload) =>
    apiRequest<DocumentTemplateSummary>("/api/document-templates", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    }),
  generateDocument: (templateId: string, payload: GenerateDocumentPayload) =>
    apiRequestBlob(`/api/document-templates/${templateId}/generate`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    }),

  // ── Training Mode (admin / authoring) ────────────────────────────────
  listTrainingPrograms: () =>
    apiRequest<TrainingProgramSummary[]>("/api/training/programs"),
  getTrainingProgramDetail: (programId: string) =>
    apiRequest<TrainingProgramDetail>(`/api/training/programs/${programId}`),
  createTrainingProgram: (payload: CreateTrainingProgramPayload) =>
    apiRequest<TrainingProgramSummary>("/api/training/programs", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    }),
  updateTrainingProgram: (programId: string, payload: UpdateTrainingProgramPayload) =>
    apiRequest<TrainingProgramSummary>(`/api/training/programs/${programId}`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    }),
  publishTrainingProgram: (programId: string) =>
    apiRequest<TrainingProgramSummary>(`/api/training/programs/${programId}/publish`, { method: "POST" }),
  unpublishTrainingProgram: (programId: string) =>
    apiRequest<TrainingProgramSummary>(`/api/training/programs/${programId}/unpublish`, { method: "POST" }),
  addTrainingModule: (programId: string, payload: AddTrainingModulePayload) =>
    apiRequest<TrainingModuleSummary>(`/api/training/programs/${programId}/modules`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    }),
  updateTrainingModule: (moduleId: string, payload: UpdateTrainingModulePayload) =>
    apiRequest<TrainingModuleSummary>(`/api/training/modules/${moduleId}`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    }),
  deleteTrainingModule: (moduleId: string) =>
    apiRequest<void>(`/api/training/modules/${moduleId}`, { method: "DELETE" }),
  listTrainingModuleQuestions: (moduleId: string) =>
    apiRequest<TrainingQuestion[]>(`/api/training/modules/${moduleId}/questions`),
  generateTrainingQuiz: (moduleId: string, payload: GenerateTrainingQuizPayload) =>
    apiRequest<TrainingQuestion[]>(`/api/training/modules/${moduleId}/generate-quiz`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    }),
  updateTrainingQuestion: (questionId: string, payload: UpdateTrainingQuestionPayload) =>
    apiRequest<TrainingQuestion>(`/api/training/questions/${questionId}`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    }),
  publishTrainingQuestion: (questionId: string) =>
    apiRequest<TrainingQuestion>(`/api/training/questions/${questionId}/publish`, { method: "POST" }),
  deleteTrainingQuestion: (questionId: string) =>
    apiRequest<void>(`/api/training/questions/${questionId}`, { method: "DELETE" }),

  // ── Training Mode (consumer / member-facing) ─────────────────────────
  listAvailableTrainingPrograms: () =>
    apiRequest<AvailableTrainingProgram[]>("/api/me/training/programs"),
  getAvailableTrainingProgram: (programId: string) =>
    apiRequest<AvailableTrainingProgramDetail>(`/api/me/training/programs/${programId}`),
  startTrainingAttempt: (moduleId: string) =>
    apiRequest<StartAttemptResult>(`/api/me/training/modules/${moduleId}/attempts`, { method: "POST" }),
  submitTrainingAnswer: (attemptId: string, payload: SubmitTrainingAnswerPayload) =>
    apiRequest<void>(`/api/me/training/attempts/${attemptId}/answer`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    }),
  submitTrainingAttempt: (attemptId: string) =>
    apiRequest<AttemptResult>(`/api/me/training/attempts/${attemptId}/submit`, { method: "POST" }),
  getTrainingAttempt: (attemptId: string) =>
    apiRequest<AttemptResult>(`/api/me/training/attempts/${attemptId}`),
  listTrainingCertificates: () =>
    apiRequest<TrainingCertificateSummary[]>("/api/me/training/certificates"),
  downloadTrainingCertificatePdf: (certificateId: string) =>
    apiRequestBlob(`/api/me/training/certificates/${certificateId}/pdf`),
  verifyTrainingCertificate: (code: string) =>
    apiRequest<PublicCertificateVerification>(`/api/public/training/verify/${encodeURIComponent(code)}`),
};
