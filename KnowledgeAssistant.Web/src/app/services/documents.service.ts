import { Injectable } from '@angular/core';
import { DocumentItem, Topic } from '../models/document';

@Injectable({
  providedIn: 'root'
})
export class DocumentsService {
  // Relative path: requests are routed to the API by the nginx reverse proxy
  // (see KnowledgeAssistant.Web/nginx.conf), so this works from any host/network.
  private baseUrl = '';

  /** Throws a human-readable error when a fetch response is not successful. */
  private async assertOk(response: Response): Promise<void> {
    if (response.ok) {
      return;
    }

    let message = `Request failed with status ${response.status}.`;
    const text = await response.text().catch(() => '');
    if (text) {
      try {
        const body = JSON.parse(text);
        message = body?.detail ?? body?.title ?? text;
      } catch {
        // response was plain text (e.g. BadRequest("...")); use it as-is
        message = text;
      }
    }

    throw new Error(message);
  }

  async getDocuments(): Promise<DocumentItem[]> {
    const response = await fetch(`${this.baseUrl}/api/documents`);
    await this.assertOk(response);
    return response.json();
  }

  async getTopics(): Promise<Topic[]> {
    const response = await fetch(`${this.baseUrl}/api/topics`);
    await this.assertOk(response);
    return response.json();
  }

  async createTopic(name: string): Promise<Topic> {
    const response = await fetch(`${this.baseUrl}/api/topics`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name })
    });
    await this.assertOk(response);
    return response.json();
  }

  async updateTopic(id: number, name: string): Promise<Topic> {
    const response = await fetch(`${this.baseUrl}/api/topics/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name })
    });
    await this.assertOk(response);
    return response.json();
  }

  async deleteTopic(id: number): Promise<void> {
    const response = await fetch(`${this.baseUrl}/api/topics/${id}`, {
      method: 'DELETE'
    });
    await this.assertOk(response);
  }

  async deleteDocument(id: number): Promise<void> {
    const response = await fetch(`${this.baseUrl}/api/documents/${id}`, {
      method: 'DELETE'
    });
    await this.assertOk(response);
  }

  async ingestText(title: string, text: string, topics: string[]): Promise<void> {
    const response = await fetch(`${this.baseUrl}/api/documents`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title, text, topics })
    });
    await this.assertOk(response);
  }

  async updateDocument(id: number, title: string, text: string, topics: string[]): Promise<void> {
    const response = await fetch(`${this.baseUrl}/api/documents/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title, text, topics })
    });
    await this.assertOk(response);
  }
}
