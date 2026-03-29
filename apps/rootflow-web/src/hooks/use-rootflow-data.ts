import { useMutation, useQueries, useQuery, useQueryClient } from "@tanstack/react-query";

import type { AskQuestionPayload, UploadDocumentPayload } from "@/lib/api/contracts";
import { rootflowApi } from "@/lib/api/rootflow-api";
import { queryKeys } from "@/lib/api/query-keys";

export function useHealthQuery() {
  return useQuery({
    queryKey: queryKeys.health,
    queryFn: rootflowApi.getHealth,
  });
}

export function useDocumentsQuery() {
  return useQuery({
    queryKey: queryKeys.documents,
    queryFn: rootflowApi.listDocuments,
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

export function useAskQuestionMutation() {
  return useMutation({
    mutationFn: (payload: AskQuestionPayload) => rootflowApi.askQuestion(payload),
  });
}

export function useConversationQuery(conversationId?: string | null) {
  return useQuery({
    queryKey: conversationId ? queryKeys.conversation(conversationId) : ["conversation", "none"],
    queryFn: () => rootflowApi.getConversationHistory(conversationId!),
    enabled: Boolean(conversationId),
  });
}

export function useConversationsByIds(conversationIds: string[]) {
  return useQueries({
    queries: conversationIds.map((conversationId) => ({
      queryKey: queryKeys.conversation(conversationId),
      queryFn: () => rootflowApi.getConversationHistory(conversationId),
      staleTime: 30_000,
    })),
  });
}
