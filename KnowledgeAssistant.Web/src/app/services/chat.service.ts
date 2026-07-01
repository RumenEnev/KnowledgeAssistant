import { Injectable } from '@angular/core';

export interface ModelInfo {
  name: string;
}

export interface ConversationInfo {
  id: string;
  title?: string;
  selectedModelId: string;
  createdOn: string;
  updatedOn: string;
  version: number;
}

export interface ChatRequest {
  conversationId?: string;
  message: string;
  model?: string;
  temperature?: number;
}

export interface ChatResponseChunk {
  Type: string; // set from SSE event line, not from JSON body
  content?: string;
  conversationId?: string;
  messageId?: string;
}

export interface Message {
  role: 'user' | 'assistant';
  content: string;
  streaming?: boolean;
}

export interface Conversation {
  id: string;
  title: string;
  messages: Message[];
}

@Injectable({
  providedIn: 'root'
})
export class ChatService {
  private baseUrl = 'http://localhost:5299';

  constructor() { }

  async getModels(): Promise<ModelInfo[]> {
    const response = await fetch(`${this.baseUrl}/api/models`);
    return response.json();
  }

  async getConversations(): Promise<ConversationInfo[]> {
    const response = await fetch(`${this.baseUrl}/api/conversations`);
    return response.json();
  }

  async newConversation(): Promise<string> {
    const response = await fetch(`${this.baseUrl}/api/conversations`, {
      method: 'POST',
      body: null,
      headers: { 'Content-Type': 'application/json' }
    });
    // const data = await response.json();
    //return data;
    return '';
  }
  
  async streamChat(
  request: ChatRequest,
  onEvent: (event: ChatResponseChunk) => void
): Promise<void> {

  const response = await fetch(`${this.baseUrl}/api/chat`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request)
  });

  if (!response.body) {
    throw new Error('No response body received from server.');
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();

  let buffer = '';
  let currentEvent = '';

  while (true) {
    const { done, value } = await reader.read();
    if (done) break;

    buffer += decoder.decode(value, { stream: true });

    const lines = buffer.split('\n');
    buffer = lines.pop() ?? '';

    for (const line of lines) {
      if (!line.trim()) continue;

      if (line.startsWith('event: ')) {
        currentEvent = line.slice(7).trim();
        continue;
      }

      if (line.startsWith('data: ')) {
        const data = line.slice(6).trim();

        const chunk: ChatResponseChunk = JSON.parse(data);

        // attach SSE event type into payload if needed
        const enrichedEvent: ChatResponseChunk = {
          ...chunk,
          Type: currentEvent || chunk.Type
        };

        onEvent(enrichedEvent);
      }
    }
  }
}
}