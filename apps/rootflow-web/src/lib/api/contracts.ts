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

export interface ChatSource {
  documentId: string;
  chunkId: string;
  documentName: string;
  sourceLabel: string;
  excerpt: string;
  score: number;
}

export interface ChatAnswer {
  conversationId: string;
  answer: string;
  modelName?: string | null;
  sources: ChatSource[];
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
