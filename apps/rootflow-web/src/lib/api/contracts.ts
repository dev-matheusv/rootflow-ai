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
  isPlatformAdmin: boolean;
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

export interface SignupViaInvitePayload {
  fullName: string;
  password: string;
  token: string;
}

export interface InviteLookupResult {
  email: string;
  workspaceName: string;
  inviterName: string;
  isExistingUser: boolean;
  isValid: boolean;
  errorMessage?: string | null;
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
  priceId?: string | null;
}

export interface BillingCreditPackSummary {
  code: string;
  name: string;
  description: string;
  credits: number;
  amount: number;
  currencyCode: string;
  isConfigured: boolean;
  priceId?: string | null;
}

export interface BillingCheckoutSession {
  sessionId: string;
  checkoutUrl: string;
}

export interface BillingCheckoutRedirect {
  sessionId: string;
  url: string;
}

export interface BillingPortalSession {
  sessionId: string;
  portalUrl: string;
}

export interface CreateBillingCheckoutPayload {
  priceId: string;
}

export interface CreateWorkspaceSubscriptionCheckoutPayload {
  planCode: string;
}

export interface CreateWorkspaceCreditPurchaseCheckoutPayload {
  creditPackCode: string;
}

export interface WorkspaceSubscriptionSummary {
  id: string;
  workspaceId: string;
  billingPlanId: string;
  status: string;
  currentPeriodStartUtc: string;
  currentPeriodEndUtc: string;
  trialEndsAtUtc?: string | null;
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
  currentPlanName?: string | null;
  subscriptionStatus?: string | null;
  trialEndsAtUtc?: string | null;
  isDegraded?: boolean;
  billingPlan?: BillingPlanSummary | null;
  subscription?: WorkspaceSubscriptionSummary | null;
  balance: WorkspaceCreditBalanceSummary;
}

export interface PlatformAdminOverview {
  totalWorkspaces: number;
  totalActiveSubscriptions: number;
  totalTrials: number;
  totalUsers: number;
  totalAvailableCredits: number;
  totalConsumedCredits: number;
  estimatedProviderCost: number;
  estimatedRevenueBasis: number;
  estimatedGrossMargin: number;
}

export interface PlatformAdminAlertCounts {
  lowCreditWorkspaces: number;
  noCreditWorkspaces: number;
  trialsExpiringSoon: number;
  paymentIssues: number;
  stripeWebhookIssues: number;
}

export interface PlatformAdminUsageWindow {
  key: string;
  workspaceCount: number;
  eventCount: number;
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
  creditsCharged: number;
  estimatedProviderCost: number;
  estimatedRevenueBasis: number;
  estimatedGrossMargin: number;
}

export interface PlatformAdminWorkspaceSummary {
  workspaceId: string;
  workspaceName: string;
  workspaceSlug: string;
  planName?: string | null;
  subscriptionStatus: string;
  memberCount: number;
  availableCredits: number;
  consumedCredits: number;
  totalTrackedCredits: number;
  remainingRatio: number;
  remainingPercent: number;
  trialEndsAtUtc?: string | null;
  lastUsageAtUtc?: string | null;
  creditsCharged: number;
  totalTokens: number;
  estimatedProviderCost: number;
  estimatedRevenueBasis: number;
  estimatedGrossMargin: number;
}

export interface PlatformAdminModelUsage {
  provider: string;
  model: string;
  workspaceCount: number;
  eventCount: number;
  creditsCharged: number;
  totalTokens: number;
  estimatedProviderCost: number;
  estimatedRevenueBasis: number;
  estimatedGrossMargin: number;
  lastUsedAtUtc?: string | null;
}

export interface PlatformAdminBillingTransaction {
  transactionId: string;
  workspaceId: string;
  workspaceName: string;
  workspaceSlug: string;
  type: string;
  status: string;
  planName?: string | null;
  credits?: number | null;
  amount: number;
  currencyCode: string;
  occurredAtUtc: string;
}

export interface PlatformAdminSubscriptionActivity {
  workspaceId: string;
  workspaceName: string;
  workspaceSlug: string;
  planName?: string | null;
  status: string;
  updatedAtUtc: string;
  currentPeriodEndUtc: string;
  trialEndsAtUtc?: string | null;
}

