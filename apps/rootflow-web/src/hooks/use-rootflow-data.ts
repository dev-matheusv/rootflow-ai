import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import type {
  AddTrainingModulePayload,
  AskQuestionPayload,
  CreateBillingCheckoutPayload,
  CreateDocumentTemplatePayload,
  CreateTrainingProgramPayload,
  CreateWorkspaceCreditPurchaseCheckoutPayload,
  CreateWorkspaceSubscriptionCheckoutPayload,
  DocumentSummary,
  GenerateDocumentPayload,
  GenerateTrainingQuizPayload,
  InviteWorkspaceMemberPayload,
  UpdateTrainingModulePayload,
  UpdateTrainingProgramPayload,
  UpdateTrainingQuestionPayload,
  UploadDocumentPayload,
} from "@/lib/api/contracts";
import { rootflowApi } from "@/lib/api/rootflow-api";
import { queryKeys } from "@/lib/api/query-keys";

interface UseDocumentsQueryOptions {
  autoRefreshProcessing?: boolean;
}

interface UseWorkspaceBillingSummaryQueryOptions {
  enabled?: boolean;
  retry?: boolean | number;
}

export function useHealthQuery() {
  return useQuery({
    queryKey: queryKeys.health,
    queryFn: rootflowApi.getHealth,
  });
}

export function usePlatformAdminDashboardQuery(enabled = true) {
  return useQuery({
    queryKey: queryKeys.platformAdminDashboard,
    queryFn: rootflowApi.getPlatformAdminDashboard,
    enabled,
  });
}

export function useReplayStripeWebhooksMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: rootflowApi.replayStripeWebhooks,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.platformAdminDashboard });
    },
  });
}

export function useRunBillingMonitoringMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: rootflowApi.runBillingMonitoring,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.platformAdminDashboard });
    },
  });
}

export function useSyncSubscriptionTransactionMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (transactionId: string) => rootflowApi.syncSubscriptionTransaction(transactionId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.platformAdminDashboard });
    },
  });
}

export function useBillingPlansQuery() {
  return useQuery({
    queryKey: queryKeys.billingPlans,
    queryFn: rootflowApi.listBillingPlans,
  });
}

export function useBillingCreditPacksQuery() {
  return useQuery({
    queryKey: queryKeys.billingCreditPacks,
    queryFn: rootflowApi.listBillingCreditPacks,
  });
}

export function useDocumentsQuery(options?: UseDocumentsQueryOptions) {
  return useQuery({
    queryKey: queryKeys.documents,
    queryFn: rootflowApi.listDocuments,
    refetchInterval: options?.autoRefreshProcessing
      ? (query) => {
          const documents = query.state.data as DocumentSummary[] | undefined;
          return documents?.some((document) => document.status === 2) ? 3_000 : false;
        }
      : false,
  });
}

export function useWorkspaceMembersQuery(workspaceId?: string | null) {
  return useQuery({
    queryKey: workspaceId ? queryKeys.workspaceMembers(workspaceId) : ["workspace-members", "none"],
    queryFn: () => rootflowApi.listWorkspaceMembers(workspaceId!),
    enabled: Boolean(workspaceId),
  });
}

export function useWorkspaceBillingSummaryQuery(
  workspaceId?: string | null,
  options?: UseWorkspaceBillingSummaryQueryOptions,
) {
  return useQuery({
    queryKey: workspaceId ? queryKeys.workspaceBillingSummary(workspaceId) : ["workspace-billing-summary", "none"],
    queryFn: () => rootflowApi.getWorkspaceBillingSummary(workspaceId!),
    enabled: options?.enabled ?? Boolean(workspaceId),
    retry: options?.retry,
    staleTime: 15_000,
    placeholderData: (previousData) => previousData,
  });
}

export function useConversationsQuery() {
  return useQuery({
    queryKey: queryKeys.conversations,
    queryFn: rootflowApi.listConversations,
  });
}

export function useInviteWorkspaceMemberMutation(workspaceId?: string | null) {
  return useMutation({
    mutationFn: (payload: InviteWorkspaceMemberPayload) => rootflowApi.inviteWorkspaceMember(workspaceId!, payload),
  });
}

