export interface ChatRequest {
  conversationId?: string;
  message: string;
  model?: string;
  provider?: string;
  temperature?: number;
  source?: 'Web' | 'Desktop';
}
