export interface HealthResponse {
  status: string;
}

export type WorkspaceRole = "Owner" | "Admin" | "Member";

export interface AuthUser {
  id: string;
  fullName: string;
  email: string;
}

export interface AuthWorkspace {
  id: string;
  name: string;
  slug: string;
}

export interface SessionInfo {
  user: AuthUser;
  workspace: AuthWorkspace;
  role: WorkspaceRole;
}

export interface AuthResponse {
  token: string;
  expiresAtUtc: string;
  session: SessionInfo;
}

export interface MessageResponse {
  message: string;
}

export interface LoginPayload {
  email: string;
  password: string;
}

export interface SignupPayload {
  fullName: string;
  email: string;
  password: string;
  workspaceName: string;
}

export interface ForgotPasswordPayload {
  email: string;
}

export interface ResetPasswordPayload {
  token: string;
  newPassword: string;
}

export interface InviteWorkspaceMemberPayload {
  email: string;
  role?: WorkspaceRole | null;
}

export interface AcceptWorkspaceInvitePayload {
  token: string;
}

export interface WorkspaceInvitationResult {
  message: string;
  email: string;
  role: WorkspaceRole;
  expiresAtUtc: string;
}

export interface WorkspaceMember {
  userId: string;
  fullName: string;
  email: string;
  role: WorkspaceRole;
  createdAtUtc: string;
  isCurrentUser: boolean;
}

export interface DocumentSummary {
  id: string;
  workspaceId: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  status: number;
  createdAtUtc: string;
  processedAtUtc?: string | null;
  failureReason?: string | null;
}

export interface UploadDocumentPayload {
  file: File;
}

export interface AskQuestionPayload {
  question: string;
  conversationId?: string | null;
  maxContextChunks?: number;
}

export interface BillingPlanSummary {
  id: string;
  code: string;
  name: string;
  monthlyPrice: number;
  currencyCode: string;
  includedCredits: number;
  maxUsers: number;
  isActive: boolean;
}

export interface WorkspaceSubscriptionSummary {
  id: string;
  workspaceId: string;
  billingPlanId: string;
  status: string;
  currentPeriodStartUtc: string;
  currentPeriodEndUtc: string;
  canceledAtUtc?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface WorkspaceCreditBalanceSummary {
  workspaceId: string;
  availableCredits: number;
  consumedCredits: number;
  updatedAtUtc: string;
}

export interface WorkspaceBillingSummary {
  billingPlan?: BillingPlanSummary | null;
  subscription?: WorkspaceSubscriptionSummary | null;
  balance: WorkspaceCreditBalanceSummary;
}

export interface ChatSource {
  documentId: string;
  chunkId: string;
  documentName: string;
  sourceLabel: string;
  excerpt: string;
  score: number;
}

export interface ChatRetrievedChunkDebug {
  rank: number;
  chunkId: string;
  documentName: string;
  sourceLabel: string;
  sequence: number;
  score: number;
  vectorScore: number;
  keywordScore: number;
  matchedTerms: string[];
  reason: string;
}

export interface ChatRagDebug {
  query: string;
  historyMessageCount: number;
  retrievedChunkCount: number;
  retrievedChunks: ChatRetrievedChunkDebug[];
}

export interface ChatAnswer {
  conversationId: string;
  answer: string;
  modelName?: string | null;
  sources: ChatSource[];
  debug?: ChatRagDebug | null;
}

export interface ConversationMessage {
  id: string;
  role: number;
  content: string;
  modelName?: string | null;
  createdAtUtc: string;
}

export interface ConversationHistory {
  conversationId: string;
  workspaceId: string;
  title: string;
  messages: ConversationMessage[];
}

export interface ConversationSummary {
  conversationId: string;
  title: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  messageCount: number;
  lastMessagePreview?: string | null;
}
