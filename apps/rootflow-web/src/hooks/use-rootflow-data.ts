import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import type { AskQuestionPayload, DocumentSummary, UploadDocumentPayload } from "@/lib/api/contracts";
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

export function useConversationsQuery() {
  return useQuery({
    queryKey: queryKeys.conversations,
    queryFn: rootflowApi.listConversations,
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
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: AskQuestionPayload) => rootflowApi.askQuestion(payload),
    onSuccess: async (answer) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.conversations }),
        queryClient.invalidateQueries({ queryKey: queryKeys.conversation(answer.conversationId) }),
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
