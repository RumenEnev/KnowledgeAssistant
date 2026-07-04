import { Injectable } from '@angular/core';
import { ChatRequest } from '../models/chat-request';
import { ModelInfo } from '../models/model-info';
import { Conversation } from '../models/conversation';
import { ChatResponseChunk } from '../models/chat-response-chunk';

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

  async getConversations(): Promise<Conversation[]> {
    const response = await fetch(`${this.baseUrl}/api/conversations`);
    return response.json();
  }

  async getConversation(conversationId: string): Promise<Conversation> {
    const response = await fetch(`${this.baseUrl}/api/conversations/${conversationId}`);
    return response.json();
  }

  async newConversation(): Promise<Conversation> {
    const response = await fetch(`${this.baseUrl}/api/conversations`, {
      method: 'POST',
      body: null,
      headers: { 'Content-Type': 'application/json' }
    });
    return response.json();
  }
  
  async renameConversation(conversationId: string, newTitle: string): Promise<void> {
    await fetch(`${this.baseUrl}/api/conversations/${conversationId}/title?newTitle=${encodeURIComponent(newTitle)}`, {
      method: 'PATCH'
    });
  }
  
  async deleteConversation(conversationId: string): Promise<void> {
    await fetch(`${this.baseUrl}/api/conversations/${conversationId}`, {
      method: 'DELETE'
    });
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