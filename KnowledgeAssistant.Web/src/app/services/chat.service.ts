import { Injectable } from '@angular/core';
import { ChatRequest } from '../models/chat-request';
import { ModelInfo } from '../models/model-info';
import { ModelContextWindow } from '../models/model-context-window';
import { Conversation } from '../models/conversation';
import { ChatResponseChunk } from '../models/chat-response-chunk';

@Injectable({
  providedIn: 'root'
})
export class ChatService {
  // Relative path: requests are routed to the API by the nginx reverse proxy
  // (see KnowledgeAssistant.Web/nginx.conf), so this works from any host/network.
  private baseUrl = '';

  constructor() { }

  /** Throws a human-readable error when a fetch response is not successful. */
  private async assertOk(response: Response): Promise<void> {
    if (response.ok) {
      return;
    }

    let message = `Request failed with status ${response.status}.`;
    try {
      const body = await response.json();
      message = body?.detail ?? body?.title ?? message;
    } catch {
      // response had no JSON body; keep the default message
    }

    throw new Error(message);
  }

  async getModels(): Promise<ModelInfo[]> {
    const response = await fetch(`${this.baseUrl}/api/models`);
    await this.assertOk(response);
    return response.json();
  }

  async getConversations(): Promise<Conversation[]> {
    const response = await fetch(`${this.baseUrl}/api/conversations`);
    await this.assertOk(response);
    return response.json();
  }

  async getConversation(conversationId: string): Promise<Conversation> {
    const response = await fetch(`${this.baseUrl}/api/conversations/${conversationId}`);
    await this.assertOk(response);
    return response.json();
  }

  async newConversation(): Promise<Conversation> {
    const response = await fetch(`${this.baseUrl}/api/conversations`, {
      method: 'POST',
      body: null,
      headers: { 'Content-Type': 'application/json' }
    });
    await this.assertOk(response);
    return response.json();
  }
  
  async renameConversation(conversationId: string, newTitle: string): Promise<void> {
    const response = await fetch(`${this.baseUrl}/api/conversations/${conversationId}/title?newTitle=${encodeURIComponent(newTitle)}`, {
      method: 'PATCH'
    });
    await this.assertOk(response);
  }
  
  async setConversationTopic(conversationId: string, topicId: number | null): Promise<void> {
    const query = topicId != null ? `?topicId=${topicId}` : '';
    const response = await fetch(`${this.baseUrl}/api/conversations/${conversationId}/topic${query}`, {
      method: 'PATCH'
    });
    await this.assertOk(response);
  }

  async deleteConversation(conversationId: string): Promise<void> {
    const response = await fetch(`${this.baseUrl}/api/conversations/${conversationId}`, {
      method: 'DELETE'
    });
    await this.assertOk(response);
  }

  async updateSelectedModel(selectedModel: string): Promise<void> {
    const response = await fetch(`${this.baseUrl}/api/configuration/selected-model`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ selectedModel })
    });
    await this.assertOk(response);
  }

  async getSelectedModel(): Promise<string | null> {
    const response = await fetch(`${this.baseUrl}/api/configuration/selected-model`);
    await this.assertOk(response);
    const dto = await response.json();
    return dto?.selectedModel ?? null;
  }

  async getChunkingSettings(): Promise<{ chunkTargetSizeChars: number; chunkOverlapChars: number }> {
    const response = await fetch(`${this.baseUrl}/api/configuration/chunking-settings`);
    await this.assertOk(response);
    return response.json();
  }

  async updateChunkingSettings(chunkTargetSizeChars: number, chunkOverlapChars: number): Promise<void> {
    const response = await fetch(`${this.baseUrl}/api/configuration/chunking-settings`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ chunkTargetSizeChars, chunkOverlapChars })
    });
    await this.assertOk(response);
  }

  async getModelContextWindows(): Promise<ModelContextWindow[]> {
    const response = await fetch(`${this.baseUrl}/api/models/context-windows`);
    await this.assertOk(response);
    return response.json();
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

  await this.assertOk(response);

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
        if (currentEvent === 'done') {
          const tokenConsumption = JSON.parse(data);
          chunk.PromptTokens = tokenConsumption.PromptTokens;
          chunk.ResponseTokens = tokenConsumption.ResponseTokens;
        }
        
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