export function useUploadDocumentMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: UploadDocumentPayload) => rootflowApi.uploadDocument(payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.documents });
    },
  });
}

export function useAskQuestionMutation(workspaceId?: string | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: AskQuestionPayload) => rootflowApi.askQuestion(payload),
    onSettled: async (answer) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.conversations }),
        ...(answer ? [queryClient.invalidateQueries({ queryKey: queryKeys.conversation(answer.conversationId) })] : []),
        ...(workspaceId ? [queryClient.invalidateQueries({ queryKey: queryKeys.workspaceBillingSummary(workspaceId) })] : []),
      ]);
    },
  });
}

export function useConversationQuery(conversationId?: string | null) {
  return useQuery({
    queryKey: conversationId ? queryKeys.conversation(conversationId) : ["conversation", "none"],
    queryFn: () => rootflowApi.getConversationHistory(conversationId!),
    enabled: Boolean(conversationId),
  });
}

export function useSubscriptionCheckoutMutation() {
  return useMutation({
    mutationFn: (payload: CreateWorkspaceSubscriptionCheckoutPayload) => rootflowApi.createSubscriptionCheckout(payload),
  });
}

export function useCreditPurchaseCheckoutMutation() {
  return useMutation({
    mutationFn: (payload: CreateWorkspaceCreditPurchaseCheckoutPayload) => rootflowApi.createCreditPurchaseCheckout(payload),
  });
}

export function useBillingCheckoutMutation() {
  return useMutation({
    mutationFn: (payload: CreateBillingCheckoutPayload) => rootflowApi.createBillingCheckout(payload),
  });
}

export function useDocumentTemplatesQuery() {
  return useQuery({
    queryKey: queryKeys.documentTemplates,
    queryFn: rootflowApi.listDocumentTemplates,
  });
}

export function useDocumentTemplateQuery(templateId?: string | null) {
  return useQuery({
    queryKey: templateId ? queryKeys.documentTemplate(templateId) : ["document-template", "none"],
    queryFn: () => rootflowApi.getDocumentTemplate(templateId!),
    enabled: Boolean(templateId),
  });
}

export function useCreateDocumentTemplateMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: CreateDocumentTemplatePayload) => rootflowApi.createDocumentTemplate(payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.documentTemplates });
    },
  });
}

export function useGenerateDocumentMutation() {
  return useMutation({
    mutationFn: ({ templateId, payload }: { templateId: string; payload: GenerateDocumentPayload }) =>
      rootflowApi.generateDocument(templateId, payload),
  });
}

// ── Training Mode (admin / authoring) ──────────────────────────────────

export function useTrainingProgramsQuery() {
  return useQuery({
    queryKey: queryKeys.trainingPrograms,
    queryFn: rootflowApi.listTrainingPrograms,
  });
}

export function useTrainingProgramQuery(programId?: string | null) {
  return useQuery({
    queryKey: programId ? queryKeys.trainingProgram(programId) : ["training-program", "none"],
    queryFn: () => rootflowApi.getTrainingProgramDetail(programId!),
    enabled: Boolean(programId),
  });
}

export function useTrainingModuleQuestionsQuery(moduleId?: string | null) {
  return useQuery({
    queryKey: moduleId ? queryKeys.trainingModuleQuestions(moduleId) : ["training-module-questions", "none"],
    queryFn: () => rootflowApi.listTrainingModuleQuestions(moduleId!),
    enabled: Boolean(moduleId),
  });
}

export function useCreateTrainingProgramMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: CreateTrainingProgramPayload) => rootflowApi.createTrainingProgram(payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.trainingPrograms });
    },
  });
}

export function useUpdateTrainingProgramMutation(programId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: UpdateTrainingProgramPayload) =>
      rootflowApi.updateTrainingProgram(programId, payload),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.trainingPrograms }),
        queryClient.invalidateQueries({ queryKey: queryKeys.trainingProgram(programId) }),
      ]);
    },
  });
}

