export interface ChatRequest {
  conversationId?: string;
  message: string;
  model?: string;
  temperature?: number;
  source?: 'Web' | 'Desktop';
}
