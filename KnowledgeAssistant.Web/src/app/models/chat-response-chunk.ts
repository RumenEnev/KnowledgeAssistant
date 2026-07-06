export interface ChatResponseChunk {
  Type: string; // set from SSE event line, not from JSON body
  content?: string;
  conversationId?: string;
  messageId?: string;
  PromptTokens?: number;
  ResponseTokens?: number;
}