export interface PlatformAdminPaymentIssue {
  transactionId: string;
  workspaceId: string;
  workspaceName: string;
  workspaceSlug: string;
  type: string;
  status: string;
  amount: number;
  currencyCode: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface PlatformAdminStripeWebhookIssue {
  webhookEventId: string;
  providerEventId: string;
  eventType: string;
  status: string;
  attemptCount: number;
  firstReceivedAtUtc: string;
  lastReceivedAtUtc: string;
  updatedAtUtc: string;
  lastError?: string | null;
}

export interface PlatformAdminDashboard {
  overview: PlatformAdminOverview;
  alerts: PlatformAdminAlertCounts;
  billingOpsReadiness: PlatformAdminBillingOpsReadiness;
  usageWindows: PlatformAdminUsageWindow[];
  lowCreditWorkspaces: PlatformAdminWorkspaceSummary[];
  noCreditWorkspaces: PlatformAdminWorkspaceSummary[];
  trialsExpiringSoon: PlatformAdminWorkspaceSummary[];
  paymentIssues: PlatformAdminPaymentIssue[];
  stripeWebhookIssues: PlatformAdminStripeWebhookIssue[];
  recentCreditPurchases: PlatformAdminBillingTransaction[];
  recentSubscriptionChanges: PlatformAdminSubscriptionActivity[];
  topCreditConsumers: PlatformAdminWorkspaceSummary[];
  topProviderCostWorkspaces: PlatformAdminWorkspaceSummary[];
  topRevenueBasisWorkspaces: PlatformAdminWorkspaceSummary[];
  modelBreakdown: PlatformAdminModelUsage[];
}

export interface PlatformAdminBillingOpsReadiness {
  isReady: boolean;
  adminAlertRecipientCount: number;
  adminAlertRecipientsConfigured: boolean;
  outboundEmailConfigured: boolean;
  backgroundMonitoringEnabled: boolean;
}

export interface PlatformAdminReplayStripeWebhooksResult {
  replayedCount: number;
  message: string;
}

export interface PlatformAdminBillingMonitoringRunResult {
  adminAlertsSent: number;
  workspaceNotificationsSent: number;
  paymentIssueCount: number;
  replayableWebhookCount: number;
  message: string;
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

export interface TemplateFieldSummary {
  key: string;
  label: string;
  type: string;
  isRequired: boolean;
}

export interface DocumentTemplateSummary {
  id: string;
  workspaceId: string;
  name: string;
  slug: string;
  description?: string | null;
  isActive: boolean;
  fields: TemplateFieldSummary[];
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface DocumentTemplateDetail extends DocumentTemplateSummary {
  body: string;
}

export interface CreateDocumentTemplatePayload {
  name: string;
  slug?: string | null;
  description?: string | null;
  body: string;
  fields: TemplateFieldSummary[];
}

export interface GenerateDocumentPayload {
  fieldValues: Record<string, string>;
}

export interface DocumentTemplateDraft {
  name: string;
  body: string;
  fields: TemplateFieldSummary[];
}

export interface AiSuggestTemplatePayload {
  description: string;
}

export interface ConversationSummary {
  conversationId: string;
  title: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  messageCount: number;
  lastMessagePreview?: string | null;
}

// ── Training Mode ──────────────────────────────────────────────────────

export type TrainingQuestionType = "SingleChoice" | "MultiChoice" | "TrueFalse";
export type TrainingQuestionStatus = "Draft" | "Published";
export type TrainingAttemptStatus = "InProgress" | "Passed" | "Failed";
export type ConsumerModuleStatus = "NotStarted" | "InProgress" | "Failed" | "Passed";

export interface TrainingProgramSummary {
  id: string;
  workspaceId: string;
  name: string;
  slug: string;
  description?: string | null;
  passingScore: number;
  isPublished: boolean;
  createdByUserId: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface TrainingModuleSummary {
  id: string;
  programId: string;
  orderIndex: number;
  title: string;
  description?: string | null;
  sourceDocumentIds: string[];
  questionCount: number;
  publishedQuestionCount: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface TrainingProgramDetail {
  program: TrainingProgramSummary;
  modules: TrainingModuleSummary[];
}

export interface TrainingQuestion {
  id: string;
  moduleId: string;
  orderIndex: number;
  prompt: string;
  type: TrainingQuestionType;
  options: string[];
  correctAnswerIndices: number[];
  explanation?: string | null;
  sourceDocumentId?: string | null;
  sourceChunkId?: string | null;
  status: TrainingQuestionStatus;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateTrainingProgramPayload {
  name: string;
  slug?: string | null;
  description?: string | null;
}

export interface UpdateTrainingProgramPayload {
  name: string;
  description?: string | null;
  passingScore: number;
}

export interface AddTrainingModulePayload {
  title: string;
  description?: string | null;
  orderIndex: number;
  sourceDocumentIds: string[];
}

export interface UpdateTrainingModulePayload {
  title: string;
  description?: string | null;
  orderIndex: number;
  sourceDocumentIds: string[];
}

export interface GenerateTrainingQuizPayload {
  questionCount: number;
}

export interface UpdateTrainingQuestionPayload {
  prompt: string;
  type: TrainingQuestionType;
  options: string[];
  correctAnswerIndices: number[];
  explanation?: string | null;
}

// Consumer views (no correctAnswerIndices)
export interface AvailableTrainingProgram {
  id: string;
  name: string;
  slug: string;
  description?: string | null;
  passingScore: number;
  moduleCount: number;
  passedModuleCount: number;
  updatedAtUtc: string;
}

export interface ConsumerModule {
  id: string;
  orderIndex: number;
  title: string;
  description?: string | null;
  questionCount: number;
  status: ConsumerModuleStatus;
  latestScore?: number | null;
  lastAttemptedAtUtc?: string | null;
}

export interface AvailableTrainingProgramDetail {
  id: string;
  name: string;
  slug: string;
  description?: string | null;
  passingScore: number;
  modules: ConsumerModule[];
}

export interface ConsumerQuestion {
  id: string;
  orderIndex: number;
  prompt: string;
  type: TrainingQuestionType;
  options: string[];
}

export interface StartAttemptResult {
  attemptId: string;
  moduleId: string;
  passingScore: number;
  startedAtUtc: string;
  questions: ConsumerQuestion[];
}

export interface SubmitTrainingAnswerPayload {
  questionId: string;
  selectedIndices: number[];
}

export interface AttemptResult {
  attemptId: string;
  moduleId: string;
  programId: string;
  status: TrainingAttemptStatus;
  score: number;
  passingScore: number;
  completedAtUtc?: string | null;
  correctAnswerCount: number;
  totalQuestionCount: number;
}

export interface TrainingCertificateSummary {
  id: string;
  programId: string;
  programName: string;
  issuedAtUtc: string;
  code: string;
  verificationUrl: string;
}

export interface PublicCertificateVerification {
  isValid: boolean;
  employeeName?: string | null;
  programName?: string | null;
  workspaceName?: string | null;
  issuedAtUtc?: string | null;
  code?: string | null;
}