export function usePublishTrainingProgramMutation(programId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => rootflowApi.publishTrainingProgram(programId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.trainingPrograms }),
        queryClient.invalidateQueries({ queryKey: queryKeys.trainingProgram(programId) }),
      ]);
    },
  });
}

export function useUnpublishTrainingProgramMutation(programId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => rootflowApi.unpublishTrainingProgram(programId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.trainingPrograms }),
        queryClient.invalidateQueries({ queryKey: queryKeys.trainingProgram(programId) }),
      ]);
    },
  });
}

export function useAddTrainingModuleMutation(programId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: AddTrainingModulePayload) => rootflowApi.addTrainingModule(programId, payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.trainingProgram(programId) });
    },
  });
}

export function useUpdateTrainingModuleMutation(programId: string, moduleId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: UpdateTrainingModulePayload) => rootflowApi.updateTrainingModule(moduleId, payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.trainingProgram(programId) });
    },
  });
}

export function useDeleteTrainingModuleMutation(programId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (moduleId: string) => rootflowApi.deleteTrainingModule(moduleId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.trainingProgram(programId) });
    },
  });
}

export function useGenerateTrainingQuizMutation(programId: string, moduleId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: GenerateTrainingQuizPayload) => rootflowApi.generateTrainingQuiz(moduleId, payload),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.trainingModuleQuestions(moduleId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.trainingProgram(programId) }),
      ]);
    },
  });
}

export function useUpdateTrainingQuestionMutation(moduleId: string, programId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ questionId, payload }: { questionId: string; payload: UpdateTrainingQuestionPayload }) =>
      rootflowApi.updateTrainingQuestion(questionId, payload),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.trainingModuleQuestions(moduleId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.trainingProgram(programId) }),
      ]);
    },
  });
}

export function usePublishTrainingQuestionMutation(moduleId: string, programId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (questionId: string) => rootflowApi.publishTrainingQuestion(questionId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.trainingModuleQuestions(moduleId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.trainingProgram(programId) }),
      ]);
    },
  });
}

export function useDeleteTrainingQuestionMutation(moduleId: string, programId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (questionId: string) => rootflowApi.deleteTrainingQuestion(questionId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.trainingModuleQuestions(moduleId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.trainingProgram(programId) }),
      ]);
    },
  });
}

// ── Training Mode (consumer) ──────────────────────────────────────────

export function useAvailableTrainingProgramsQuery() {
  return useQuery({
    queryKey: queryKeys.availableTrainingPrograms,
    queryFn: rootflowApi.listAvailableTrainingPrograms,
  });
}

export function useAvailableTrainingProgramQuery(programId?: string | null) {
  return useQuery({
    queryKey: programId ? queryKeys.availableTrainingProgram(programId) : ["available-training-program", "none"],
    queryFn: () => rootflowApi.getAvailableTrainingProgram(programId!),
    enabled: Boolean(programId),
  });
}

export function useStartTrainingAttemptMutation() {
  return useMutation({
    mutationFn: (moduleId: string) => rootflowApi.startTrainingAttempt(moduleId),
  });
}

export function useSubmitTrainingAnswerMutation(attemptId: string) {
  return useMutation({
    mutationFn: (payload: { questionId: string; selectedIndices: number[] }) =>
      rootflowApi.submitTrainingAnswer(attemptId, payload),
  });
}

export function useSubmitTrainingAttemptMutation(programId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (attemptId: string) => rootflowApi.submitTrainingAttempt(attemptId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.availableTrainingProgram(programId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.availableTrainingPrograms }),
        queryClient.invalidateQueries({ queryKey: queryKeys.trainingCertificates }),
      ]);
    },
  });
}

export function useTrainingCertificatesQuery() {
  return useQuery({
    queryKey: queryKeys.trainingCertificates,
    queryFn: rootflowApi.listTrainingCertificates,
  });
}

export function useTrainingCertificateVerificationQuery(code: string | null) {
  return useQuery({
    queryKey: code ? queryKeys.certificateVerification(code) : ["training-cert-verify", "none"],
    queryFn: () => rootflowApi.verifyTrainingCertificate(code!),
    enabled: Boolean(code),
    retry: false,
  });
}
