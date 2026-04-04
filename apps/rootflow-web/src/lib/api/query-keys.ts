export const queryKeys = {
  health: ["health"] as const,
  billingPlans: ["billing-plans"] as const,
  billingCreditPacks: ["billing-credit-packs"] as const,
  workspaceBillingSummary: (workspaceId: string) => ["workspace-billing-summary", workspaceId] as const,
  workspaceMembers: (workspaceId: string) => ["workspace-members", workspaceId] as const,
  documents: ["documents"] as const,
  conversations: ["conversations"] as const,
  conversation: (conversationId: string) => ["conversation", conversationId] as const,
};
