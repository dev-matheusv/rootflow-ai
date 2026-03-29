export const queryKeys = {
  health: ["health"] as const,
  documents: ["documents"] as const,
  conversations: ["conversations"] as const,
  conversation: (conversationId: string) => ["conversation", conversationId] as const,
};
