export interface HealthResponse {
  status: string;
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
