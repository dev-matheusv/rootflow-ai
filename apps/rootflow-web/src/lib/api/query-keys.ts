export const queryKeys = {
  health: ["health"] as const,
  workspaceMembers: (workspaceId: string) => ["workspace-members", workspaceId] as const,
  documents: ["documents"] as const,
  conversations: ["conversations"] as const,
  conversation: (conversationId: string) => ["conversation", conversationId] as const,
};
