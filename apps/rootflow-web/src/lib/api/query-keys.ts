export const queryKeys = {
  health: ["health"] as const,
  documents: ["documents"] as const,
  conversation: (conversationId: string) => ["conversation", conversationId] as const,
};
