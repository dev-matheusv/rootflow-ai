import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import type {
  AskQuestionPayload,
  CreateWorkspaceCreditPurchaseCheckoutPayload,
  CreateWorkspaceSubscriptionCheckoutPayload,
  DocumentSummary,
  InviteWorkspaceMemberPayload,
  UploadDocumentPayload,
} from "@/lib/api/contracts";
import { rootflowApi } from "@/lib/api/rootflow-api";
import { queryKeys } from "@/lib/api/query-keys";

interface UseDocumentsQueryOptions {
  autoRefreshProcessing?: boolean;
}

export function useHealthQuery() {
  return useQuery({
    queryKey: queryKeys.health,
    queryFn: rootflowApi.getHealth,
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

export function useWorkspaceBillingSummaryQuery(workspaceId?: string | null) {
  return useQuery({
    queryKey: workspaceId ? queryKeys.workspaceBillingSummary(workspaceId) : ["workspace-billing-summary", "none"],
    queryFn: () => rootflowApi.getWorkspaceBillingSummary(workspaceId!),
    enabled: Boolean(workspaceId),
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
